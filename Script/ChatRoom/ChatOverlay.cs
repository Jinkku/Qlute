using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

/// <summary>
/// The chat overlay opened from the top panel. Shows both chats side by side:
/// online (<see cref="OnlineChat"/>: server channels, private rooms and direct messages, needs a login) and LAN
/// (<see cref="LanChat"/>). Rooms + invites on the left, the selected room in the middle, people on the right.
/// "@name" pings someone: it's highlighted, and lines that ping you stand out. Tab completes a name after "@".
/// </summary>
public partial class ChatOverlay : PanelContainer
{
	[ExportGroup("Nodes")]
	[Export] public Label Status { get; set; }
	[Export] public VBoxContainer RoomList { get; set; }
	[Export] public Button NewRoomButton { get; set; }
	[Export] public RichTextLabel Log { get; set; }
	[Export] public LineEdit Input { get; set; }
	[Export] public Label Empty { get; set; }
	[Export] public Label PeopleTitle { get; set; }
	[Export] public VBoxContainer PeopleList { get; set; }
	[Export] public Button LeaveButton { get; set; }
	[Export] public LineEdit NameEdit { get; set; }
	[Export] public CheckButton PowerButton { get; set; }
	[Export] public Button TurnOnButton { get; set; }

	/// <summary>Theme type variation (Resources/System/theme.tres) every chat box button uses.</summary>
	public const string ButtonVariation = "ChatboxButton";
	private const int MaxPeopleShown = 100;

	private const string LanPrefix = "lan:", OnlinePrefix = "on:";

	/// <summary>Selected room key ("on:#general", "lan:lan"...). Remembered across open/close of the overlay.</summary>
	private static string _selected;
	private int _shownLan = -1, _shownOnline = -1;
	private string _shownRoom;
	private PopupMenu _newRoomMenu;
	private string _inviteDraft = "";

	private static LanChat Lan => LanChat.Instance;
	private static OnlineChat Online => OnlineChat.Instance;

	public override void _Ready()
	{
		NewRoomButton.ThemeTypeVariation = ButtonVariation;
		LeaveButton.ThemeTypeVariation = ButtonVariation;
		TurnOnButton.ThemeTypeVariation = ButtonVariation;

		_newRoomMenu = new PopupMenu();
		_newRoomMenu.AddItem("Online room (invite by username)", 0);
		_newRoomMenu.AddItem("LAN room (people on this network)", 1);
		_newRoomMenu.IdPressed += id => CreateRoom(online: id == 0);
		AddChild(_newRoomMenu);
		NewRoomButton.Pressed += OnNewRoomPressed;

		LeaveButton.Pressed += () =>
		{
			if (IsOnlineKey(_selected)) Online?.LeaveRoom(RoomId(_selected));
			else Lan?.LeaveRoom(RoomId(_selected));
			_selected = null;
			Refresh();
		};
		TurnOnButton.Pressed += () => Lan?.SetEnabled(true);
		PowerButton.Toggled += on => Lan?.SetEnabled(on);
		NameEdit.Text = LanChat.Nickname;
		NameEdit.TextSubmitted += text => { SaveName(text); NameEdit.ReleaseFocus(); };
		NameEdit.FocusExited += () => SaveName(NameEdit.Text);
		Input.GuiInput += OnInputKey;
		Input.TextSubmitted += text =>
		{
			Input.Clear();
			if (_selected == null) return;
			if (IsOnlineKey(_selected)) Online?.Send(RoomId(_selected), text);
			else Lan?.Send(RoomId(_selected), text);
		};
		Refresh();
	}

	public override void _ExitTree() => OnlineChat.VisibleRoom = null;

	public override void _Process(double delta)
	{
		// What the player is looking at right now (no ping / DM notifications for it).
		OnlineChat.VisibleRoom = SettingsOperator.SessionConfig.ChatBoxVisible && IsOnlineKey(_selected) ? RoomId(_selected) : null;
		if (Online?.JustCreated is { } created)
		{
			Online.JustCreated = null;
			_selected = OnlinePrefix + created;
		}
		if ((Lan?.Version ?? 0) != _shownLan || (Online?.Version ?? 0) != _shownOnline || _shownRoom != _selected) Refresh();
	}

