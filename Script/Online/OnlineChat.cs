using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

/// <summary>
/// Online chat (autoload "OnlineChat"): the server's public channels (#general...), private rooms you're
/// invited to by username, and direct messages. Needs a logged-in account; connects by itself and reconnects if
/// the line drops. It's also how the game hears about a ban (and its end) the moment it happens, and about
/// players adding us as a friend.
/// Protocol: Chikara/online/PROTOCOL.md. Rooms / lines / invites reuse LanChat's types so the overlay shows both alike.
/// </summary>
public partial class OnlineChat : Node
{
	public const string EnabledSetting = "onlinechat";
	private const double PingInterval = 20;
	private const int MaxLines = 300;
	/// <summary>What we tell the server we understand (see Chikara/online/PROTOCOL.md).</summary>
	private const string Features = "ban,dm,friends";

	public enum ConnState { Off, LoggedOut, Connecting, Online, Failed, Restricted }

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
	/// <summary>Set when a private room we just made (or a DM we opened) arrives, so the overlay can open it.</summary>
	public string JustCreated { get; set; }
	/// <summary>The account is restricted; <see cref="StatusText"/> says why and until when.</summary>
	public bool Restricted => State == ConnState.Restricted;
	/// <summary>Raised when something wants the chat overlay opened (e.g. "message" on a profile).</summary>
	public static event Action OpenRequested;
	/// <summary>Online room id the chat overlay is showing right now (null when it's closed), so we don't notify about it.</summary>
	public static string VisibleRoom { get; set; }

	private WebSocketPeer _ws;
	private double _retry, _ping;
	private int _backoff = 5;
	private string _authKey;    // username + hash we last tried, so a refused login isn't retried until it changes
	private bool _authRefused;
	private bool _creating;
	/// <summary>We've seen this login unrestricted (a welcome), so a "restricted" now is a fresh ban.</summary>
	private bool _sawUnrestricted;
	private bool _sawRestricted;
	private string _pendingDm;

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
			if (args.Any(a => a.StartsWith("--mod-test="))) AddChild(new ModerationSelfTest());
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

	/// <summary>Leave a private room, or close a DM (it comes back with the next message).</summary>
	public void LeaveRoom(string roomId)
	{
		if (!Rooms.TryGetValue(roomId, out var room) || room.IsPublic) return;
		SendOp(new JsonObject { ["op"] = "leave", ["room"] = roomId });
	}

	public void MarkRead(string roomId)
	{
		if (Rooms.TryGetValue(roomId, out var r) && (r.Unread != 0 || r.Mentioned)) { r.Unread = 0; r.Mentioned = false; Version++; }
	}

	public bool IsUserOnline(string name) => name == Username || OnlineUsers.Contains(name);

	/// <summary>Opens (or starts) a DM with that player and shows it in the chat overlay.</summary>
	public void OpenDm(string user)
	{
		user = LanChat.Clean(user, 256, null);
		if (user == null || user == Username) return;
		OpenRequested?.Invoke();
		var existing = Rooms.Values.FirstOrDefault(r => r.IsDm && string.Equals(r.With, user, StringComparison.OrdinalIgnoreCase));
		if (existing != null) { JustCreated = existing.Id; Version++; return; }
		if (!Connected)
		{
			_pendingDm = user; // sent once we're connected
			if (!OnlineApi.Available) Notify.Post("Log in to send messages.", Type: NotificationIcons.NotificationType.Warning);
			return;
		}
		SendOp(new JsonObject { ["op"] = "dm_open", ["user"] = user });
	}

	/// <summary>True when the text @mentions that name ("@Name", any case, not part of a longer word).</summary>
	public static bool Mentions(string text, string name) =>
		!string.IsNullOrEmpty(text) && !string.IsNullOrEmpty(name)
		&& Regex.IsMatch(text, $@"(?<![\w@])@{Regex.Escape(name)}(?![\w])", RegexOptions.IgnoreCase);

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
		if (key != _authKey) { _authKey = key; _authRefused = false; ResetAccountState(); if (_ws != null) Disconnect(); _retry = 0; }

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
			if (_ws.GetCloseCode() == OnlineApi.ReconnectCode)
			{
				// Banned or unbanned just now: connect again right away to get the new state.
				_ws = null;
				ClearSession();
				_retry = 0.3;
				SetState(ConnState.Connecting, StatusText);
				return;
			}
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
		_ws = OnlineApi.Connect("chat", out var err, Features);
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

