using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Multiplayer lounge: lists LAN rooms found via broadcast (with player counts),
/// lets you create a room, refresh, or join by IP when broadcasts are blocked.
/// </summary>
public partial class MultiplayerList : PanelContainer
{
	private static LanNetwork Net => LanNetwork.Instance;
	private const string RoomScenePath = "res://Panels/Multiplayer/MultiplayerListRoom.tscn";

	private VBoxContainer _list;
	private Label _lanLabel, _searching;
	private CheckButton _showInProgress;
	private OptionButton _filter;
	private PackedScene _roomScene;
	private LineEdit _directIp;
	private readonly List<Control> _roomButtons = new();
	private int _seenRooms = -1;
	private bool _leaving;

	public override void _Ready()
	{
		_list = GetNode<VBoxContainer>("VBoxContainer/ScrollContainer/VBoxContainer");
		_lanLabel = _list.GetNode<Label>("LAN");
		_searching = _list.GetNode<Label>("Searching");
		_showInProgress = GetNode<CheckButton>("VBoxContainer/Title/H/Marg/Tools/CheckButton");
		_filter = GetNode<OptionButton>("VBoxContainer/Title/H/Marg/Tools/Type");
		_roomScene = GD.Load<PackedScene>(RoomScenePath);

		// Placeholder room from the editor layout.
		_list.GetNodeOrNull("MultiplayerRoom")?.QueueFree();

		var tools = GetNode<HBoxContainer>("VBoxContainer/Title/H/Marg/Tools");
		tools.GetNode<Button>("Refresh").Pressed += () => { Net.ClearRooms(); _seenRooms = -1; };
		tools.GetNode<Button>("CR2").Pressed += CreateRoom;
		_showInProgress.ButtonPressed = true;
		_showInProgress.Toggled += _ => _seenRooms = -1;
		_filter.ItemSelected += _ => _seenRooms = -1;

		// Direct connect, for networks that block broadcasts.
		_directIp = new LineEdit { PlaceholderText = "IP or IP:port", CustomMinimumSize = new Vector2(170, 0) };
		_directIp.TextSubmitted += _ => JoinDirect();
		var joinButton = LanUi.Style(new Button { Text = "Join IP" });
		joinButton.Pressed += JoinDirect;
		tools.AddChild(_directIp);
		tools.MoveChild(_directIp, 0);
		tools.AddChild(joinButton);
		tools.MoveChild(joinButton, 1);

		// Coming back here means we're not in a room anymore.
		if (Net.State != null && !Net.LocalInMatch) Net.Leave(silent: true);
		Net.StartDiscovery();
		Cursor.CursorVisible = true;
	}

	public override void _ExitTree()
	{
		Net?.StopDiscovery();
	}

	public override void _Process(double delta)
	{
		if (_leaving) return;

		if (Input.IsActionJustPressed("pausemenu"))
		{
			_leaving = true;
			GetNode<SceneTransition>("/root/Transition").Switch("res://Panels/Screens/home_screen.tscn");
			return;
		}

		if (_seenRooms != Net.RoomsVersion)
		{
			_seenRooms = Net.RoomsVersion;
			RebuildRooms();
		}
	}

	private void RebuildRooms()
	{
		foreach (var b in _roomButtons) b.QueueFree();
		_roomButtons.Clear();

		var rooms = Net.Rooms.Values
			.Where(r => _showInProgress.ButtonPressed || r.Beacon.phase == (int)LanPhase.Lobby)
			.Where(r => _filter.Selected != 2) // "Locked": LAN rooms have no passwords
			.OrderByDescending(r => r.Beacon.players).ThenBy(r => r.Beacon.name)
			.ToList();

		_lanLabel.Text = $"({rooms.Count}) LAN Rooms";
		_searching.Visible = rooms.Count == 0;

		int insertAt = _lanLabel.GetIndex() + 1;
		foreach (var room in rooms)
		{
			var button = _roomScene.Instantiate<Button>();
			_list.AddChild(button);
			_list.MoveChild(button, insertAt++);
			_roomButtons.Add(button);
			SetupRoomButton(button, room);
		}
	}

	private void SetupRoomButton(Button button, LanRoomInfo room)
	{
		var b = room.Beacon;
		bool compatible = b.proto == LanNetwork.Protocol;
		bool playing = b.phase == (int)LanPhase.Playing;

		var title = button.GetNode<Label>("MarginContainer/Columns/Title");
		var status = !compatible ? $"Incompatible (v{b.ver})" : playing ? "In progress" : "Open";
		title.Text = $"{b.name}\n{status}  -  host: {b.host}  -  {(string.IsNullOrEmpty(b.map) ? "no beatmap yet" : b.map)}";
		title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		title.VerticalAlignment = VerticalAlignment.Center;
		title.ClipText = true;
		title.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;

		// Player avatars (up to the 6 slots in the layout) + exact count.
		var players = button.GetNode<HBoxContainer>("MarginContainer/Columns/PanelContainer/Players");
		int slot = 0;
		foreach (var child in players.GetChildren())
		{
			if (child is TextureRect avatar) avatar.Visible = slot++ < b.players;
		}
		var count = players.GetNode<Label>("PlayerCount");
		count.Visible = true;
		count.CustomMinimumSize = new Vector2(110, 50);
		count.Text = $"{b.players} player{(b.players == 1 ? "" : "s")}";

		button.TooltipText = $"{room.Address}:{room.Port}";
		button.Disabled = !compatible;
		button.Pressed += () => Net.Join(room.Address, room.Port);
	}

	private void CreateRoom()
	{
		if (Net.Host($"{LanNetwork.LocalName()}'s room"))
		{
			_leaving = true;
			GetNode<SceneTransition>("/root/Transition").Switch(LanNetwork.LobbyScene);
		}
	}

	private void JoinDirect()
	{
		var text = _directIp.Text.Trim();
		if (text.Length == 0) return;
		int port = LanNetwork.DefaultGamePort;
		var host = text;
		int colon = text.LastIndexOf(':');
		if (colon > 0 && int.TryParse(text[(colon + 1)..], out var p))
		{
			host = text[..colon];
			port = p;
		}
		Net.Join(host, port);
	}
}