	// =====================================================================
	// Rooms from both chats
	// =====================================================================

	private static bool IsOnlineKey(string key) => key?.StartsWith(OnlinePrefix) == true;
	private static string RoomId(string key) => key == null ? null : key[(IsOnlineKey(key) ? OnlinePrefix.Length : LanPrefix.Length)..];

	private static List<(string key, LanChat.Room room)> OnlineRooms() =>
		Online?.HasSession == true ? Online.Rooms.Values.Select(r => (OnlinePrefix + r.Id, r)).ToList() : new();

	private static List<(string key, LanChat.Room room)> LanRooms() =>
		Lan?.Running == true ? Lan.VisibleRooms().Select(r => (LanPrefix + r.Id, r)).ToList() : new();

	private static LanChat.Room Find(string key)
	{
		if (key == null) return null;
		if (IsOnlineKey(key)) return Online?.HasSession == true && Online.Rooms.TryGetValue(RoomId(key), out var o) ? o : null;
		return Lan?.Running == true && Lan.VisibleRooms().FirstOrDefault(r => r.Id == RoomId(key)) is { } l ? l : null;
	}

	private void Refresh()
	{
		_shownLan = Lan?.Version ?? 0;
		_shownOnline = Online?.Version ?? 0;
		RefreshName();
		PowerButton.SetPressedNoSignal(LanChat.Enabled);

		var online = OnlineRooms();
		var lan = LanRooms();
		if (Find(_selected) == null) _selected = online.FirstOrDefault().key ?? lan.FirstOrDefault().key;
		var room = Find(_selected);
		_shownRoom = _selected;
		if (room != null)
		{
			if (IsOnlineKey(_selected)) Online.MarkRead(room.Id); else Lan.MarkRead(room.Id);
			_shownLan = Lan?.Version ?? 0;
			_shownOnline = Online?.Version ?? 0;
		}

		Status.Text = $"Online: {OnlineSummary()}   ·   LAN: {LanSummary()}";
		NewRoomButton.Disabled = Online?.Connected != true && Lan?.Running != true;
		TurnOnButton.Visible = room == null && !LanChat.Enabled;

		BuildRoomList(online, lan);
		BuildPeople(room);

		ShowEmpty(room == null);
		if (room == null)
		{
			Empty.Text = EmptyText();
			return;
		}
		var me = IsOnlineKey(_selected) ? Online.Username : LanChat.LocalName();
		Input.PlaceholderText = room.IsDm
			? $"Message {room.With} (only the two of you can read this)"
			: $"Chatting as {me} in {room.Title}" + (IsOnlineKey(_selected) && room.IsPublic ? " (everyone online can read this)" : "") + "  ·  @name to ping";
		if (IsOnlineKey(_selected) && !Online.Connected) Input.PlaceholderText = Online.StatusText;
		Input.Editable = !IsOnlineKey(_selected) || Online.Connected;
		LeaveButton.Visible = !room.IsPublic;
		LeaveButton.Text = room.IsDm ? "Close" : "Leave";
		LeaveButton.TooltipText = room.IsDm ? "Hide this conversation (it comes back when either of you writes)" : "Leave this room";
		Log.Clear();
		Log.AppendText(string.Join("\n", room.Lines.Select(l => Format(l, IsOnlineKey(_selected)))));
	}

	private static string OnlineSummary()
	{
		var o = Online;
		if (o == null) return "unavailable";
		return o.State switch
		{
			OnlineChat.ConnState.Online => $"{o.OnlineUsers.Count} player{(o.OnlineUsers.Count == 1 ? "" : "s")} online",
			_ when o.Reconnecting => "reconnecting...",
			OnlineChat.ConnState.Connecting => "connecting...",
			OnlineChat.ConnState.Off => "off",
			OnlineChat.ConnState.Restricted => "restricted",
			OnlineChat.ConnState.LoggedOut => "log in to chat",
			_ => "offline",
		};
	}