	/// <summary>A new login starts fresh: the first "restricted" we hear for it isn't news.</summary>
	private void ResetAccountState() { _sawUnrestricted = false; _sawRestricted = false; }

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
				var text = OnlineApi.Str(m, "text");
				AddLine(room, author, author, text, OnlineApi.Long(m, "ts"), markUnread: author != Username);
				if (author != Username) Alert(room, author, text);
				break;
			}
			case "notice":
				if (Rooms.TryGetValue(OnlineApi.Str(m, "channel") ?? "", out var nroom))
					AddLine(nroom, null, null, OnlineApi.Str(m, "text"), 0, markUnread: false);
				break;
			case "room":
				if (m["room"] is JsonObject r)
				{
					bool known = Rooms.ContainsKey(OnlineApi.Str(r, "id") ?? "");
					UpsertRoom(r);
					if (OnlineApi.Bool(m, "focus")) JustCreated = OnlineApi.Str(r, "id");
					// Someone started a DM with us: say so (its first message comes in the history right after).
					else if (!known && OnlineApi.Bool(r, "dm") && Rooms.TryGetValue(OnlineApi.Str(r, "id"), out var dm)) _announceDm = dm;
				}
				break;
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
			case "restricted": OnRestricted(OnlineApi.Str(m, "text"), OnlineApi.Long(m, "expires")); break;
			case "unrestricted":
				_sawRestricted = false;
				_sawUnrestricted = true;
				SetState(ConnState.Connecting, "Your restriction was lifted. Reconnecting...");
				PlayerOverlays.ShowMessage("Restriction lifted", OnlineApi.Str(m, "text") ?? "Your restriction has been lifted.", ModUi.Green);
				Notify.Post("Your account isn't restricted anymore. Welcome back!");
				break;
			case "friend":
			{
				var who = OnlineApi.Str(m, "user");
				if (who == null || !OnlineApi.Bool(m, "added")) break;
				Notify.Post(OnlineApi.Bool(m, "mutual") ? $"{who} added you back. You're friends now!" : $"{who} added you as a friend.");
				PlayerApi.RefreshFriends();
				break;
			}
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

	/// <summary>The account is restricted. A fresh ban (we were fine a moment ago) gets a pop-up; at startup the
	/// login already told the player, so it's just the chat status.</summary>
	private void OnRestricted(string text, long expires)
	{
		text ??= "Your account is restricted.";
		if (expires > 0) text = text.TrimEnd();
		ClearSession();
		bool fresh = _sawUnrestricted && !_sawRestricted;
		_sawRestricted = true;
		_sawUnrestricted = false;
		SetState(ConnState.Restricted, text);
		if (fresh)
		{
			PlayerOverlays.ShowMessage("Account restricted", text, ModUi.Red);
			Notify.Post("Your account was just restricted.", Type: NotificationIcons.NotificationType.Warning);
		}
	}

	private LanChat.Room _announceDm;

	/// <summary>Pings (@us) and DMs get a notification, unless the overlay is already showing that room.</summary>
	private void Alert(LanChat.Room room, string author, string text)
	{
		bool mentioned = Mentions(text, Username);
		if (mentioned) room.Mentioned = true;
		if (VisibleRoom == room.Id) return;
		var preview = text.Length > 80 ? text[..80] + "..." : text;
		if (room.IsDm) Notify.Post($"{author} (message): {preview}");
		else if (mentioned) Notify.Post($"{author} mentioned you in {room.Title}: {preview}");
	}

	private void OnWelcome(JsonObject m)
	{
		_sawUnrestricted = true;
		_sawRestricted = false;
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
		PlayerApi.RefreshFriends(); // (re)connected = logged in: get the friends list
		if (_pendingDm != null) { var who = _pendingDm; _pendingDm = null; SendOp(new JsonObject { ["op"] = "dm_open", ["user"] = who }); }
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
		if (room.Lines.Count == 0 && room.IsDm)
			room.Lines.Add(new LanChat.Line { Text = $"This is the start of your messages with {room.With}. Only the two of you can read them.", Time = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() });
		if (_announceDm == room)
		{
			// A new conversation someone else started: its newest line is what they just sent.
			_announceDm = null;
			var last = room.Lines.LastOrDefault(l => l.FromId != null && l.FromId != Username);
			if (last != null) { room.Unread++; Alert(room, last.Name, last.Text); }
		}
		Version++;
	}

	private void UpsertRoom(JsonObject r)
	{
		var id = OnlineApi.Str(r, "id");
		if (id == null) return;
		bool isNew = !Rooms.TryGetValue(id, out var room);
		if (isNew) { room = new LanChat.Room { Id = id }; Rooms[id] = room; }
		room.Title = OnlineApi.Str(r, "title") ?? "Room";
		room.IsDm = OnlineApi.Bool(r, "dm");
		room.With = OnlineApi.Str(r, "with");
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
