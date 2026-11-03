using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// LAN room screen: players (left), beatmaps (center), chat (right),
/// with the "time till play" countdown and ready-up button in the header.
/// UI is built in code; all room data comes from <see cref="LanNetwork"/>.
/// </summary>
public partial class MultiplayerLobby : Control
{
	private static LanNetwork Net => LanNetwork.Instance;

	private static readonly Color PanelColor = new(0.06f, 0.07f, 0.1f, 0.82f);
	private static readonly Color Accent = new("#7fc8ff");
	private static readonly Color ReadyColor = new("#5be37d");
	private static readonly Color NotReadyColor = new("#9aa3b5");
	private static readonly Color NoMapColor = new("#ffa94d");
	private static readonly Color PlayingColor = new("#7fc8ff");

	private Label _roomTitle, _playerCount, _timerLabel, _playersHeader, _mapInfo, _mapStatus, _hostTimerLabel;
	private Button _readyButton, _leaveButton, _startNowButton, _timerMinus, _timerPlus, _getMapButton;
	private VBoxContainer _playerRows;
	private ItemList _mapList;
	private LineEdit _search, _chatInput;
	private RichTextLabel _chat;
	private ProgressBar _transferBar;
	private HBoxContainer _hostControls;

	private readonly List<int> _filtered = new();
	private int _seenState = -1, _seenChat = -1, _seenBeatmapCount = -1;
	private string _lastTimerText;
	private bool _leaving;
	private bool _ignoreSelection;

	public override void _Ready()
	{
		if (Net?.State == null)
		{
			_leaving = true;
			Callable.From(() => GetNode<SceneTransition>("/root/Transition").Switch(LanNetwork.LoungeScene)).CallDeferred();
			return;
		}
		GetNode<SettingsOperator>("/root/SettingsOperator").toppaneltoggle(true);
		Cursor.CursorVisible = true;
		BuildUi();
	}

	public override void _Process(double delta)
	{
		if (_leaving) return;
		if (Net.State == null)
		{
			// Host closed the room (LanNetwork already notified the player).
			_leaving = true;
			GetNode<SceneTransition>("/root/Transition").Switch(LanNetwork.LoungeScene);
			return;
		}

		if (_seenBeatmapCount != SettingsOperator.Beatmaps.Count)
		{
			_seenBeatmapCount = SettingsOperator.Beatmaps.Count;
			RebuildMapList();
			_seenState = -1; // re-highlight the selection
		}
		if (_seenState != Net.StateVersion)
		{
			_seenState = Net.StateVersion;
			RefreshRoom();
		}
		if (_seenChat != Net.ChatVersion)
		{
			_seenChat = Net.ChatVersion;
			_chat.Clear();
			_chat.AppendText(string.Join("\n", Net.ChatLog));
		}

		UpdateTimerLabel();

		bool receiving = Net.TransferProgress >= 0;
		_transferBar.Visible = receiving;
		if (receiving) _transferBar.Value = Net.TransferProgress * 100;
	}

	// =====================================================================
	// Refresh from room state
	// =====================================================================

	private void RefreshRoom()
	{
		var s = Net.State;
		var me = Net.LocalPlayer;
		bool lobby = s.Phase == LanPhase.Lobby;

		_roomTitle.Text = s.Name;
		_playerCount.Text = $"{s.Players.Count} player{(s.Players.Count == 1 ? "" : "s")} - hosted by {s.HostName}";
		_playersHeader.Text = $"Players ({s.Players.Count})";

		// Header buttons
		_hostControls.Visible = Net.IsHost;
		_hostTimerLabel.Text = $"Timer: {FormatTime(s.LobbyTimer)}";
		_startNowButton.Disabled = !lobby || !s.CountdownActive;
		_readyButton.Disabled = me == null || !lobby || !me.HasMap;
		_readyButton.SetPressedNoSignal(me?.Ready ?? false);
		_readyButton.Text = !lobby ? "Match in progress"
			: me != null && !me.HasMap ? "Need beatmap"
			: me?.Ready == true ? "Ready!" : "Ready up";

		RebuildPlayerRows(s);

		// Selected beatmap
		if (s.Beatmap == null)
		{
			_mapInfo.Text = Net.IsHost ? "Pick a beatmap from the list below." : "Waiting for the host to pick a beatmap...";
			_mapStatus.Text = "";
			_getMapButton.Visible = false;
		}
		else
		{
			var b = s.Beatmap;
			_mapInfo.Text = $"{b.Artist} - {b.Title}\n[{b.Version}] mapped by {b.Mapper}   Lv. {b.Levelrating:N0}   {FormatTime(b.LengthMs / 1000f)}";
			bool has = Net.IsHost || Net.FindLocalBeatmap(b) >= 0;
			_mapStatus.Text = has ? "" : "You don't have this beatmap.";
			_mapStatus.AddThemeColorOverride("font_color", NoMapColor);
			_getMapButton.Visible = !has && Net.TransferProgress < 0;
		}

		_mapList.SelectMode = ItemList.SelectModeEnum.Single;
		HighlightSelectedMap();
	}