	private static string LanSummary()
	{
		var c = Lan;
		if (c == null || !c.Running) return LanChat.Enabled ? "unavailable" : "invisible";
		return c.Peers.Count switch { 0 => "nobody else here", 1 => "1 other player", var n => $"{n} other players" };
	}

	private static string EmptyText()
	{
		var lines = new List<string>();
		var o = Online;
		if (o != null && o.State != OnlineChat.ConnState.Online) lines.Add(o.StatusText);
		if (Lan == null || !Lan.Running)
			lines.Add(LanChat.Enabled
				? $"LAN chat couldn't open UDP port {LanChat.PresencePort} (in use or blocked by a firewall)."
				: "LAN chat is off: nobody on the network can see you, and you can't see them.");
		else lines.Add("Nobody else is playing on this network. The LAN chat shows up as soon as someone starts the game.");
		return string.Join("\n\n", lines.Where(l => !string.IsNullOrWhiteSpace(l)));
	}

	private void RefreshName()
	{
		bool offline = LanChat.CanUseNickname;
		NameEdit.Editable = offline;
		NameEdit.TooltipText = offline
			? "Your name in LAN chat and LAN rooms while you aren't logged in."
			: "You're logged in, so your account name is used.";
		if (!NameEdit.HasFocus()) NameEdit.Text = offline ? LanChat.Nickname : "";
		NameEdit.PlaceholderText = offline ? $"Name when offline ({LanChat.LocalName()})" : LanChat.LocalName();
	}

	private void SaveName(string text)
	{
		if (!LanChat.CanUseNickname || LanChat.Clean(text, LanChat.MaxNameLength, "") == LanChat.Nickname) return;
		Lan?.SetNickname(text);
	}

	private void ShowEmpty(bool empty)
	{
		Empty.Visible = empty;
		Log.Visible = !empty;
		Input.Visible = !empty;
		if (empty) LeaveButton.Visible = false;
	}

	private void OnNewRoomPressed()
	{
		bool online = Online?.Connected == true, lan = Lan?.Running == true;
		if (online && lan)
		{
			var r = NewRoomButton.GetGlobalRect();
			_newRoomMenu.Popup(new Rect2I((Vector2I)(r.Position + new Vector2(0, -60)), Vector2I.Zero));
		}
		else if (online || lan) CreateRoom(online);
	}

	private void CreateRoom(bool online)
	{
		if (online) Online?.CreatePrivateRoom(null); // opens when the server confirms it (JustCreated)
		else if (Lan != null) _selected = LanPrefix + Lan.CreatePrivateRoom(null);
		Refresh();
	}

	// =====================================================================
	// Left column
	// =====================================================================

