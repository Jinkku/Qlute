using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

/// <summary>
/// Online chat (autoload "OnlineChat"): the server's public channels (#general...) and private rooms you're
/// invited to by username. Needs a logged-in account; connects by itself and reconnects if the line drops.
/// Protocol: Chikara/online/PROTOCOL.md. Rooms / lines / invites reuse LanChat's types so the overlay shows both alike.
/// </summary>
public partial class OnlineChat : Node
{
	public const string EnabledSetting = "onlinechat";
	private const double PingInterval = 20;
	private const int MaxLines = 300;

	public enum ConnState { Off, LoggedOut, Connecting, Online, Failed }

	public static OnlineChat Instance { get; private set; }

	public ConnState State { get; private set; } = ConnState.LoggedOut;
	/// <summary>Why we aren't connected, for the overlay.</summary>
	public string StatusText { get; private set; } = "Log in to use online chat.";
	/// <summary>Our account name as the server knows it.</summary>
	public string Username { get; private set; }
	/// <summary>Channel id ("#general", "p12") -> room. Public channels first, in server order.</summary>
	public readonly Dictionary<string, LanChat.Room> Rooms = new();
	public readonly List<LanChat.Invite> Invites = new();
	public readonly SortedSet<string> OnlineUsers = new(StringComparer.OrdinalIgnoreCase);
	public int Version { get; private set; }
	/// <summary>Set when a private room we just made arrives, so the overlay can open it.</summary>
	public string JustCreated { get; set; }

	private WebSocketPeer _ws;
	private double _retry, _ping;
	private int _backoff = 5;
	private string _authKey;    // username + hash we last tried, so a refused login isn't retried until it changes
	private bool _authRefused;
	private bool _creating;

	public static bool Enabled
	{
		get
		{
			var v = SettingsOperator.GetSetting(EnabledSetting)?.ToString();
			return v == null || Check.CheckBoolValue(v);
		}
	}

	public override void _Ready()
	{
		Instance = this;
		ProcessMode = ProcessModeEnum.Always;
		// Dev testing: --online-test-user=NAME --online-test-pass=SHA256 logs in without the real login flow.
		var args = OS.GetCmdlineUserArgs();
		var user = args.FirstOrDefault(a => a.StartsWith("--online-test-user="))?["--online-test-user=".Length..];
		if (user != null)
		{
			ApiOperator.Username = user;
			ApiOperator.PasswordHash = args.FirstOrDefault(a => a.StartsWith("--online-test-pass="))?["--online-test-pass=".Length..];
			SettingsOperator.SessionConfig.Loggedin = true;
		}
	}

	public override void _ExitTree() => Disconnect();

	public void SetEnabled(bool on)
	{
		SettingsOperator.SetSetting(EnabledSetting, on);
		if (!on) { Disconnect(); SetState(ConnState.Off, "Online chat is off."); }
		else { _retry = 0; _authRefused = false; }
		Version++;
	}

	// =====================================================================
	// Public API (overlay)
	// =====================================================================

	public bool Connected => State == ConnState.Online;
	/// <summary>We've been welcomed at least once; rooms and history stay visible while a dropped line reconnects.</summary>
	public bool HasSession => Username != null;
	public bool Reconnecting => HasSession && !Connected;

	public void Send(string roomId, string text)
	{
		text = LanChat.Clean(text, LanChat.MaxTextLength, null);
		if (text == null || !Rooms.ContainsKey(roomId)) return;
		if (!Connected) { Notify.Post("Not connected to online chat yet, so your message wasn't sent.", Type: NotificationIcons.NotificationType.Warning); return; }
		SendOp(new JsonObject { ["op"] = "msg", ["channel"] = roomId, ["text"] = text });
	}

	public void CreatePrivateRoom(string title)
	{
		_creating = true;
		SendOp(new JsonObject { ["op"] = "create", ["title"] = LanChat.Clean(title, LanChat.MaxRoomTitleLength, $"{Username}'s room") });
	}

	public void Invite(string roomId, string user)
	{
		user = LanChat.Clean(user, 256, null);
		if (user == null || !Rooms.TryGetValue(roomId, out var room) || room.IsPublic) return;
		room.Invited.Add(user);
		Version++;
		SendOp(new JsonObject { ["op"] = "invite", ["room"] = roomId, ["user"] = user });
	}

	public void AcceptInvite(LanChat.Invite inv)
	{
		Invites.Remove(inv);
		Version++;
		SendOp(new JsonObject { ["op"] = "accept", ["room"] = inv.RoomId });
	}

	public void DeclineInvite(LanChat.Invite inv)
	{
		Invites.Remove(inv);
		Version++;
		SendOp(new JsonObject { ["op"] = "decline", ["room"] = inv.RoomId });
	}

