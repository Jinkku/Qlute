using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public class InfoBox
{
	public static void Text(Node Node,string value)
	{
		Node.Set("Text", value);
	}
}

public partial class SongSelect : Control
{
	// Called when the node enters the scene tree for the first time.
	public SettingsOperator SettingsOperator { get; set; }
	public Label SongTitle { get; set; }
	public Label ExSongInfo { get; set; }
	private PanelContainer LevelRating { get; set; }
	private PanelContainer RankStatus { get; set; }
	private Label NoBeatmap { get; set; }
	public PanelContainer Songpp { get; set; }
	public Label Debugtext { get; set; }
	public PanelContainer SongBPM { get; set; }
	public PanelContainer SongLen { get; set; }
	public TextureButton StartButton { get; set; }
	public List<Button> SongEntry = new List<Button>();
	public int SongETick { get; set; }
	public Texture2D ImageCache { get; set; }
	public string ImageURL { get; set; }
	public Control ModScreen { get; set; }
	public PanelContainer ModeScreen { get; set; }
	public Tween ModScreen_Tween { get; set; }
	public Tween ModeScreen_Tween { get; set; }
	public ScrollBar scrollBar { get; set; }
	public PanelContainer Info { get; set; }
	public Tween scrolltween { get; private set; }
	private int scrollvelocity { get; set; }
	private bool AnimationSong { get; set; }
	private int SongLoaded { get; set; }
	private bool ContextMenuActive { get; set; }
	private Tween ContextMenuAni { get; set; }
	private BeatmapContextMenu ContextMenu { get; set; }
	private Button LeaderboardLocal { get; set; }
	private Button LeaderboardGlobal { get; set; }
	private Button LeaderboardDetails { get; set; }
	private PanelContainer DetailsExtended { get; set; }
	private ScrollContainer Leaderboardinfo { get; set; }
	private bool Update { get; set; }

	private int OldSongID { get; set; }

	int startposition = 0;
	int scrolloldvalue { get; set; }
	private List<int> FilteredIndices { get; set; } = new List<int>();

	/// <summary>Where each SongID sits in <see cref="FilteredIndices"/> (the scroll position of its card).</summary>
	private readonly Dictionary<int, int> _filterPos = new();
	private int PosOf(int songId) => _filterPos.TryGetValue(songId, out var p) ? p : -1;

	/// <summary>The list to show: search text + filters + sort (SongFilter). Empty = nothing matches.</summary>
	private void RebuildFilter(string query)
	{
		bool lanFilter = LanNetwork.Picking != LanNetwork.PickMode.None && LanNetwork.Instance != null;
		var list = SongFilter.Apply(query, lanFilter ? i => LanNetwork.Instance.PickFilter(i) : null); // LAN: e.g. only the up-next set
		FilteredIndices.Clear();
		FilteredIndices.AddRange(list);
		_filterPos.Clear();
		for (int i = 0; i < FilteredIndices.Count; i++) _filterPos[FilteredIndices[i]] = i;
	}

	/// <summary>
	/// Filters the list again without reloading the scene: cards for beatmaps that are still listed stay (just move),
	/// and the selected song keeps playing if it's still in the list. Used by search, filters and new imports.
	/// </summary>
	private void ApplyFilters(bool rebuildCards = false)
	{
		RebuildFilter(Searchtext);
		_lastStartIndex = int.MinValue;
		_lastEndIndex = int.MinValue;
		scrollBar.MaxValue = Math.Max(1, FilteredIndices.Count);
		int current = PosOf(SettingsOperator.SessionConfig.SongID);
		if (current < 0 && FilteredIndices.Count > 0)
		{
			current = 0; // the selected song was filtered out: pick the first one shown
			SettingsOperator.SelectSongID(FilteredIndices[0]);
		}
		scrolltween?.Kill();
		scrollBar.Value = Math.Max(0, current);
		ScrollSongs();
		_carousel?.SetData(FilteredIndices);
		UpdateFilterButton();
		FillHeader();
	}

	private void _valuechangedscroll(float value)
	{
		// Part of the loading Beatmaps
		SongETick = 0;
		startposition = ((int)GetViewportRect().Size.Y / 2) - 208;
		ScrollSongs();
	}

	private void scrollmode(int ement = 0, int exactvalue = 0)
	{
		OldSongID = SettingsOperator.SessionConfig.SongID;
		double value = ement != 0 ? scrollBar.Value + ement : exactvalue;
		scrolltween?.Kill();
		scrolltween = CreateTween();
		scrolltween.BindNode(scrollBar);
		scrolltween.TweenProperty(scrollBar, "value", value, 0.5f).SetEase(Tween.EaseType.Out)
			.SetTrans(Tween.TransitionType.Cubic);
		scrolltween.Play();
	}

	private Vector2 WindowSize { get; set; }
	private Vector2 WindowSizeCenter { get; set; }

	public void _res_resize()
	{
		WindowSize = GetViewportRect().Size;
		WindowSizeCenter = WindowSize / 2;

		Control SongPanel = GetNode<Control>("SongPanel");
		float left = WindowSize.X - (WindowSize.X / 2.5f);
		float top = 131.25f;
		if (_header != null)
		{
			// Filter header (placed by its anchors in song_select.tscn) on top, the carousel under it.
			top = _header.Position.Y + Mathf.Max(_header.Size.Y, _header.GetCombinedMinimumSize().Y) + 16;
		}
		SongPanel.Size = new Vector2((WindowSize.X / 2.5f) + 50, WindowSize.Y - top - 70);
		SongPanel.Position = new Vector2(left, top);
		ScrollSongs();
	}