	private void BuildRoomList(List<(string key, LanChat.Room room)> online, List<(string key, LanChat.Room room)> lan)
	{
		foreach (var child in RoomList.GetChildren()) child.QueueFree();

		RoomList.AddChild(Header("Online"));
		if (online.Count == 0) RoomList.AddChild(Hint(Online?.StatusText is { Length: > 0 } t ? t : "Not connected."));
		else if (Online?.Reconnecting == true) RoomList.AddChild(Hint("Reconnecting..."));
		foreach (var (key, r) in online.Where(x => x.room.IsPublic)) RoomList.AddChild(RoomButton(key, r));
		if (online.Any(x => x.room.IsDm)) RoomList.AddChild(Header("Messages"));
		foreach (var (key, r) in online.Where(x => x.room.IsDm).OrderByDescending(x => x.room.Lines.LastOrDefault()?.Time ?? 0))
			RoomList.AddChild(RoomButton(key, r));
		if (online.Any(x => !x.room.IsPublic && !x.room.IsDm)) RoomList.AddChild(Header("Online · private"));
		foreach (var (key, r) in online.Where(x => !x.room.IsPublic && !x.room.IsDm)) RoomList.AddChild(RoomButton(key, r));

		RoomList.AddChild(Header("LAN"));
		if (lan.Count == 0) RoomList.AddChild(Hint(Lan?.Running == true ? "Nobody else on this network." : LanChat.Enabled ? "Unavailable." : "Off (invisible)."));
		foreach (var (key, r) in lan.Where(x => x.room.IsPublic)) RoomList.AddChild(RoomButton(key, r));
		if (lan.Any(x => !x.room.IsPublic)) RoomList.AddChild(Header("LAN · private"));
		foreach (var (key, r) in lan.Where(x => !x.room.IsPublic)) RoomList.AddChild(RoomButton(key, r));

		var invites = (Online?.Connected == true ? Online.Invites.Select(i => (online: true, inv: i)) : Enumerable.Empty<(bool, LanChat.Invite)>())
			.Concat(Lan?.Running == true ? Lan.Invites.Select(i => (online: false, inv: i)) : Enumerable.Empty<(bool, LanChat.Invite)>())
			.ToList();
		if (invites.Count > 0) RoomList.AddChild(Header("Invites"));
		foreach (var (isOnline, inv) in invites)
		{
			var box = new VBoxContainer();
			box.AddChild(new Label
			{
				Text = $"{inv.Title} ({(isOnline ? "online" : "LAN")})\nfrom {inv.FromName}",
				AutowrapMode = TextServer.AutowrapMode.WordSmart,
			});
			var row = new HBoxContainer();
			var accept = new Button { Text = "Join", SizeFlagsHorizontal = SizeFlags.ExpandFill, ThemeTypeVariation = ButtonVariation };
			var decline = new Button { Text = "Decline", SizeFlagsHorizontal = SizeFlags.ExpandFill, ThemeTypeVariation = ButtonVariation };
			accept.Pressed += () =>
			{
				if (isOnline) Online.AcceptInvite(inv); else Lan.AcceptInvite(inv);
				_selected = (isOnline ? OnlinePrefix : LanPrefix) + inv.RoomId; // online: opens once the server confirms
				Refresh();
			};
			decline.Pressed += () => { if (isOnline) Online.DeclineInvite(inv); else Lan.DeclineInvite(inv); Refresh(); };
			row.AddChild(accept);
			row.AddChild(decline);
			box.AddChild(row);
			RoomList.AddChild(box);
		}
	}

	private Button RoomButton(string key, LanChat.Room r)
	{
		var title = r.IsDm ? (Online?.IsUserOnline(r.With) == true ? "● " : "○ ") + r.Title : r.Title;
		var badge = key == _selected ? "" : r.Mentioned ? $"  (@{(r.Unread > 0 ? " " + r.Unread : "")})" : r.Unread > 0 ? $"  ({r.Unread})" : "";
		var b = new Button
		{
			Text = title + badge,
			Alignment = HorizontalAlignment.Left,
			ToggleMode = true,
			ButtonPressed = key == _selected,
			ThemeTypeVariation = ButtonVariation,
			TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
		};
		b.Pressed += () => { _selected = key; Refresh(); };
		return b;
	}

	// =====================================================================
	// Right column
	// =====================================================================

	private void BuildPeople(LanChat.Room room)
	{
		foreach (var child in PeopleList.GetChildren()) child.QueueFree();
		if (room == null) { PeopleTitle.Text = ""; return; }
		if (IsOnlineKey(_selected)) BuildOnlinePeople(room);
		else BuildLanPeople(room);
	}