	private void RebuildPlayerRows(LanRoomState s)
	{
		foreach (var child in _playerRows.GetChildren()) child.QueueFree();

		// During a match, show players ordered by live score; otherwise ready players first.
		var ordered = s.Phase == LanPhase.Playing
			? s.Players.OrderByDescending(p => p.InMatch).ThenByDescending(p => p.Score)
			: s.Players.OrderByDescending(p => p.Id == 1).ThenByDescending(p => p.Ready).ThenBy(p => p.Name);

		foreach (var p in ordered)
		{
			var row = new PanelContainer();
			row.AddThemeStyleboxOverride("panel", Box(p.Id == Net.LocalId ? new Color(0.2f, 0.25f, 0.35f, 0.9f) : new Color(0.12f, 0.14f, 0.19f, 0.9f), 6, 8));
			var h = new HBoxContainer();
			h.AddThemeConstantOverride("separation", 8);
			row.AddChild(h);

			string status; Color color;
			if (s.Phase == LanPhase.Playing && p.InMatch) { status = p.Finished ? (p.Quit ? "QUIT" : "DONE") : "PLAYING"; color = PlayingColor; }
			else if (!p.HasMap && s.Beatmap != null) { status = "NO MAP"; color = NoMapColor; }
			else if (p.Ready) { status = "READY"; color = ReadyColor; }
			else { status = s.Phase == LanPhase.Playing ? "WAITING" : "NOT READY"; color = NotReadyColor; }

			var statusLabel = new Label { Text = status, CustomMinimumSize = new Vector2(84, 0) };
			statusLabel.AddThemeColorOverride("font_color", color);
			statusLabel.AddThemeFontSizeOverride("font_size", 13);
			h.AddChild(statusLabel);

			var nameBox = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
			nameBox.AddThemeConstantOverride("separation", 0);
			var name = new Label { Text = p.Name + (p.Id == 1 ? "  (host)" : ""), ClipText = true, TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis };
			nameBox.AddChild(name);
			if (s.Phase == LanPhase.Playing && p.InMatch)
			{
				var score = new Label { Text = $"{p.Score:N0}   {p.Accuracy:P2}   {p.Combo}x" };
				score.AddThemeFontSizeOverride("font_size", 12);
				score.AddThemeColorOverride("font_color", NotReadyColor);
				nameBox.AddChild(score);
			}
			h.AddChild(nameBox);
			_playerRows.AddChild(row);
		}

		if (s.LastResults.Count > 0)
		{
			var header = new Label { Text = "Last match" };
			header.AddThemeColorOverride("font_color", Accent);
			_playerRows.AddChild(new HSeparator());
			_playerRows.AddChild(header);
			foreach (var r in s.LastResults)
			{
				var l = new Label
				{
					Text = r.Quit ? $"#{r.Rank}  {r.Name}  -  quit" : $"#{r.Rank}  {r.Name}  -  {r.Score:N0}  ({r.Accuracy:P2})",
					ClipText = true,
					TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
				};
				l.AddThemeFontSizeOverride("font_size", 13);
				_playerRows.AddChild(l);
			}
		}
	}

	private void UpdateTimerLabel()
	{
		var s = Net.State;
		string text;
		if (s.Phase == LanPhase.Playing) text = "Match in progress";
		else if (s.CountdownActive) text = $"Time till play: {FormatTime(Net.TimeLeftNow)}";
		else text = "Waiting for a beatmap";
		if (text == _lastTimerText) return;
		_lastTimerText = text;
		_timerLabel.Text = text;
		bool soon = s.CountdownActive && Net.TimeLeftNow <= LanNetwork.AllReadyCountdown;
		_timerLabel.AddThemeColorOverride("font_color", soon ? ReadyColor : Colors.White);
	}

	// =====================================================================
	// Beatmap list
	// =====================================================================