	private void _on_random()
	{
		if (FilteredIndices.Count == 0) return;
		var pick = FilteredIndices[Random.Shared.Next(FilteredIndices.Count)]; // only from what's listed (search / filters)
		SettingsOperator.SelectSongID(pick);
		scrollmode(exactvalue: PosOf(pick));
	}

	private Tween Ani { get; set; }

	private void AnimationScene(int type)
	{
		Ani?.Kill();
		Ani = CreateTween();
		Ani.SetParallel(true);
		if (type == 1)
		{
			Ani.TweenProperty(SongDetails, "position", new Vector2(-SongDetails.Size.X, SongDetails.Position.Y), 0.5f)
				.SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
			Ani.TweenProperty(SongPanel, "position",
					new Vector2(SongPanel.Position.X + SongPanel.Size.X, SongPanel.Position.Y), 0.5f)
				.SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
			Ani.TweenProperty(SongPanel, "modulate", new Color(0, 0, 0, 0), 0.5f).SetEase(Tween.EaseType.Out)
				.SetTrans(Tween.TransitionType.Cubic);
			Ani.TweenProperty(BottomBar, "position",
					new Vector2(BottomBar.Position.X, BottomBar.Position.Y + BottomBar.Size.Y), 0.5f)
				.SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
			Ani.TweenProperty(SongControl, "position", new Vector2(SongControl.Position.X, -SongControl.Size.Y), 0.5f)
				.SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
			Ani.TweenProperty(StartButton, "modulate", new Color(1, 1, 1, 0), 0.5f)
				.SetTrans(Tween.TransitionType.Linear);
			Ani.Play();
		}
		else if (type == 2)
		{
			Ani.TweenProperty(SongDetails, "position", new Vector2(0, SongDetails.Position.Y), 0.5f)
				.SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
			Ani.TweenProperty(SongDetails, "modulate", new Color(1f, 1f, 1f, 1f), 0.5f).SetEase(Tween.EaseType.Out)
				.SetTrans(Tween.TransitionType.Cubic);
			Ani.TweenProperty(SongPanel, "position",
					new Vector2(GetViewportRect().Size.X - SongPanel.Size.X, SongPanel.Position.Y), 0.5f)
				.SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
			Ani.TweenProperty(SongPanel, "modulate", new Color(1f, 1f, 1f, 1f), 0.5f).SetEase(Tween.EaseType.Out)
				.SetTrans(Tween.TransitionType.Cubic);
			Ani.TweenProperty(BottomBar, "position",
					new Vector2(BottomBar.Position.X, GetViewportRect().Size.Y - BottomBar.Size.Y), 0.5f)
				.SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
			Ani.TweenProperty(BottomBar, "modulate", new Color(1f, 1f, 1f, 1f), 0.5f).SetEase(Tween.EaseType.Out)
				.SetTrans(Tween.TransitionType.Cubic);
			Ani.TweenProperty(StartButton, "modulate", new Color(1, 1, 1, 1f), 0.5f)
				.SetTrans(Tween.TransitionType.Linear);
			Ani.TweenProperty(SongControl, "position", new Vector2(SongControl.Position.X, 0), 0.5f)
				.SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
			Ani.TweenProperty(SongControl, "modulate", new Color(1f, 1f, 1f, 1f), 0.5f).SetEase(Tween.EaseType.Out)
				.SetTrans(Tween.TransitionType.Cubic);
			Ani.Play();
		}
		else if (type == 0)
		{
			SongDetails.Position = new Vector2(-SongDetails.Size.X, SongDetails.Position.Y);
			SongPanel.Position = new Vector2(SongPanel.Position.X + SongPanel.Size.X, SongPanel.Position.Y);
			BottomBar.Position = new Vector2(BottomBar.Position.X, GetViewportRect().Size.Y + BottomBar.Size.Y);
			SongControl.Position = new Vector2(SongControl.Position.X, -SongControl.Size.Y);

			SongDetails.Modulate = new Color(0f, 0f, 0f, 0f);
			SongPanel.Modulate = new Color(0f, 0f, 0f, 0f);
			BottomBar.Modulate = new Color(0f, 0f, 0f, 0f);
			SongControl.Modulate = new Color(0f, 0f, 0f, 0f);
		}
	}

	private VBoxContainer SongDetails { get; set; }
	private Control SongPanel { get; set; }
	private Control BottomBar { get; set; }
	private PanelContainer SongControl { get; set; }

	private void PrepareMainComponents()
	{
		SongDetails = GetNode<VBoxContainer>("SongDetails");
		SongPanel = GetNode<Control>("SongPanel");
		BottomBar = GetNode<Control>("BottomBar");
		SongControl = GetNode<PanelContainer>("ControlBar");
	}

	private string Searchtext { get; set; }
	private double timetext { get; set; }
	private bool textchanging { get; set; }

	private void SearchPrepare(string value)
	{
		Searchtext = value;
		textchanging = true;
		timetext = Extras.GetMilliseconds();
	}

	private bool blockinputwhentext { get; set; }
	private void searchfocus() => blockinputwhentext = true;
	private void searchunfocus() => blockinputwhentext = false;

