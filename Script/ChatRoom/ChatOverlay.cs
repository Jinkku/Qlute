using Godot;
using System;
using System.Linq;

/// <summary>
/// The chat overlay opened from the top panel. Shows the LAN chat (<see cref="LanChat"/>):
/// rooms + invites on the left, the selected room in the middle, people on the network on the right.
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

	/// <summary>Remembered across open/close of the overlay.</summary>
	private static string _selected = LanChat.PublicRoomId;
	private int _shownVersion = -1;
	private string _shownRoom;

	public override void _Ready()
	{
		NewRoomButton.ThemeTypeVariation = ButtonVariation;
		LeaveButton.ThemeTypeVariation = ButtonVariation;
		NewRoomButton.Pressed += () =>
		{
			var c = LanChat.Instance;
			if (c == null) return;
			_selected = c.CreatePrivateRoom(null);
			Refresh(force: true);
		};
		LeaveButton.Pressed += () =>
		{
			LanChat.Instance?.LeaveRoom(_selected);
			_selected = LanChat.PublicRoomId;
			Refresh(force: true);
		};
		TurnOnButton.ThemeTypeVariation = ButtonVariation;
		TurnOnButton.Pressed += () => LanChat.Instance?.SetEnabled(true);
		PowerButton.Toggled += on => LanChat.Instance?.SetEnabled(on);
		NameEdit.Text = LanChat.Nickname;
		NameEdit.TextSubmitted += text => { SaveName(text); NameEdit.ReleaseFocus(); };
		NameEdit.FocusExited += () => SaveName(NameEdit.Text);
		Input.TextSubmitted += text =>
		{
			Input.Clear();
			LanChat.Instance?.Send(_selected, text);
		};
		Refresh(force: true);
	}

	public override void _Process(double delta)
	{
		var c = LanChat.Instance;
		if (c == null) return;
		if (c.Version != _shownVersion || _shownRoom != _selected) Refresh(force: false);
	}

	private void Refresh(bool force)
	{
		var c = LanChat.Instance;
		if (c == null) return;
		_shownVersion = c.Version;
		_shownRoom = _selected;
		RefreshName();
		PowerButton.SetPressedNoSignal(LanChat.Enabled);
		TurnOnButton.Visible = !LanChat.Enabled;
		NewRoomButton.Disabled = !c.Running;
		if (!c.Running)
		{
			if (LanChat.Enabled)
			{
				Status.Text = "LAN chat is unavailable";
				Empty.Text = $"Couldn't open UDP port {LanChat.PresencePort} (in use or blocked by a firewall).";
			}
			else
			{
				Status.Text = "You're invisible on this network";
				Empty.Text = "LAN chat is off. Nobody on the network can see you, and you can't see them.";
			}
			foreach (var child in RoomList.GetChildren()) child.QueueFree();
			foreach (var child in PeopleList.GetChildren()) child.QueueFree();
			PeopleTitle.Text = "";
			ShowEmpty(true);
			return;
		}
		Empty.Text = "Nobody else is playing on this network right now.\nThe LAN chat shows up as soon as someone starts the game.";

		var rooms = c.VisibleRooms().ToList();
		if (rooms.All(r => r.Id != _selected)) _selected = rooms.FirstOrDefault()?.Id ?? LanChat.PublicRoomId;
		bool hasRoom = rooms.Any(r => r.Id == _selected);
		if (hasRoom) c.MarkRead(_selected);
		_shownVersion = c.Version;

		Status.Text = c.Peers.Count switch
		{
			0 => "Nobody else on this network yet",
			1 => "1 other player on this network",
			var n => $"{n} other players on this network",
		};

		BuildRoomList(c, rooms);
		BuildPeople(c, hasRoom ? c.Rooms[_selected] : null);

		ShowEmpty(!hasRoom);
		if (!hasRoom) return;
		var room = c.Rooms[_selected];
		Input.PlaceholderText = $"Chatting as {LanChat.LocalName()} in {room.Title}";
		LeaveButton.Visible = !room.IsPublic;
		Log.Clear();
		Log.AppendText(string.Join("\n", room.Lines.Select(Format)));
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
		LanChat.Instance?.SetNickname(text);
	}

	private void ShowEmpty(bool empty)
	{
		Empty.Visible = empty;
		Log.Visible = !empty;
		Input.Visible = !empty;
		if (empty) LeaveButton.Visible = false;
	}

	private void BuildRoomList(LanChat c, System.Collections.Generic.List<LanChat.Room> rooms)
	{
		foreach (var child in RoomList.GetChildren()) child.QueueFree();

		if (rooms.Any(r => r.IsPublic)) RoomList.AddChild(Header("Public"));
		foreach (var r in rooms.Where(r => r.IsPublic)) RoomList.AddChild(RoomButton(r));

		if (rooms.Any(r => !r.IsPublic)) RoomList.AddChild(Header("Private"));
		foreach (var r in rooms.Where(r => !r.IsPublic)) RoomList.AddChild(RoomButton(r));

		if (c.Invites.Count > 0) RoomList.AddChild(Header("Invites"));
		foreach (var inv in c.Invites.ToList())
		{
			var box = new VBoxContainer();
			box.AddChild(new Label
			{
				Text = $"{inv.Title}\nfrom {inv.FromName}",
				AutowrapMode = TextServer.AutowrapMode.WordSmart,
			});
			var row = new HBoxContainer();
			var accept = new Button { Text = "Join", SizeFlagsHorizontal = SizeFlags.ExpandFill, ThemeTypeVariation = ButtonVariation };
			var decline = new Button { Text = "Decline", SizeFlagsHorizontal = SizeFlags.ExpandFill, ThemeTypeVariation = ButtonVariation };
			accept.Pressed += () => { c.AcceptInvite(inv); _selected = inv.RoomId; Refresh(true); };
			decline.Pressed += () => { c.DeclineInvite(inv); Refresh(true); };
			row.AddChild(accept);
			row.AddChild(decline);
			box.AddChild(row);
			RoomList.AddChild(box);
		}
	}

	private Button RoomButton(LanChat.Room r)
	{
		var b = new Button
		{
			Text = r.Unread > 0 && r.Id != _selected ? $"{r.Title}  ({r.Unread})" : r.Title,
			Alignment = HorizontalAlignment.Left,
			ToggleMode = true,
			ButtonPressed = r.Id == _selected,
			ThemeTypeVariation = ButtonVariation,
			TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
		};
		b.Pressed += () => { _selected = r.Id; Refresh(true); };
		return b;
	}

	private void BuildPeople(LanChat c, LanChat.Room room)
	{
		foreach (var child in PeopleList.GetChildren()) child.QueueFree();

		if (room != null && !room.IsPublic)
		{
			PeopleTitle.Text = $"In {room.Title}";
			foreach (var (id, name) in room.Members.OrderBy(m => m.Value))
			{
				var you = id == c.LocalId ? " (you)" : "";
				var away = c.IsOnline(id) ? "" : " - offline";
				PeopleList.AddChild(new Label { Text = $"• {name}{you}{away}", TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis });
			}
			var others = c.Peers.Values.Where(p => !room.Members.ContainsKey(p.Id)).OrderBy(p => p.Name).ToList();
			PeopleList.AddChild(Header(others.Count > 0 ? "Invite from the LAN" : "No one else to invite"));
			foreach (var p in others)
			{
				var row = new HBoxContainer();
				row.AddChild(new Label { Text = p.Name, SizeFlagsHorizontal = SizeFlags.ExpandFill, TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis, TooltipText = PeerTooltip(p), MouseFilter = MouseFilterEnum.Pass });
				bool sent = room.Invited.Contains(p.Id);
				var invite = new Button { Text = sent ? "Sent" : "Invite", Disabled = sent, ThemeTypeVariation = ButtonVariation };
				var peerId = p.Id;
				var roomId = room.Id;
				invite.Pressed += () => { c.InvitePeer(roomId, peerId); invite.Disabled = true; invite.Text = "Sent"; };
				row.AddChild(invite);
				PeopleList.AddChild(row);
			}
			return;
		}

		PeopleTitle.Text = "On this network";
		PeopleList.AddChild(new Label { Text = $"• {LanChat.LocalName()} (you)" });
		foreach (var p in c.Peers.Values.OrderBy(p => p.Name))
			PeopleList.AddChild(new Label { Text = $"• {p.Name}", TooltipText = PeerTooltip(p), MouseFilter = MouseFilterEnum.Pass, TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis });
	}

	private static string PeerTooltip(LanChat.Peer p) => $"{p.EndPoint?.Address} - game {p.GameVersion}";

	private static Label Header(string text)
	{
		var l = new Label { Text = text };
		l.AddThemeColorOverride("font_color", new Color(1, 1, 1, 0.55f));
		return l;
	}

	private static string Format(LanChat.Line l)
	{
		var time = DateTimeOffset.FromUnixTimeMilliseconds(l.Time).ToLocalTime().ToString("HH:mm");
		var text = Escape(l.Text);
		if (l.FromId == null) return $"[color=#ffffff80]{time}[/color] [color=#7fc8ff]* {text}[/color]";
		bool me = l.FromId == LanChat.Instance?.LocalId;
		return $"[color=#ffffff80]{time}[/color] [color={(me ? "#9dff9d" : "#ffda75")}]{Escape(l.Name)}[/color]: {text}";
	}

	private static string Escape(string s) => (s ?? "").Replace("[", "[lb]");
}