	public void LeaveRoom(string roomId)
	{
		if (!Rooms.TryGetValue(roomId, out var room) || room.IsPublic) return;
		SendOp(new JsonObject { ["op"] = "leave", ["room"] = roomId });
	}

	public void MarkRead(string roomId)
	{
		if (Rooms.TryGetValue(roomId, out var r) && r.Unread != 0) { r.Unread = 0; Version++; }
	}

	public bool IsUserOnline(string name) => name == Username || OnlineUsers.Contains(name);

	// =====================================================================
	// Connection
	// =====================================================================

	public override void _Process(double delta)
	{
		if (!Enabled) { if (State != ConnState.Off) { Disconnect(); SetState(ConnState.Off, "Online chat is off."); } return; }
		if (!OnlineApi.Available)
		{
			if (State != ConnState.LoggedOut) { Disconnect(); SetState(ConnState.LoggedOut, "Log in to use online chat."); }
			return;
		}
		var key = ApiOperator.Username + "|" + ApiOperator.PasswordHash;
		if (key != _authKey) { _authKey = key; _authRefused = false; if (_ws != null) Disconnect(); _retry = 0; }

		if (_ws == null)
		{
			if (_authRefused) return;
			_retry -= delta;
			if (_retry > 0) return;
			Open();
			return;
		}

		_ws.Poll();
		var state = _ws.GetReadyState();
		if (state == WebSocketPeer.State.Closed)
		{
			bool refused = _ws.GetCloseCode() == OnlineApi.AuthFailedCode || _authRefused;
			var reason = refused ? StatusText : OnlineApi.CloseReason(_ws, "Can't reach the online chat right now.");
			bool wasOnline = State == ConnState.Online;
			_ws = null;
			_authRefused = refused;
			if (refused || !HasSession) ClearSession(); // keep rooms on screen through a blip
			if (refused) { SetState(ConnState.Failed, reason); return; }
			if (wasOnline)
			{
				// The line just dropped (network hiccup, server restart): try again right away.
				_retry = 1;
				_backoff = 5;
			}
			else
			{
				_retry = _backoff;
				_backoff = Math.Min(_backoff * 2, 60);
			}
			SetState(ConnState.Failed, HasSession ? "Lost the online chat connection. Reconnecting..." : $"{reason} Retrying...");
			return;
		}
		if (state != WebSocketPeer.State.Open) return;

		while (_ws != null && _ws.GetAvailablePacketCount() > 0)
			Handle(OnlineApi.ParseJson(_ws.GetPacket()));

		_ping -= delta;
		if (_ping <= 0) { _ping = PingInterval; SendOp(new JsonObject { ["op"] = "ping" }); }
	}

	private void Open()
	{
		_ws = OnlineApi.Connect("chat", out var err);
		if (err != Error.Ok)
		{
			_ws = null;
			_retry = _backoff;
			_backoff = Math.Min(_backoff * 2, 60);
			SetState(ConnState.Failed, $"Can't reach the online chat ({err}). Retrying...");
			return;
		}
		_ping = PingInterval;
		SetState(ConnState.Connecting, HasSession ? "Reconnecting to online chat..." : "Connecting to online chat...");
	}

	private void Disconnect()
	{
		_ws?.Close(1000, "bye");
		_ws = null;
		ClearSession();
	}

	private void ClearSession()
	{
		Rooms.Clear();
		Invites.Clear();
		OnlineUsers.Clear();
		Username = null;
		Version++;
	}

	private void SetState(ConnState s, string text)
	{
		if (State == s && StatusText == text) return;
		State = s;
		StatusText = text;
		Version++;
	}

	private void SendOp(JsonObject op) => OnlineApi.SendJson(_ws, op);

	// =====================================================================
	// Server messages. Unknown ops / fields are ignored (newer servers).
	// =====================================================================