	private void RebuildMapList()
	{
		var query = _search.Text.Trim();
		_filtered.Clear();
		_mapList.Clear();
		var maps = SettingsOperator.Beatmaps;
		for (int i = 0; i < maps.Count; i++)
		{
			var b = maps[i];
			var label = $"{b.Artist} - {b.Title} [{b.Version}]";
			if (query.Length > 0 && !(label + " " + b.Mapper).Contains(query, StringComparison.OrdinalIgnoreCase)) continue;
			_filtered.Add(i);
			int item = _mapList.AddItem($"{label}   Lv. {b.Levelrating:N0}");
			_mapList.SetItemTooltip(item, $"{label}\nMapped by {b.Mapper}");
		}
		HighlightSelectedMap();
	}

	private void HighlightSelectedMap()
	{
		_ignoreSelection = true;
		_mapList.DeselectAll();
		int local = Net.State?.Beatmap == null ? -1 : (Net.IsHost ? SettingsOperator.SessionConfig.SongID : Net.FindLocalBeatmap(Net.State.Beatmap));
		int item = local < 0 ? -1 : _filtered.IndexOf(local);
		if (item >= 0)
		{
			_mapList.Select(item);
			_mapList.EnsureCurrentIsVisible();
		}
		_ignoreSelection = false;
	}

	private void OnMapSelected(long item)
	{
		if (_ignoreSelection) return;
		if (Net.IsHost && Net.State.Phase == LanPhase.Lobby && item >= 0 && item < _filtered.Count)
			Net.HostSelectBeatmap(_filtered[(int)item]);
		else
			HighlightSelectedMap(); // only the host picks; snap back to the room's beatmap
	}

	// =====================================================================
	// UI construction
	// =====================================================================

	private void BuildUi()
	{
		// Background: the selected beatmap's art, like the other menus.
		var bg = GD.Load<PackedScene>("res://Panels/Overlays/beatmap_background.tscn")?.Instantiate<Control>();
		if (bg != null)
		{
			AddChild(bg);
			bg.SelfModulate = new Color(0.45f, 0.45f, 0.45f);
		}

		var margin = new MarginContainer();
		margin.SetAnchorsPreset(LayoutPreset.FullRect);
		margin.AddThemeConstantOverride("margin_top", 64); // clear the top panel
		margin.AddThemeConstantOverride("margin_left", 16);
		margin.AddThemeConstantOverride("margin_right", 16);
		margin.AddThemeConstantOverride("margin_bottom", 16);
		AddChild(margin);

		var root = new VBoxContainer();
		root.AddThemeConstantOverride("separation", 12);
		margin.AddChild(root);

		root.AddChild(BuildHeader());

		var columns = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
		columns.AddThemeConstantOverride("separation", 12);
		root.AddChild(columns);
		columns.AddChild(BuildPlayersPanel());
		columns.AddChild(BuildMapsPanel());
		columns.AddChild(BuildChatPanel());
	}

	private Control BuildHeader()
	{
		var panel = Panel();
		var h = new HBoxContainer();
		h.AddThemeConstantOverride("separation", 12);
		panel.AddChild(h);

		var titles = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		titles.AddThemeConstantOverride("separation", 0);
		_roomTitle = new Label { ClipText = true, TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis };
		_roomTitle.AddThemeFontSizeOverride("font_size", 24);
		_playerCount = new Label();
		_playerCount.AddThemeColorOverride("font_color", NotReadyColor);
		titles.AddChild(_roomTitle);
		titles.AddChild(_playerCount);
		h.AddChild(titles);

		_timerLabel = new Label { VerticalAlignment = VerticalAlignment.Center, CustomMinimumSize = new Vector2(220, 0), HorizontalAlignment = HorizontalAlignment.Center };
		_timerLabel.AddThemeFontSizeOverride("font_size", 20);
		h.AddChild(_timerLabel);

		_hostControls = new HBoxContainer();
		_hostControls.AddThemeConstantOverride("separation", 6);
		_timerMinus = SmallButton("-15s", () => Net.HostAdjustTimer(-15));
		_hostTimerLabel = new Label { VerticalAlignment = VerticalAlignment.Center };
		_timerPlus = SmallButton("+15s", () => Net.HostAdjustTimer(15));
		_startNowButton = SmallButton("Start now", () => Net.HostStartNow());
		_hostControls.AddChild(_timerMinus);
		_hostControls.AddChild(_hostTimerLabel);
		_hostControls.AddChild(_timerPlus);
		_hostControls.AddChild(_startNowButton);
		h.AddChild(_hostControls);

		_readyButton = LanUi.Style(new Button { ToggleMode = true, CustomMinimumSize = new Vector2(150, 44) });
		_readyButton.Toggled += pressed => Net.SetReady(pressed);
		h.AddChild(_readyButton);

		_leaveButton = LanUi.Style(new Button { Text = "Leave", CustomMinimumSize = new Vector2(90, 44) });
		_leaveButton.Pressed += () =>
		{
			_leaving = true;
			Net.Leave();
			GetNode<SceneTransition>("/root/Transition").Switch(LanNetwork.LoungeScene);
		};
		h.AddChild(_leaveButton);
		return panel;
	}

