using Godot;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// LAN spectate screen: shows the players in the running match live, either all at once in a grid or one at a
/// time (with previous / next). Each tile is a <see cref="LanPlayfieldView"/> drawn locally from the player's
/// chart + inputs (<see cref="LanNetwork.Feeds"/>), so it's real-time and light on the network. Players only
/// stream while someone is watching them; in one-by-one mode only the watched player streams.
/// The music follows the watched player (or the first one still playing in the grid) if you have the map.
///
/// Layout lives in Panels/Screens/MultiplayerSpectate.tscn; tiles are built here (one per player).
/// </summary>
public partial class MultiplayerSpectate : Control
{
	private static LanNetwork Net => LanNetwork.Instance;

	[ExportGroup("Nodes")]
	[Export] public Label TitleLabel { get; set; }
	[Export] public Button BackButton { get; set; }
	[Export] public Button GridButton { get; set; }
	[Export] public Button SingleButton { get; set; }
	[Export] public Control SingleNav { get; set; }
	[Export] public Button PrevButton { get; set; }
	[Export] public Label FocusLabel { get; set; }
	[Export] public Button NextButton { get; set; }
	[Export] public GridContainer Grid { get; set; }
	[Export] public Label StatusLabel { get; set; }

	[ExportGroup("Behaviour")]
	/// <summary>Seconds to stay on the screen after the match ends before going back to the lobby.</summary>
	[Export] public double ReturnDelay { get; set; } = 3;
	/// <summary>A tile says it's lagging if nothing arrived for this many milliseconds.</summary>
	[Export] public int StaleMs { get; set; } = 3000;
	/// <summary>Re-sync the music when it drifts this far from the watched player (seconds).</summary>
	[Export] public float MusicResyncSeconds { get; set; } = 0.12f;

	private class Tile
	{
		public long Id;
		public PanelContainer Root;
		public LanPlayfieldView View;
		public Label Waiting, Name, Stats;
	}