	private void SearchTime()
	{
		if (Extras.GetMilliseconds() - timetext > 500 && textchanging)
		{
			textchanging = false;
			Search(Searchtext);
		}
	}

	private void Search(string value)
	{
		Searchtext = value;
		ApplyFilters(); // no rebuild of the whole list, and the song keeps playing if it still matches
	}

	public override void _Ready()
	{
		PrepareMainComponents(); // Prepares Song Select v2 components.
		AnimationScene(0); // Makes all elements invisible when ready to start.
		SettingsOperator.loopaudio = true;
		scrollBar = GetNode<VScrollBar>("SongPanel/VScrollBar");
		RankStatus = GetNode<PanelContainer>("SongDetails/SongInfo/Rows/Column1/RankBox");
		ContextMenu = GetNode<BeatmapContextMenu>("ContextMenu");
		ContextMenu.Visible = false;
		NoBeatmap = GetNode<Label>("SongPanel/NoBeatmap");
		NoBeatmap.Visible = true;
		SongETick = 0;
		startposition = ((int)GetViewportRect().Size.Y / 2) - 208;

		SettingsOperator.Marathon = false;
		ModScreen = GetNode<Control>("ModsScreen");
		ModeScreen = GetNode<PanelContainer>("ModeScreen");
		ModScreen.Visible = true;
		ModeScreen.Visible = true;
		ModeScreen.Position = new Vector2(138, GetViewportRect().Size.Y);
		ModScreen.Position = new Vector2(0, GetViewportRect().Size.Y);
		SettingsOperator = GetNode<SettingsOperator>("/root/SettingsOperator");
		SongTitle = GetNode<Label>("SongDetails/SongInfo/Rows/Column1/Title");
		ExSongInfo = GetNode<Label>("SongDetails/SongInfo/Rows/ExSongInfo");
		LevelRating = GetNode<PanelContainer>("SongDetails/SongInfo/Rows/Misc/Level");
		Songpp = GetNode<PanelContainer>("SongDetails/SongInfo/Rows/Misc/Points");
		SongBPM = GetNode<PanelContainer>("SongDetails/SongInfo/Rows/Misc/BPM");
		SongLen = GetNode<PanelContainer>("SongDetails/SongInfo/Rows/Misc/Length");
		LeaderboardGlobal = GetNode<Button>("SongDetails/Leaderboard Panel/Rows/Box4/Global");
		LeaderboardLocal = GetNode<Button>("SongDetails/Leaderboard Panel/Rows/Box4/Local");
		LeaderboardDetails = GetNode<Button>("SongDetails/Leaderboard Panel/Rows/Box4/Details");
		DetailsExtended = GetNode<PanelContainer>("SongDetails/Leaderboard Panel/Rows/Details");
		Leaderboardinfo = GetNode<ScrollContainer>("SongDetails/Leaderboard Panel/Rows/LeaderboardInfo");
		StartButton = GetNode<TextureButton>("BottomBar/Start");
		StartButton.Visible = false; // Start the button off with being hidden.
		scrollBar.Value = SettingsOperator.SessionConfig.SongID;
		CheckLeaderboardMode();
		RebuildFilter(""); // Populate FilteredIndices with all songs on load
		// Setting scrollBar.Value above already spawned a few cards from the unfiltered list; drop them so a
		// filtered list (e.g. the LAN difficulty picker) doesn't keep cards from other songs.
		for (int i = SongEntry.Count - 1; i >= 0; i--)
		{
			SongEntry[i].GetParent()?.RemoveChild(SongEntry[i]);
			SongEntry[i].Free();
		}
		SongEntry.Clear();
		if (FilteredIndices.Count > 0)
		{
			// Scroll positions are positions in the filtered list (search / filters / LAN picker), not SongIDs.
			int pos = PosOf(SettingsOperator.SessionConfig.SongID);
			if (pos < 0) { pos = 0; SettingsOperator.SelectSongID(FilteredIndices[0]); }
			scrollBar.MaxValue = FilteredIndices.Count;
			scrollBar.Value = pos;
		}
		AnimationSong = true; // the position is set above
		_knownCount = SettingsOperator.Beatmaps.Count;
		BuildFilterUi();
		BuildHeader();
		BuildCarousel();
		_lastStartIndex = int.MinValue;
		_lastEndIndex = int.MinValue;
		ScrollSongs();

		OldSongID = SettingsOperator.SessionConfig.SongID;

		_res_resize();
		checksongpanel();

		ModsOperator.Refresh();

		AnimationScene(2); // Starts Animation
	}
	// Animation for Start Button DUH

	public Tween StartTween { get; set; }
	private Color idlestartcolour = new Color(1f, 1f, 1f);
	private Color focuscolour = new Color(1.2f, 1.2f, 1.2f);
	private Color startpress = new Color(1.3f, 1.3f, 1.3f);
	private Tween StartTween2 { get; set; }
	private bool HeartbeatHover { get; set; }
	private int beattick = 1;

	private void _tick()
	{
		if (HeartbeatHover && beattick < 4) Sample.PlaySample("res://SelectableSkins/Slia/Sounds/heartbeat.wav");
		if (HeartbeatHover && beattick == 4) Sample.PlaySample("res://SelectableSkins/Slia/Sounds/downbeat.wav");
		if (beattick > 3) beattick = 1;
		else beattick++; // for the feedback of hovering the Qlute logo
		StartButtonTick();
	}