	private void Handle(JsonObject m)
	{
		switch (OnlineApi.Str(m, "op"))
		{
			case "welcome": OnWelcome(m); break;
			case "history": OnHistory(m); break;
			case "msg":
			{
				if (!Rooms.TryGetValue(OnlineApi.Str(m, "channel") ?? "", out var room)) break;
				var author = OnlineApi.Str(m, "author") ?? "?";
				AddLine(room, author, author, OnlineApi.Str(m, "text"), OnlineApi.Long(m, "ts"), markUnread: author != Username);
				break;
			}
			case "notice":
				if (Rooms.TryGetValue(OnlineApi.Str(m, "channel") ?? "", out var nroom))
					AddLine(nroom, null, null, OnlineApi.Str(m, "text"), 0, markUnread: false);
				break;
			case "room": if (m["room"] is JsonObject r) UpsertRoom(r); break;
			case "left":
				if (Rooms.Remove(OnlineApi.Str(m, "room") ?? "")) Version++;
				break;
			case "invite":
			{
				var id = OnlineApi.Str(m, "room");
				if (id == null || Rooms.ContainsKey(id)) break;
				Invites.RemoveAll(i => i.RoomId == id);
				var inv = new LanChat.Invite { RoomId = id, Title = OnlineApi.Str(m, "title") ?? "Room", FromId = OnlineApi.Str(m, "from"), FromName = OnlineApi.Str(m, "from") ?? "?" };
				Invites.Add(inv);
				Version++;
				Notify.Post($"{inv.FromName} invited you to the online chat room \"{inv.Title}\". Open chat to answer.");
				break;
			}
			case "invite_gone":
				if (Invites.RemoveAll(i => i.RoomId == OnlineApi.Str(m, "room")) > 0) Version++;
				break;
			case "presence":
			{
				var user = OnlineApi.Str(m, "user");
				if (user == null) break;
				if (OnlineApi.Bool(m, "online")) OnlineUsers.Add(user); else OnlineUsers.Remove(user);
				Version++;
				break;
			}
			case "online_list": ReadOnline(m); break;
			case "error":
				if (OnlineApi.Bool(m, "fatal"))
				{
					_authRefused = true;
					StatusText = OnlineApi.Str(m, "text") ?? "The server refused your login.";
				}
				else Notify.Post(OnlineApi.Str(m, "text") ?? "Online chat error.", Type: NotificationIcons.NotificationType.Warning);
				break;
		}
	}

	private void OnWelcome(JsonObject m)
	{
		ClearSession();
		Username = OnlineApi.Str(m, "user") ?? ApiOperator.Username;
		if (m["channels"] is JsonArray chans)
			foreach (var c in chans.OfType<JsonObject>())
			{
				var id = OnlineApi.Str(c, "id");
				if (id != null) Rooms[id] = new LanChat.Room { Id = id, Title = OnlineApi.Str(c, "title") ?? id, IsPublic = true };
			}
		if (m["rooms"] is JsonArray rooms)
			foreach (var r in rooms.OfType<JsonObject>()) UpsertRoom(r);
		if (m["invites"] is JsonArray invs)
			foreach (var i in invs.OfType<JsonObject>())
				Invites.Add(new LanChat.Invite { RoomId = OnlineApi.Str(i, "room"), Title = OnlineApi.Str(i, "title") ?? "Room", FromId = OnlineApi.Str(i, "from"), FromName = OnlineApi.Str(i, "from") ?? "?" });
		ReadOnline(m);
		_backoff = 5;
		SetState(ConnState.Online, "");
		Version++;
	}

	private void ReadOnline(JsonObject m)
	{
		if (m["online"] is not JsonArray arr) return;
		OnlineUsers.Clear();
		foreach (var u in arr) if (u is JsonValue v && v.TryGetValue<string>(out var s)) OnlineUsers.Add(s);
		Version++;
	}

	private void OnHistory(JsonObject m)
	{
		if (!Rooms.TryGetValue(OnlineApi.Str(m, "channel") ?? "", out var room) || m["lines"] is not JsonArray lines) return;
		room.Lines.Clear();
		foreach (var l in lines.OfType<JsonObject>())
		{
			var author = OnlineApi.Str(l, "author") ?? "?";
			room.Lines.Add(new LanChat.Line { FromId = author, Name = author, Text = OnlineApi.Str(l, "text") ?? "", Time = OnlineApi.Long(l, "ts") });
		}
		if (room.Lines.Count == 0 && room.IsPublic)
			room.Lines.Add(new LanChat.Line { Text = $"Welcome to {room.Title}! Everyone logged in to Qlute can read this channel.", Time = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() });
		Version++;
	}

	private void UpsertRoom(JsonObject r)
	{
		var id = OnlineApi.Str(r, "id");
		if (id == null) return;
		bool isNew = !Rooms.TryGetValue(id, out var room);
		if (isNew) { room = new LanChat.Room { Id = id }; Rooms[id] = room; }
		room.Title = OnlineApi.Str(r, "title") ?? "Room";
		room.Members.Clear();
		if (r["members"] is JsonArray members)
			foreach (var mm in members) if (mm is JsonValue v && v.TryGetValue<string>(out var name)) { room.Members[name] = name; room.Invited.Remove(name); }
		if (isNew && _creating && OnlineApi.Str(r, "owner") == Username) { _creating = false; JustCreated = id; }
		Version++;
	}

	private void AddLine(LanChat.Room room, string fromId, string name, string text, long ts, bool markUnread)
	{
		if (string.IsNullOrEmpty(text)) return;
		room.Lines.Add(new LanChat.Line { FromId = fromId, Name = name, Text = text, Time = ts > 0 ? ts : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() });
		if (room.Lines.Count > MaxLines) room.Lines.RemoveRange(0, room.Lines.Count - MaxLines);
		if (markUnread) room.Unread++;
		Version++;
	}
}