	private void BuildOnlinePeople(LanChat.Room room)
	{
		var o = Online;
		if (room.IsDm)
		{
			PeopleTitle.Text = room.With;
			PeopleList.AddChild(PersonLabel(o.IsUserOnline(room.With) ? "• online now" : "• offline (they'll see it later)"));
			if (PlayerApi.Friends.TryGetValue(room.With, out var mutual))
				PeopleList.AddChild(Hint(mutual ? "You're friends." : "On your friends list."));
			var profile = new Button { Text = "View profile", ThemeTypeVariation = ButtonVariation };
			var with = room.With;
			profile.Pressed += () => PlayerOverlays.OpenProfile(with);
			PeopleList.AddChild(profile);
			return;
		}
		if (room.IsPublic)
		{
			PeopleTitle.Text = $"Online now ({o.OnlineUsers.Count})";
			foreach (var name in o.OnlineUsers.Take(MaxPeopleShown))
				PeopleList.AddChild(PersonLabel(name == o.Username ? $"• {name} (you)" : $"• {name}"));
			if (o.OnlineUsers.Count > MaxPeopleShown) PeopleList.AddChild(Hint($"and {o.OnlineUsers.Count - MaxPeopleShown} more"));
			return;
		}

		PeopleTitle.Text = $"In {room.Title}";
		foreach (var name in room.Members.Keys.OrderBy(n => n))
		{
			var you = name == o.Username ? " (you)" : "";
			var away = o.IsUserOnline(name) ? "" : " - offline";
			PeopleList.AddChild(PersonLabel($"• {name}{you}{away}"));
		}

		PeopleList.AddChild(Header("Invite by username"));
		var row = new HBoxContainer();
		var edit = new LineEdit { PlaceholderText = "Username", Text = _inviteDraft, SizeFlagsHorizontal = SizeFlags.ExpandFill, MaxLength = 64 };
		var send = new Button { Text = "Invite", ThemeTypeVariation = ButtonVariation };
		var roomId = room.Id;
		void Submit()
		{
			var who = edit.Text.Trim();
			if (who.Length == 0) return;
			_inviteDraft = "";
			o.Invite(roomId, who);
		}
		edit.TextChanged += t => _inviteDraft = t;
		edit.TextSubmitted += _ => Submit();
		send.Pressed += Submit;
		row.AddChild(edit);
		row.AddChild(send);
		PeopleList.AddChild(row);

		var others = o.OnlineUsers.Where(n => n != o.Username && !room.Members.ContainsKey(n)).Take(MaxPeopleShown).ToList();
		PeopleList.AddChild(Header(others.Count > 0 ? "Online now" : "Nobody else online to invite"));
		foreach (var name in others) PeopleList.AddChild(InviteRow(name, null, room.Invited.Contains(name), () => o.Invite(roomId, name)));
	}

	private void BuildLanPeople(LanChat.Room room)
	{
		var c = Lan;
		if (!room.IsPublic)
		{
			PeopleTitle.Text = $"In {room.Title}";
			foreach (var (id, name) in room.Members.OrderBy(m => m.Value))
			{
				var you = id == c.LocalId ? " (you)" : "";
				var away = c.IsOnline(id) ? "" : " - offline";
				PeopleList.AddChild(PersonLabel($"• {name}{you}{away}"));
			}
			var others = c.Peers.Values.Where(p => !room.Members.ContainsKey(p.Id)).OrderBy(p => p.Name).ToList();
			PeopleList.AddChild(Header(others.Count > 0 ? "Invite from the LAN" : "No one else to invite"));
			var roomId = room.Id;
			foreach (var p in others)
			{
				var peerId = p.Id;
				PeopleList.AddChild(InviteRow(p.Name, PeerTooltip(p), room.Invited.Contains(p.Id), () => c.InvitePeer(roomId, peerId)));
			}
			return;
		}

		PeopleTitle.Text = "On this network";
		PeopleList.AddChild(PersonLabel($"• {LanChat.LocalName()} (you)"));
		foreach (var p in c.Peers.Values.OrderBy(p => p.Name))
		{
			var l = PersonLabel($"• {p.Name}");
			l.TooltipText = PeerTooltip(p);
			PeopleList.AddChild(l);
		}
	}