	private Control BuildPlayersPanel()
	{
		var panel = Panel();
		panel.CustomMinimumSize = new Vector2(280, 0);
		var v = new VBoxContainer();
		v.AddThemeConstantOverride("separation", 8);
		panel.AddChild(v);

		_playersHeader = Header("Players");
		v.AddChild(_playersHeader);

		var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
		_playerRows = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		_playerRows.AddThemeConstantOverride("separation", 6);
		scroll.AddChild(_playerRows);
		v.AddChild(scroll);
		return panel;
	}

	private Control BuildMapsPanel()
	{
		var panel = Panel();
		panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		panel.SizeFlagsStretchRatio = 1.6f;
		var v = new VBoxContainer();
		v.AddThemeConstantOverride("separation", 8);
		panel.AddChild(v);

		v.AddChild(Header("Beatmap"));
		_mapInfo = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
		v.AddChild(_mapInfo);

		var statusRow = new HBoxContainer();
		statusRow.AddThemeConstantOverride("separation", 8);
		_mapStatus = new Label { VerticalAlignment = VerticalAlignment.Center };
		_getMapButton = SmallButton("Get it from the host", () => { Net.RequestBeatmapFromHost(); _getMapButton.Visible = false; });
		_transferBar = new ProgressBar { CustomMinimumSize = new Vector2(200, 20), SizeFlagsHorizontal = SizeFlags.ExpandFill, Visible = false };
		statusRow.AddChild(_mapStatus);
		statusRow.AddChild(_getMapButton);
		statusRow.AddChild(_transferBar);
		v.AddChild(statusRow);

		_search = new LineEdit { PlaceholderText = Net.IsHost ? "Search your beatmaps..." : "Search beatmaps (the host picks what to play)", ClearButtonEnabled = true };
		_search.TextChanged += _ => RebuildMapList();
		v.AddChild(_search);

		_mapList = new ItemList { SizeFlagsVertical = SizeFlags.ExpandFill, AllowReselect = false, AutoHeight = false };
		_mapList.ItemSelected += OnMapSelected;
		v.AddChild(_mapList);
		return panel;
	}

	private Control BuildChatPanel()
	{
		var panel = Panel();
		panel.CustomMinimumSize = new Vector2(320, 0);
		var v = new VBoxContainer();
		v.AddThemeConstantOverride("separation", 8);
		panel.AddChild(v);

		v.AddChild(Header("Chat"));
		_chat = new RichTextLabel { BbcodeEnabled = true, ScrollFollowing = true, SizeFlagsVertical = SizeFlags.ExpandFill, SelectionEnabled = true };
		v.AddChild(_chat);

		_chatInput = new LineEdit { PlaceholderText = $"Chatting as {LanNetwork.LocalName()}", MaxLength = 300 };
		_chatInput.TextSubmitted += text =>
		{
			Net.SendChat(text);
			_chatInput.Clear();
		};
		v.AddChild(_chatInput);
		return panel;
	}

	// ---- small UI helpers ----

	private static PanelContainer Panel()
	{
		var p = new PanelContainer();
		p.AddThemeStyleboxOverride("panel", Box(PanelColor, 10, 12));
		return p;
	}

	private static StyleBoxFlat Box(Color color, int radius, int padding)
	{
		var sb = new StyleBoxFlat { BgColor = color };
		sb.SetCornerRadiusAll(radius);
		sb.SetContentMarginAll(padding);
		return sb;
	}

	private static Label Header(string text)
	{
		var l = new Label { Text = text };
		l.AddThemeFontSizeOverride("font_size", 18);
		l.AddThemeColorOverride("font_color", Accent);
		return l;
	}

	private static Button SmallButton(string text, Action onPressed)
	{
		var b = LanUi.Style(new Button { Text = text, CustomMinimumSize = new Vector2(0, 36) });
		b.Pressed += onPressed;
		return b;
	}

	private static string FormatTime(double seconds)
	{
		var t = TimeSpan.FromSeconds(Math.Max(0, Math.Ceiling(seconds)));
		return $"{(int)t.TotalMinutes}:{t.Seconds:00}";
	}
}