	private readonly Dictionary<long, Tile> _tiles = new();
	private bool _single;
	private long _focus;
	private bool _leaving;
	private double _endedFor;
	private int _seenState = -1;
	private long _musicFor;
	private int _musicIndex = -1;

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
		BackButton.Pressed += Leave;
		GridButton.Pressed += () => SetMode(false);
		SingleButton.Pressed += () => SetMode(true);
		PrevButton.Pressed += () => Step(-1);
		NextButton.Pressed += () => Step(1);
		SetMode(false);
	}

	public override void _Process(double delta)
	{
		if (_leaving) return;
		var s = Net.State;
		if (s == null) { _leaving = true; GetNode<SceneTransition>("/root/Transition").Switch(LanNetwork.LoungeScene); return; }

		if (_seenState != Net.StateVersion)
		{
			_seenState = Net.StateVersion;
			SyncTiles(s);
		}
		UpdateViews();
		SyncMusic();

		// Match over: show it briefly, then head back to the room.
		if (s.Phase != LanPhase.Playing)
		{
			_endedFor += delta;
			StatusLabel.Text = "Match finished! Back to the lobby...";
			StatusLabel.Visible = true;
			if (_endedFor >= ReturnDelay) Leave();
		}
	}

	public override void _UnhandledInput(InputEvent e)
	{
		if (_leaving) return;
		if (e.IsActionPressed("ui_cancel")) { Leave(); AcceptEvent(); }
		else if (_single && e.IsActionPressed("ui_left")) { Step(-1); AcceptEvent(); }
		else if (_single && e.IsActionPressed("ui_right")) { Step(1); AcceptEvent(); }
	}

	// ---- tiles ----

	private List<LanPlayer> Watchable(LanRoomState s) =>
		s.Players.Where(p => p.InMatch && p.Id != Net.LocalId).ToList();

	private void SyncTiles(LanRoomState s)
	{
		var players = Watchable(s);
		TitleLabel.Text = s.Beatmap == null ? "Spectating" : $"Spectating  ·  {s.Beatmap.Display}";

		foreach (var id in _tiles.Keys.Where(id => players.All(p => p.Id != id)).ToList())
		{
			_tiles[id].Root.QueueFree();
			_tiles.Remove(id);
		}
		foreach (var p in players)
		{
			if (!_tiles.TryGetValue(p.Id, out var t)) _tiles[p.Id] = t = MakeTile(p.Id);
			t.Name.Text = p.Name;
			t.Stats.Text = p.Finished ? (p.Quit ? "QUIT" : $"DONE  {p.Score:N0}") : $"{p.Score:N0}   {p.Accuracy:P2}";
			t.View.Finished = p.Finished;
			t.View.Speed = s.CurrentSpeed > 0 ? s.CurrentSpeed : 1f;
		}
		// Keep tile order = player order.
		for (int i = 0; i < players.Count; i++) Grid.MoveChild(_tiles[players[i].Id].Root, i);

		if (players.Count > 0 && players.All(p => p.Id != _focus)) _focus = players[0].Id;
		StatusLabel.Visible = players.Count == 0 && s.Phase == LanPhase.Playing;
		if (StatusLabel.Visible) StatusLabel.Text = "Nobody else is playing right now.";
		ApplyLayout(players);
	}

	private Tile MakeTile(long id)
	{
		var t = new Tile { Id = id };
		t.Root = new PanelContainer
		{
			ThemeTypeVariation = "LobbyCard",
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			MouseFilter = MouseFilterEnum.Stop,
		};
		var v = new VBoxContainer();
		t.Root.AddChild(v);

		var stack = new MarginContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
		v.AddChild(stack);
		t.View = new LanPlayfieldView
		{
			SizeFlagsVertical = SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(120, 160),
		};
		stack.AddChild(t.View);
		t.Waiting = new Label
		{
			Text = "Waiting for the player...",
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			ThemeTypeVariation = "LobbySubtle",
		};
		stack.AddChild(t.Waiting);

		var row = new HBoxContainer();
		v.AddChild(row);
		t.Name = new Label { SizeFlagsHorizontal = SizeFlags.ExpandFill, ClipText = true, TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis };
		t.Stats = new Label { HorizontalAlignment = HorizontalAlignment.Right };
		row.AddChild(t.Name);
		row.AddChild(t.Stats);

		// Click a tile in the grid to watch just that player.
		t.Root.GuiInput += e =>
		{
			if (!_single && e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
			{
				_focus = id;
				SetMode(true);
			}
		};
		Grid.AddChild(t.Root);
		return t;
	}

	private void UpdateViews()
	{
		ulong now = Time.GetTicksMsec();
		foreach (var t in _tiles.Values)
		{
			Net.Feeds.TryGetValue(t.Id, out var f);
			t.View.Feed = f;
			bool live = f is { HasChart: true, HasSample: true } && now - f.SampleAtMs < (ulong)StaleMs;
			t.Waiting.Visible = !live && !t.View.Finished;
			if (t.Waiting.Visible) t.Waiting.Text = f?.HasSample == true ? "Lagging..." : "Waiting for the player...";
		}
	}

	/// <summary>Plays the song in time with the player you're following, if you have their map.</summary>
	private void SyncMusic()
	{
		var s = Net.State;
		var players = Watchable(s);
		var follow = _single ? players.FirstOrDefault(p => p.Id == _focus) : players.FirstOrDefault(p => !p.Finished);
		var player = AudioPlayer.Instance;
		if (follow == null || !_tiles.TryGetValue(follow.Id, out var tile) || tile.View.ShownMs == double.MinValue || tile.View.Finished)
		{
			LanUi.KeepNormalSpeed();
			return;
		}

		if (_musicFor != follow.Id)
		{
			_musicFor = follow.Id;
			_musicIndex = Net.FindLocalBeatmap(Net.ChartFor(follow));
			if (_musicIndex >= 0 && SettingsOperator.SessionConfig.SongID != _musicIndex)
				GetNode<SettingsOperator>("/root/SettingsOperator").SelectSongID(_musicIndex);
			SettingsOperator.loopaudio = false;
		}
		if (_musicIndex < 0 || player.Stream == null) return; // no map locally: watch without music

		float speed = s.CurrentSpeed > 0 ? s.CurrentSpeed : 1f;
		if (!Mathf.IsEqualApprox(player.PitchScale, speed)) player.PitchScale = speed;
		double target = tile.View.ShownMs / 1000.0;
		if (target < 0 || target > player.Stream.GetLength())
		{
			if (player.Playing) player.Stop(); // lead-in before the song, or past its end
			return;
		}
		if (!player.Playing) player.Play((float)target);
		else if (Mathf.Abs(player.GetPlaybackPosition() - target) > MusicResyncSeconds) player.Seek((float)target);
	}

	// ---- modes ----

	private void SetMode(bool single)
	{
		_single = single;
		GridButton.SetPressedNoSignal(!single);
		SingleButton.SetPressedNoSignal(single);
		SingleNav.Visible = single;
		ApplyLayout(Watchable(Net.State));
	}

	private void Step(int dir)
	{
		var players = Watchable(Net.State);
		if (players.Count == 0) return;
		int i = players.FindIndex(p => p.Id == _focus);
		_focus = players[((i < 0 ? 0 : i) + dir + players.Count) % players.Count].Id;
		ApplyLayout(players);
	}

	private void ApplyLayout(List<LanPlayer> players)
	{
		if (_single)
		{
			Grid.Columns = 1;
			foreach (var t in _tiles.Values) t.Root.Visible = t.Id == _focus;
			FocusLabel.Text = players.FirstOrDefault(p => p.Id == _focus)?.Name ?? "-";
			PrevButton.Disabled = NextButton.Disabled = players.Count < 2;
			Net.SetWatchTarget(_focus != 0 ? _focus : 0); // only the watched player streams
		}
		else
		{
			int n = Mathf.Max(1, players.Count);
			Grid.Columns = n <= 1 ? 1 : n <= 4 ? 2 : n <= 9 ? 3 : 4;
			foreach (var t in _tiles.Values) t.Root.Visible = true;
			Net.SetWatchTarget(0);
		}
	}

	private void Leave()
	{
		if (_leaving) return;
		_leaving = true;
		SettingsOperator.loopaudio = true;
		Net?.SetWatchTarget(-1);
		GetNode<SceneTransition>("/root/Transition").Switch(Net?.ReturnScene ?? LanNetwork.LoungeScene);
	}
}