	private static HBoxContainer InviteRow(string name, string tooltip, bool sent, Action invite)
	{
		var row = new HBoxContainer();
		var label = PersonLabel(name);
		label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		label.TooltipText = tooltip ?? "";
		row.AddChild(label);
		var button = new Button { Text = sent ? "Sent" : "Invite", Disabled = sent, ThemeTypeVariation = ButtonVariation };
		button.Pressed += () => { invite(); button.Disabled = true; button.Text = "Sent"; };
		row.AddChild(button);
		return row;
	}

	private static Label PersonLabel(string text) =>
		new() { Text = text, TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis, MouseFilter = MouseFilterEnum.Pass };

	private static string PeerTooltip(LanChat.Peer p) => $"{p.EndPoint?.Address} - game {p.GameVersion}";

	private static Label Header(string text)
	{
		var l = new Label { Text = text };
		l.AddThemeColorOverride("font_color", new Color(1, 1, 1, 0.55f));
		return l;
	}

	private static Label Hint(string text)
	{
		var l = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart };
		l.AddThemeColorOverride("font_color", new Color(1, 1, 1, 0.4f));
		return l;
	}

	private static readonly Regex MentionPattern = new(@"(?<![\w@])@[\w.\-]+", RegexOptions.Compiled);

	private static string Format(LanChat.Line l, bool online)
	{
		var time = DateTimeOffset.FromUnixTimeMilliseconds(l.Time).ToLocalTime().ToString("HH:mm");
		var text = Escape(l.Text);
		if (l.FromId == null) return $"[color=#ffffff80]{time}[/color] [color=#7fc8ff]* {text}[/color]";
		bool me = online ? l.FromId == Online?.Username : l.FromId == Lan?.LocalId;
		var myName = online ? Online?.Username : LanChat.LocalName();
		// @names stand out; a line that pings us gets a highlight behind it.
		text = MentionPattern.Replace(text, m => $"[color=#f3baff]{m.Value}[/color]");
		var line = $"[color=#ffffff80]{time}[/color] [color={(me ? "#9dff9d" : "#ffda75")}]{Escape(l.Name)}[/color]: {text}";
		return !me && OnlineChat.Mentions(l.Text, myName) ? $"[bgcolor=#f3baff30]{line}[/bgcolor]" : line;
	}

	// =====================================================================
	// Tab completes "@na" -> "@name " from the room's people (and everyone online, for public rooms)
	// =====================================================================

	private void OnInputKey(InputEvent e)
	{
		if (e is not InputEventKey { Pressed: true, Keycode: Key.Tab } || _selected == null) return;
		var text = Input.Text[..Input.CaretColumn];
		var m = Regex.Match(text, @"(?<![\w@])@([\w.\-]*)$");
		if (!m.Success) return;
		var prefix = m.Groups[1].Value;
		var match = Candidates().Where(n => n.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
			.OrderBy(n => n.Length).FirstOrDefault();
		Input.AcceptEvent();
		if (match == null) return;
		var rest = Input.Text[Input.CaretColumn..];
		Input.Text = text[..m.Groups[1].Index] + match + " " + rest.TrimStart();
		Input.CaretColumn = m.Groups[1].Index + match.Length + 1;
	}

	private IEnumerable<string> Candidates()
	{
		var room = Find(_selected);
		if (room == null) return Enumerable.Empty<string>();
		var me = IsOnlineKey(_selected) ? Online?.Username : LanChat.LocalName();
		IEnumerable<string> names = room.Members.Values;
		if (room.IsDm) names = new[] { room.With };
		else if (IsOnlineKey(_selected) && room.IsPublic) names = Online.OnlineUsers.Concat(PlayerApi.Friends.Keys);
		else if (!IsOnlineKey(_selected) && room.IsPublic) names = Lan.Peers.Values.Select(p => p.Name);
		// People who spoke recently first-class too.
		names = names.Concat(room.Lines.Where(l => l.FromId != null).Select(l => l.Name).Reverse().Take(50));
		return names.Where(n => !string.IsNullOrEmpty(n) && n != me).Distinct(StringComparer.OrdinalIgnoreCase);
	}

	private static string Escape(string s) => (s ?? "").Replace("[", "[lb]");
}