	private void StartButtonTick()
	{
		StartButton.Scale = new Vector2(Scale.X + 0.02777777778f, Scale.Y + 0.02777777778f);
		StartTween2?.Kill();
		StartTween2 = StartButton.CreateTween();
		StartTween2.TweenProperty(StartButton, "scale", new Vector2(1f, 1f),
				60000 / (SettingsOperator.SessionConfig.bpm * AudioPlayer.Instance.PitchScale) * 0.001)
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.Out);
		StartTween2.Play();
	}

	private void _start_down()
	{
		StartTween?.Kill();
		StartTween = StartButton.CreateTween();
		StartTween.TweenProperty(StartButton, "self_modulate", idlestartcolour, 0.2f).SetEase(Tween.EaseType.Out)
			.SetTrans(Tween.TransitionType.Cubic);
		StartTween.Play();
	}

	private void _start_focus()
	{
		HeartbeatHover = true;
		StartTween?.Kill();
		StartTween = StartButton.CreateTween();
		StartTween.TweenProperty(StartButton, "self_modulate", focuscolour, 0.2f).SetEase(Tween.EaseType.Out)
			.SetTrans(Tween.TransitionType.Cubic);
		StartTween.Play();
	}

	private void _start_unfocus()
	{
		HeartbeatHover = false;
		StartTween?.Kill();
		StartTween = StartButton.CreateTween();
		StartTween.TweenProperty(StartButton, "self_modulate", idlestartcolour, 0.2f).SetEase(Tween.EaseType.Out)
			.SetTrans(Tween.TransitionType.Cubic);
		StartTween.Play();
	}
	private int _lastStartIndex = int.MinValue;
	private int _lastEndIndex = int.MinValue;
	/// <summary>The carousel (SongCarousel) draws the list; this only clears any old-style cards.</summary>
	private void ScrollSongs()
	{
		for (int i = SongEntry.Count - 1; i >= 0; i--) { SongEntry[i].GetParent()?.RemoveChild(SongEntry[i]); SongEntry[i].Free(); }
		SongEntry.Clear();
	}

	private object _songPanelState;
	private void checksongpanel()
	{
		// Rebuilding these strings and calling into GDScript every frame is expensive; only do it when something changed.
		var state = (SettingsOperator.SessionConfig.BeatmapTitle, SettingsOperator.SessionConfig.BeatmapArtist,
			SettingsOperator.SessionConfig.BeatmapMapper, SettingsOperator.SessionConfig.BeatmapDifficultyName,
			SettingsOperator.Gameplaycfg.maxpp, ModsMulti.multiplier, SettingsOperator.SessionConfig.LevelRating,
			SettingsOperator.SessionConfig.bpm, AudioPlayer.Instance.PitchScale, SettingsOperator.Gameplaycfg.TimeTotalGame,
			SettingsOperator.Beatmaps.Count > 0, SettingsOperator.SessionConfig.SongID);
		if (state.Equals(_songPanelState)) return;
		_songPanelState = state;

		SongTitle.Text = SettingsOperator.SessionConfig.BeatmapTitle?.ToString() ?? "No song selected.";
		if (SettingsOperator.Beatmaps.Count > 0 && SettingsOperator.SessionConfig.BeatmapTitle != null)
		{
			// Update song details
			ExSongInfo.Text =
				$"by {SettingsOperator.SessionConfig.BeatmapArtist}\nmapped by {SettingsOperator.SessionConfig.BeatmapMapper}{SetInfo()}\nDifficulty: {SettingsOperator.SessionConfig.BeatmapDifficultyName}";
			InfoBox.Text(Songpp,
				"+" + SettingsOperator.GetMaxPpWithMods().ToString("N0") + "pp"); // same as the server (pp v3)
			InfoBox.Text(LevelRating,
				"Lv. " + SettingsOperator.GetLevelWithMods() ?? "Lv. 0");
			LevelRating.SelfModulate =
				SettingsOperator.ReturnLevelColour(SettingsOperator.GetLevelWithMods());
			InfoBox.Text(SongBPM,
				(SettingsOperator.SessionConfig.bpm * AudioPlayer.Instance.PitchScale).ToString("N0") ?? "???");
			InfoBox.Text(SongLen,
				TimeSpan.FromMilliseconds((SettingsOperator.Gameplaycfg.TimeTotalGame / 0.001f) /
				                          AudioPlayer.Instance.PitchScale).ToString(@"mm\:ss") ?? "00:00");

			SetControlsVisibility(true);
		}
		else
		{
			SetControlsVisibility(false);
		}

		if (SettingsOperator.Beatmaps.Count > 0 && SettingsOperator.SessionConfig.SongID == -1)
		{
			SettingsOperator.SelectSongID(0);
			scrollmode(exactvalue: SettingsOperator.SessionConfig.SongID);
		}
	}

	private void SetControlsVisibility(bool visible)
	{
		Songpp.Visible = visible;
		ExSongInfo.Visible = visible;
		SongLen.Visible = visible;
		SongBPM.Visible = visible;
		LevelRating.Visible = visible;
		RankStatus.Visible = visible;
	}
	

	private void _leaderboardmode(int index)
	{
		SettingsOperator.SetSetting("leaderboardtype", index);
		SettingsOperator.LeaderboardType = index;
		if (index != 2)
		{
			SettingsOperator.Start_reloadLeaderboard = true;
		}
	}

	private void CheckLeaderboardMode()
	{
		if (SettingsOperator.LeaderboardType == 0)
		{
			LeaderboardGlobal.ButtonPressed = false;
			LeaderboardLocal.ButtonPressed = true;
			LeaderboardDetails.ButtonPressed = false;
			Leaderboardinfo.Visible = true;
			DetailsExtended.Visible = false;
		}
		else if (SettingsOperator.LeaderboardType == 1)
		{
			LeaderboardGlobal.ButtonPressed = true;
			LeaderboardLocal.ButtonPressed = false;
			LeaderboardDetails.ButtonPressed = false;
			Leaderboardinfo.Visible = true;
			DetailsExtended.Visible = false;
		}
		else
		{
			LeaderboardGlobal.ButtonPressed = false;
			LeaderboardLocal.ButtonPressed = false;
			LeaderboardDetails.ButtonPressed = true;
			Leaderboardinfo.Visible = false;
			DetailsExtended.Visible = true;
		}
	}

	private string NoBeatmapCount = "You have no beatmaps!\nvisit the catalog to find some!\n<3";

	private string NoBeatmapSearch = "Nothing matches your search\n</3";
	private string NoBeatmapFilters = "Nothing matches your search and filters\n(press Filters > Reset to see everything)";

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double _delta)
	{
		SearchTime();
		CheckLeaderboardMode();
		CheckRankStatus();
		// Show Play button if SongID is set.
		StartButton.Visible = (SettingsOperator.SessionConfig.SongID != -1);
		NoBeatmap.Visible = SettingsOperator.Beatmaps.Count == 0 || FilteredIndices.Count == 0;
		var noText = SettingsOperator.Beatmaps.Count == 0 ? NoBeatmapCount
			: SongFilter.ActiveCount > 0 ? NoBeatmapFilters : NoBeatmapSearch;
		if (NoBeatmap.Text != noText) NoBeatmap.Text = noText;
		_filterPanel?.Refresh(FilteredIndices.Count, SettingsOperator.Beatmaps.Count);
		UpdateHeaderCount();
		// Collections changed (e.g. right-click > remove) while showing one: filter again.
		if (_seenCollections != Collections.Version)
		{
			_seenCollections = Collections.Version;
			if (!string.IsNullOrEmpty(SongFilter.Current.Collection)) ApplyFilters();
		}

		if (Input.IsMouseButtonPressed(MouseButton.Right) && SettingsOperator.SessionConfig.SongIDHighlighted != -1)
		{
			ContextMenu.SongID = SettingsOperator.SessionConfig.SongIDHighlighted;
			ContextMenuActive = true;
			ContextMenu.Visible = true;
			ContextMenuAni?.Kill();
			ContextMenuAni = ContextMenu.CreateTween();
			ContextMenuAni.SetParallel(true);
			ContextMenuAni.TweenProperty(ContextMenu, "modulate", new Color(1f, 1f, 1f, 1f), 0.5f)
				.SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
			ContextMenuAni.TweenProperty(ContextMenu, "position", SettingsOperator.MouseMovement, 0.5f)
				.SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
		}
		else if (Input.IsMouseButtonPressed(MouseButton.Left) &&
		         ContextMenuActive) // Hide context menu if left mouse button is pressed outside of it
		{
			Rect2 contextRect = new Rect2(ContextMenu.Position, ContextMenu.Size);
			if (!contextRect.HasPoint(SettingsOperator.MouseMovement))
			{
				ContextMenu.SetMeta("SongID", -1);
				ContextMenuAni?.Kill();
				ContextMenuAni = ContextMenu.CreateTween();
				ContextMenuAni.SetParallel(true);
				ContextMenuAni.TweenProperty(ContextMenu, "modulate", new Color(1f, 1f, 1f, 0f), 0.5f)
					.SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
				ContextMenuAni.TweenProperty(ContextMenu, "position", SettingsOperator.MouseMovement, 0.5f)
					.SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
				ContextMenuAni.TweenProperty(ContextMenu, "visible", false, 0.5f).SetEase(Tween.EaseType.Out)
					.SetTrans(Tween.TransitionType.Cubic);
				ContextMenuActive = false;
			}
		}

		if (SettingsOperator.Start_reloadLeaderboard && SettingsOperator.Beatmaps.Count > 0)
		{
			SettingsOperator.Start_reloadLeaderboard = false;
			GD.Print("Loading Leaderboard for: " + SettingsOperator.SessionConfig.BeatmapID);
			ApiOperator.ReloadLeaderboard(SettingsOperator.SessionConfig.BeatmapID);
		}

		if ((bool)SettingsOperator.SessionConfig.ReloadDB)
		{
			// New / deleted beatmaps: update the list in place instead of reloading song select.
			SettingsOperator.SessionConfig.ReloadDB = false;
			bool removed = SettingsOperator.Beatmaps.Count < _knownCount; // indices shifted: cards must be rebuilt
			_knownCount = SettingsOperator.Beatmaps.Count;
			ApplyFilters(rebuildCards: removed);
		}

		scrollBar.MaxValue = Math.Max(1, FilteredIndices.Count);

		if (!AnimationSong)
		{
			AnimationSong = !AnimationSong;
			scrollBar.Value = Math.Max(0, PosOf(SettingsOperator.SessionConfig.SongID));
		}

		checksongpanel();

		if (Input.IsActionJustPressed("Songup"))
		{
			if (FilteredIndices.Count > 0)
			{
				int currentPos = PosOf(SettingsOperator.SessionConfig.SongID);
				int prevPos = currentPos - 1 >= 0 ? currentPos - 1 : FilteredIndices.Count - 1;
				SettingsOperator.SelectSongID(FilteredIndices[prevPos]);
				scrollmode(exactvalue: prevPos);
			}
		}
		else if (Input.IsActionJustPressed("Songdown"))
		{
			if (FilteredIndices.Count > 0)
			{
				int currentPos = PosOf(SettingsOperator.SessionConfig.SongID);
				int nextPos = currentPos + 1 < FilteredIndices.Count ? currentPos + 1 : 0;
				SettingsOperator.SelectSongID(FilteredIndices[nextPos]);
				scrollmode(exactvalue: nextPos);
			}
		}
		else if (Input.IsActionJustPressed("Mod"))
		{
			_Mods_show();
		}
		else if (Input.IsActionJustPressed("Random"))
		{
			_on_random();
		}
		else if (Input.IsActionJustPressed("Collections") && !blockinputwhentext)
		{
			ToggleFilters(); // collections live in the Filters panel
		}
		else if (_carousel == null && Input.IsActionJustPressed("scrolldown") && scrollBar.Value + 1 < FilteredIndices.Count &&
		         SongPanelAccess)
		{
			if (scrollvelocity < 0)
			{
				scrollvelocity = 0;
			}

			scrollvelocity = Math.Min(scrollvelocity + 1, 2);
			scrollmode(1 + scrollvelocity);
		}
		else if (_carousel == null && Input.IsActionJustPressed("scrollup") && scrollBar.Value - 1 > -1 && SongPanelAccess)
		{
			if (scrollvelocity > 0)
			{
				scrollvelocity = 0;
			}

			scrollvelocity = Math.Max(scrollvelocity - 1, -2);
			scrollmode(-1 + scrollvelocity);
		}
		else if (Input.IsActionJustPressed("ui_accept") && FilteredIndices.Count > 0 && !blockinputwhentext)
		{
			_Start();
		}
	}

	private bool ModsScreenActive = false;
	private bool SongPanelAccess = false;

	private void _songenter()
	{
		SongPanelAccess = true;
	}

	private void _songexit()
	{
		SongPanelAccess = false;
	}
	
	/// <summary>
	/// Status for the Gamemode selection screen is enabled.
	/// </summary>
	private bool ModeScreenActive = false;
	private void _Mode_show()
	{
		if (ModeScreen_Tween != null)
		{
			ModeScreen_Tween.Kill();
		}
		if (ModsScreenActive) _Mods_show();
		ModeScreen_Tween = ModeScreen.CreateTween();
		var colour = new Color(1f, 1f, 1f, 1f);
		var pos = new Vector2(138, GetViewportRect().Size.Y - ModeScreen.Size.Y - 63);
		if (ModeScreenActive)
		{
			ModeScreenActive = false;
			colour = new Color(0f, 0f, 0f, 0f);
			pos = new Vector2(138, GetViewportRect().Size.Y);
		}else {
			ModeScreenActive = true;
		}
		ModeScreen_Tween.SetParallel(true);
		ModeScreen_Tween.TweenProperty(ModeScreen, "position", pos, 0.5f).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
		ModeScreen_Tween.Play();
	}
private void _Mods_show()
	{
		if (ModScreen_Tween != null)
		{
			ModScreen_Tween.Kill();
		}
		if (ModeScreenActive) _Mode_show();
		ModScreen_Tween = ModScreen.CreateTween();
		var colour = new Color(1f, 1f, 1f, 1f);
		var pos = new Vector2(0, 0);
		if (ModsScreenActive)
		{
			ModsScreenActive = false;
			ModScreen.SetProcess(false);
			colour = new Color(0f, 0f, 0f, 0f);
			pos = new Vector2(0, GetViewportRect().Size.Y);
		}
		else
		{
			ModsScreenActive = true;
			ModScreen.SetProcess(true);
		}
		ModScreen_Tween.SetParallel(true);
		ModScreen_Tween.TweenProperty(ModScreen, "position", pos, 0.5f).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
		ModScreen_Tween.Play();
	}
	private void _resetreplay()
	{
		SettingsOperator.SpectatorMode = false;
	}

	private bool _lanPickDone; // the LAN pick was handed over; ignore further presses while the scene fades out

	private void _Start()
	{
		if (_lanPickDone) return;
		if (ModsScreenActive)
		{
			_Mods_show();
		}
		if (SettingsOperator.CreateSelectingBeatmap && SettingsOperator.SessionConfig.SongID >= 0 && SettingsOperator.SessionConfig.SongID < SettingsOperator.Beatmaps.Count)
		{
			// File > Open in the beatmap editor: open the picked difficulty instead of playing it.
			SettingsOperator.CreateSelectingBeatmap = false;
			CreateEditor.EditorSongInfo.LoadFromBeatmap(SettingsOperator.Beatmaps[SettingsOperator.SessionConfig.SongID]);
			AnimationScene(1);
			GetNode<SceneTransition>("/root/Transition").Switch("res://Panels/Screens/Create.tscn");
			return;
		}
		if (LanNetwork.Picking != LanNetwork.PickMode.None && LanNetwork.Instance != null)
		{
			// Picking for a LAN room: hand the beatmap to the room and go back to the lobby instead of playing.
			_lanPickDone = true;
			Sample.PlaySample("res://SelectableSkins/Slia/Sounds/selected.wav");
			AnimationScene(1);
			LanNetwork.Instance.FinishPick(SettingsOperator.SessionConfig.SongID);
			return;
		}
		Sample.PlaySample("res://SelectableSkins/Slia/Sounds/play.wav");

		AnimationScene(1);
		
		GetNode<SceneTransition>("/root/Transition").Switch("res://Panels/Screens/SongLoadingScreen.tscn");
		SettingsOperator.loopaudio = false;
	}
	private void _on_back_pressed()
	{
		if (_lanPickDone) return;
		AnimationScene(1);
		if (LanNetwork.Picking != LanNetwork.PickMode.None && LanNetwork.Instance != null)
		{
			_lanPickDone = true;
			LanNetwork.Instance.FinishPick(null); // cancelled: back to the room
			return;
		}
		if (SettingsOperator.CreateSelectingBeatmap) GetNode<SceneTransition>("/root/Transition").Switch("res://Panels/Screens/Create.tscn");
		else GetNode<SceneTransition>("/root/Transition").Switch("res://Panels/Screens/home_screen.tscn");
	}

	private void _openexternal()
	{
		OS.ShellOpen($"{SettingsOperator.GetSetting("api")}beatmapset/{SettingsOperator.SessionConfig.BeatmapSetID}/{SettingsOperator.SessionConfig.BeatmapID}");
	}
	///<summary>
	/// Check Rank status
	/// </summary>
	private int _lastRankStatus = int.MinValue;
	private void CheckRankStatus()
	{
		// RankBox's setter re-applies self_modulate (forcing a redraw), so only set it when it changes.
		if (_lastRankStatus == ApiOperator.RankedStatus) return;
		_lastRankStatus = ApiOperator.RankedStatus;
		RankStatus.Set("Rankid", _lastRankStatus);
	}

	// ------------------------------------------------------------------ filters, collections, sets

	private SongFilterPanel _filterPanel;
	private Button _filterButton;
	private int _seenCollections = -1;
	/// <summary>Library size when the list was last built: fewer = something was deleted (indices shifted).</summary>
	private int _knownCount;

	/// <summary>" · 4 difficulties · set #123": which set the selected difficulty is from (two sets can share a song).</summary>
	private static string SetInfo()
	{
		int id = SettingsOperator.SessionConfig.SongID;
		if (id < 0 || id >= SettingsOperator.Beatmaps.Count) return "";
		var b = SettingsOperator.Beatmaps[id];
		int n = SongFilter.SetSize(b);
		var text = n == 1 ? "  ·  1 difficulty" : $"  ·  {n} difficulties";
		if (b.BeatmapSetID > 0) text += $"  ·  set #{b.BeatmapSetID}";
		return text;
	}

	private void BuildFilterUi()
	{
		_filterButton = GetNode<Button>("%FiltersButton");
		_filterButton.Pressed += ToggleFilters;

		_filterPanel = new SongFilterPanel
		{
			Visible = false, ZIndex = 10, CustomMinimumSize = new Vector2(640, 0),
			Selected = () => SettingsOperator.SessionConfig.SongID >= 0 && SettingsOperator.SessionConfig.SongID < SettingsOperator.Beatmaps.Count
				? SettingsOperator.Beatmaps[SettingsOperator.SessionConfig.SongID] : null,
		};
		_filterPanel.Changed += () => ApplyFilters();
		AddChild(_filterPanel);
		_seenCollections = Collections.Version;
		UpdateFilterButton();
	}

	private void ToggleFilters()
	{
		if (_filterPanel == null) return;
		_filterPanel.Visible = !_filterPanel.Visible;
		if (!_filterPanel.Visible) return;
		_filterPanel.FillFromState();
		// Right under the header, right-aligned with it.
		var anchor = (_header ?? (Control)_filterButton).GetGlobalRect();
		var size = _filterPanel.GetCombinedMinimumSize();
		_filterPanel.Size = size; // only as tall as its content
		_filterPanel.GlobalPosition = new Vector2(Mathf.Max(10, Mathf.Min(anchor.End.X, GetViewportRect().Size.X) - 18 - size.X), anchor.End.Y + 14);
		_filterPanel.Modulate = new Color(1, 1, 1, 0);
		_filterPanel.CreateTween().TweenProperty(_filterPanel, "modulate:a", 1f, 0.15f);
	}

	private void UpdateFilterButton()
	{
		if (_filterButton == null) return;
		int n = SongFilter.ActiveCount;
		_filterButton.Text = n > 0 ? $"Filters ({n})" : "Filters";
	}

	// ------------------------------------------------------------------ header + carousel

	private SongCarousel _carousel;
	private PanelContainer _header;
	private LevelRangeSlider _levels;
	private OptionButton _sortPick, _collectionPick;
	private Button _sortDirection;
	private Label _matchCount;
	private int _headerCollections = -1;
	private bool _headerQuiet;

	private void BuildCarousel()
	{
		var panel = GetNode<Control>("SongPanel");
		scrollBar.Visible = false; // the carousel scrolls itself (wheel, keys, selection)
		_carousel = new SongCarousel { Name = "Carousel" };
		_carousel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		_carousel.Select = id => SettingsOperator.SelectSongID(id);
		_carousel.Play = () => { if (!blockinputwhentext) _Start(); };
		panel.AddChild(_carousel);
		panel.MoveChild(_carousel, 0); // under the "no beatmaps" message
		ScrollSongs(); // clears the old-style cards
		_carousel.SetData(FilteredIndices);
		_carousel.Follow(SettingsOperator.SessionConfig.SongID, instant: true);
		_res_resize();
		// The header only knows its height after its first layout: place the list under it again then.
		_header.Resized += () => CallDeferred(MethodName._res_resize);
		CallDeferred(MethodName._res_resize);
	}

	private Label _minChip, _maxChip;

	/// <summary>
	/// The search / level / sort header is "FilterHeader" in song_select.tscn (layout and styles are edited there);
	/// this only finds its parts by unique name and wires them up.
	/// </summary>
	private void BuildHeader()
	{
		_header = GetNode<PanelContainer>("FilterHeader");
		_matchCount = GetNode<Label>("%MatchCount");
		_minChip = GetNode<Label>("%LevelMin");
		_maxChip = GetNode<Label>("%LevelMax");

		_levels = GetNode<LevelRangeSlider>("%LevelRange");
		_levels.Moving += (min, max) => ShowLevelChips(min, max);
		_levels.RangeChanged += (min, max) => { SongFilter.Current.LevelMin = min; SongFilter.Current.LevelMax = max; CommitHeader(); };

		_sortPick = GetNode<OptionButton>("%SortPick");
		_sortPick.Clear();
		foreach (var sort in SongFilter.Sorts) _sortPick.AddItem("Sort: " + sort.name);
		_sortPick.ItemSelected += i => { if (_headerQuiet) return; SongFilter.Current.Sort = SongFilter.Sorts[(int)i].mode; CommitHeader(); };

		_sortDirection = GetNode<Button>("%SortDirection");
		_sortDirection.Pressed += () => { SongFilter.Current.Descending = !SongFilter.Current.Descending; CommitHeader(); };

		_collectionPick = GetNode<OptionButton>("%CollectionPick");
		_collectionPick.ItemSelected += i =>
		{
			if (_headerQuiet) return;
			SongFilter.Current.Collection = i == 0 ? null : _collectionPick.GetItemMetadata((int)i).AsString();
			CommitHeader();
		};
		FillHeader();
	}

	private void ShowLevelChips(int min, int max)
	{
		if (_minChip == null) return;
		_minChip.Text = $"Lv {min}";
		_maxChip.Text = max >= 100 ? "100+" : $"Lv {max}";
		// level colour mixed with white: the top levels are nearly black and vanished on the dark chip
		_minChip.AddThemeColorOverride("font_color", SettingsOperator.ReturnLevelColour(Math.Min(min, 99)).Lerp(Colors.White, 0.45f));
		_maxChip.AddThemeColorOverride("font_color", max >= 100 ? Colors.White : SettingsOperator.ReturnLevelColour(Math.Min(max, 99)).Lerp(Colors.White, 0.45f));
	}

	private void CommitHeader()
	{
		SongFilter.Save();
		ApplyFilters();
		if (_filterPanel?.Visible == true) _filterPanel.FillFromState();
	}

	/// <summary>Shows the saved filters in the header (after the Filters panel or a reset changed them).</summary>
	private void FillHeader()
	{
		if (_header == null || _levels == null) return;
		_headerQuiet = true;
		var f = SongFilter.Current;
		_levels.SetRange(f.LevelMin, f.LevelMax);
		ShowLevelChips(_levels.Min, _levels.Max);
		_sortPick.Select(Math.Max(0, Array.FindIndex(SongFilter.Sorts, x => x.mode == f.Sort)));
		_sortDirection.Text = f.Descending ? "↓" : "↑";
		_sortDirection.TooltipText = f.Descending ? "Descending" : "Ascending";
		if (_headerCollections != Collections.Version || _collectionPick.ItemCount != Collections.Names.Count + 1)
		{
			_headerCollections = Collections.Version;
			_collectionPick.Clear();
			_collectionPick.AddItem("Collection: All beatmaps");
			foreach (var name in Collections.Names)
			{
				_collectionPick.AddItem($"Collection: {name}  ({Collections.CountInLibrary(name)})");
				_collectionPick.SetItemMetadata(_collectionPick.ItemCount - 1, name);
			}
		}
		int sel = 0;
		for (int i = 1; i < _collectionPick.ItemCount; i++)
			if (string.Equals(_collectionPick.GetItemMetadata(i).AsString(), f.Collection, StringComparison.OrdinalIgnoreCase)) sel = i;
		_collectionPick.Select(sel);
		_headerQuiet = false;
	}

	private void UpdateHeaderCount()
	{
		if (_matchCount == null) return;
		int total = SettingsOperator.Beatmaps.Count, n = FilteredIndices.Count;
		var text = n == total ? $"{total:N0} maps" : $"{n:N0} match{(n == 1 ? "" : "es")}";
		if (_matchCount.Text != text) _matchCount.Text = text;
		if (_headerCollections != Collections.Version) FillHeader();
	}
}
