using Godot;
using Realms;
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
	public PackedScene musiccardtemplate;
	private GpuParticles2D Particle { get; set; }
	public Label SongTitle { get; set; }
	public Label ExSongInfo { get; set; }
	private PanelContainer LevelRating { get; set; }
	private PanelContainer RankStatus { get; set; }
	private Label NoBeatmap { get; set; }
	public PanelContainer Songpp { get; set; }
	public Label Debugtext { get; set; }
	public PanelContainer SongBPM { get; set; }
	public PanelContainer SongLen { get; set; }
	public Button StartButton { get; set; }
	public List<Button> SongEntry = new List<Button>();
	public int SongETick { get; set; }
	public Texture2D ImageCache { get; set; }
	public string ImageURL { get; set; }
	public Control ModScreen { get; set; }
	public Tween ModScreen_Tween { get; set; }
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
	private Vector2 CardSize { get; set; }
	private bool Update { get; set; }
	
	private int OldSongID { get; set; }


	int startposition = 0;
	int scrolloldvalue { get; set; }
	private void _valuechangedscroll(float value)
	{ // Part of the loading Beatmaps
		SongETick = 0;
		startposition = ((int)GetViewportRect().Size.Y / 2) - 166;
		ScrollSongs();
	}



	private void scrollmode(int ement = 0, int exactvalue = 0)
	{
		OldSongID = SettingsOperator.SongID;
		double value = ement != 0 ? scrollBar.Value + ement : exactvalue;
		scrolltween?.Kill();
		scrolltween = CreateTween();
		scrolltween.TweenProperty(scrollBar, "value", value, 0.5f).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
		scrolltween.Play();
	}
	public void _res_resize()
	{
		var window_size = GetViewportRect().Size;
		Control SongPanel = GetNode<Control>("SongPanel");
		SongPanel.Size = new Vector2((window_size.X / 2.5f) + 40, window_size.Y - 150);
		SongPanel.Position = new Vector2(window_size.X - (window_size.X / 2.5f), 105);
		ScrollSongs();
	}
	///<summary>
	/// Initiates Music Card then returns into a button.
	/// </summary>
	private MusicCard InitiateMusicCard()
	{
		return (MusicCard)musiccardtemplate.Instantiate();
	}

	public void AddSongList(int id)
	{
		MusicCard button = InitiateMusicCard();
		var Rating = button.GetNode<Label>("MarginContainer/VBoxContainer/InfoBoxBG/InfoBox/Rating");
		var Version = button.GetNode<Label>("MarginContainer/VBoxContainer/InfoBoxBG/InfoBox/Version");
		button.Position = new Vector2(0, startposition + (CardSize.Y * id));
		button.Name = id.ToString();
		button.ClipText = true;
		button.BackgroundPath = SettingsOperator.Beatmaps[id].Path + SettingsOperator.Beatmaps[id].Background;
		button.SongID = id;
		GetNode<Control>("SongPanel").AddChild(button);
		SongEntry.Add(button);
	}
	private void _on_random()
	{
		SettingsOperator.SelectSongID(SettingsOperator.RndSongID());
		scrollmode(exactvalue: SettingsOperator.SongID);
	}

	private Tween Ani { get; set; }
	
	
	
	private void AnimationScene(int type)
	{
		Ani?.Kill();
		Ani = CreateTween();
		Ani.SetParallel(true);
		if (type == 1)
		{
			var LoadScreen = GD.Load<PackedScene>("res://Panels/Screens/SongLoadingScreen.tscn").Instantiate().GetNode<Control>(".");
			LoadScreen.Modulate = new Color(0, 0, 0, 0);
			GetTree().CurrentScene.AddChild(LoadScreen);
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
			Ani.TweenProperty(LoadScreen, "modulate", new Color(1, 1, 1, 1), 0.5f)
				.SetTrans(Tween.TransitionType.Linear);
			Ani.Play();
		}
		else if (type == 2)
		{
			Ani.TweenProperty(SongDetails, "position", new Vector2(0, SongDetails.Position.Y), 0.5f)
				.SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
			Ani.TweenProperty(SongDetails, "modulate", new Color(1f,1f,1f,1f), 0.5f).SetEase(Tween.EaseType.Out)
				.SetTrans(Tween.TransitionType.Cubic);
			Ani.TweenProperty(SongPanel, "position",
					new Vector2(GetViewportRect().Size.X - SongPanel.Size.X, SongPanel.Position.Y), 0.5f)
				.SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
			Ani.TweenProperty(SongPanel, "modulate", new Color(1f,1f,1f,1f), 0.5f).SetEase(Tween.EaseType.Out)
				.SetTrans(Tween.TransitionType.Cubic);
			Ani.TweenProperty(BottomBar, "position",
					new Vector2(BottomBar.Position.X, GetViewportRect().Size.Y - BottomBar.Size.Y), 0.5f)
				.SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
			Ani.TweenProperty(BottomBar, "modulate", new Color(1f,1f,1f,1f), 0.5f).SetEase(Tween.EaseType.Out)
				.SetTrans(Tween.TransitionType.Cubic);
			Ani.TweenProperty(SongControl, "position", new Vector2(SongControl.Position.X, 0), 0.5f)
				.SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
			Ani.TweenProperty(SongControl, "modulate", new Color(1f,1f,1f,1f), 0.5f).SetEase(Tween.EaseType.Out)
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
			BottomBar.Modulate =  new Color(0f, 0f, 0f, 0f);
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
	public override void _Ready()
	{
		PrepareMainComponents(); // Prepares Song Select v2 components.
		AnimationScene(0); // Makes all elements invisible when ready to start.
		
		SettingsOperator.loopaudio = true;
		scrollBar = GetNode<VScrollBar>("SongPanel/VScrollBar");
		RankStatus = GetNode<PanelContainer>("SongDetails/SongInfo/Rows/Column1/RankBox");
		ContextMenu = GetNode<BeatmapContextMenu>("ContextMenu");
		ContextMenu.Visible = false;
		musiccardtemplate = ResourceLoader.Load<PackedScene>("res://Panels/SongSelectButtons/MusicCard.tscn");
		NoBeatmap = GetNode<Label>("SongPanel/NoBeatmap");
		NoBeatmap.Visible = true;
		SongETick = 0;
		startposition = ((int)GetViewportRect().Size.Y / 2) - 166;

		// Particle
		Particle = GetNode<GpuParticles2D>("Particle");
		Particle.Emitting = true; // Enables it


		SettingsOperator.Marathon = false;
		ModScreen = GetNode<Control>("ModsScreen");
		ModScreen.Visible = true;
		SettingsOperator = GetNode<SettingsOperator>("/root/SettingsOperator");
		SongTitle = GetNode<Label>("SongDetails/SongInfo/Rows/Column1/Title");
		ExSongInfo = GetNode<Label>("SongDetails/SongInfo/Rows/ExSongInfo");

		CardSize = InitiateMusicCard().Size;
		CardSize = new Vector2(CardSize.X, CardSize.Y + 5);

		LevelRating = GetNode<PanelContainer>("SongDetails/SongInfo/Rows/Misc/Level");
		Songpp = GetNode<PanelContainer>("SongDetails/SongInfo/Rows/Misc/Points");
		SongBPM = GetNode<PanelContainer>("SongDetails/SongInfo/Rows/Misc/BPM");
		SongLen = GetNode<PanelContainer>("SongDetails/SongInfo/Rows/Misc/Length");
		LeaderboardGlobal = GetNode<Button>("SongDetails/Leaderboard Panel/Rows/Box4/Global");
		LeaderboardLocal = GetNode<Button>("SongDetails/Leaderboard Panel/Rows/Box4/Local");
		LeaderboardDetails = GetNode<Button>("SongDetails/Leaderboard Panel/Rows/Box4/Details");
		DetailsExtended = GetNode<PanelContainer>("SongDetails/Leaderboard Panel/Rows/Details");
		Leaderboardinfo = GetNode<ScrollContainer>("SongDetails/Leaderboard Panel/Rows/LeaderboardInfo");
		StartButton = GetNode<Button>("BottomBar/Start");
		StartButton.Visible = false; // Start the button off with being hidden.
		scrollBar.Value = SettingsOperator.SongID;
		CheckLeaderboardMode();


		check_modscreen();
		ScrollSongs();

		OldSongID = SettingsOperator.SongID;
		
		_res_resize();
		checksongpanel();
		
		AnimationScene(2); // Starts Animation
	}
	// Animation for Start Button DUH

	public Tween StartTween { get; set; }
	private Color idlestartcolour = new Color(0.5f, 0.5f, 0.5f, 0.5f);
	private Color focuscolour = new Color(0.270f, 0.549f, 1f);
	private Color startpress = new Color(0.44f, 0.53f, 0.5f);
	private void _start_down()
	{
		StartTween?.Kill();
		StartTween = StartButton.CreateTween();
		StartTween.TweenProperty(StartButton, "self_modulate", idlestartcolour, 0.2f).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
		StartTween.Play();
	}
	private void _start_focus()
	{
		StartTween?.Kill();
		StartTween = StartButton.CreateTween();
		StartTween.TweenProperty(StartButton, "self_modulate", focuscolour, 0.2f).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
		StartTween.Play();
	}
	private void _start_unfocus()
	{
		StartTween?.Kill();
		StartTween = StartButton.CreateTween();
		StartTween.TweenProperty(StartButton, "self_modulate", idlestartcolour, 0.2f).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
		StartTween.Play();
	}

	private void ScrollSongs()
	{
		if (scrollBar == null)
			return;

		SongLoaded = 0;
		float height = GetViewportRect().Size.Y;
		int cardHeight = (int)(CardSize.Y + 5);
		int itemCount = (int)(height / cardHeight);
		int startIndex = Math.Max(0, (int)scrollBar.Value - (itemCount / 2));
		int endIndex = Math.Min(SettingsOperator.Beatmaps.Count, (int)scrollBar.Value + (itemCount / 2) + 2);

		// ⚡ Build quick lookup of visible buttons to avoid repeated LINQ
		var visible = new Dictionary<int, Button>(SongEntry.Count);
		for (int i = 0; i < SongEntry.Count; i++)
		{
			Button btn = SongEntry[i];
			if (btn != null)
			{
				int idx = int.Parse(btn.Name);
				visible[idx] = btn;
			}
		}

		// ⚡ Remove off-screen buttons in reverse to avoid shifting
		for (int i = SongEntry.Count - 1; i >= 0; i--)
		{
			Button button = SongEntry[i];
			int buttonIndex = int.Parse(button.Name);

			if (buttonIndex < startIndex || buttonIndex >= endIndex)
			{
				button.QueueFree();
				SongEntry.RemoveAt(i);
				visible.Remove(buttonIndex);
			}
		}

		// ⚡ Add missing visible items and update existing ones
		for (int i = startIndex; i < endIndex; i++)
		{
			Button entry;
			if (!visible.TryGetValue(i, out entry))
			{
				// new song card needed
				AddSongList(i);
				entry = SongEntry.Last(); // AddSongList adds it to SongEntry
				visible[i] = entry;
				SongLoaded++;
			}

			// Update existing position (no redundant search)
			float targetY = startposition + (CardSize.Y * i) - (CardSize.Y * (float)scrollBar.Value);
			if (!Mathf.IsEqualApprox(entry.Position.Y, targetY))
				entry.Position = new Vector2(entry.Position.X, targetY);

			entry.ZIndex = 0;
			SongLoaded++;
		}
	}

	private void checksongpanel()
	{
		SongTitle.Text = SettingsOperator.Sessioncfg["beatmaptitle"]?.ToString() ?? "No song selected.";
		if (SettingsOperator.Beatmaps.Count > 0 && SettingsOperator.Sessioncfg["beatmaptitle"] != null)
		{
			// Update song details
			ExSongInfo.Text = $"by {SettingsOperator.Sessioncfg["beatmapartist"]}\nmapped by {SettingsOperator.Sessioncfg["beatmapmapper"]}\nDifficulty: {SettingsOperator.Sessioncfg["beatmapdiff"]}";
			InfoBox.Text(Songpp, "+" + (SettingsOperator.Gameplaycfg.maxpp * ModsMulti.multiplier).ToString("N0") + "pp");
			InfoBox.Text(LevelRating, "Lv. " + (SettingsOperator.LevelRating * ModsMulti.multiplier).ToString("N0") ?? "Lv. 0");
			LevelRating.SelfModulate = SettingsOperator.ReturnLevelColour((int)(SettingsOperator.LevelRating * ModsMulti.multiplier));
			InfoBox.Text(SongBPM, ((int)SettingsOperator.Sessioncfg["beatmapbpm"] * AudioPlayer.Instance.PitchScale).ToString("N0") ?? "???");
			InfoBox.Text(SongLen, TimeSpan.FromMilliseconds((SettingsOperator.Gameplaycfg.TimeTotalGame / 0.001f) / AudioPlayer.Instance.PitchScale).ToString(@"mm\:ss") ?? "00:00");


			SetControlsVisibility(true);
		}
		else
		{
			SetControlsVisibility(false);
		}

		if (SettingsOperator.Beatmaps.Count > 0 && SettingsOperator.SongID == -1)
		{
			SettingsOperator.SelectSongID(0);
			scrollmode(exactvalue: SettingsOperator.SongID);
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
	public void check_modscreen()
	{
		if (!ModsScreenActive)
		{
			ModScreen.Position = new Vector2(0, GetViewportRect().Size.Y);
		}
	}

	private void _leaderboardmode(int index)
	{
		GD.Print(index);
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

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double _delta)
	{
		CheckLeaderboardMode();
		CheckRankStatus();
		// Show Play button if SongID is set.
		StartButton.Visible = (SettingsOperator.SongID != -1);
		NoBeatmap.Visible = (SettingsOperator.Beatmaps.Count < 1);

		if (Input.IsMouseButtonPressed(MouseButton.Right) && SettingsOperator.SongIDHighlighted != -1)
		{
			ContextMenu.SongID = SettingsOperator.SongIDHighlighted;
			ContextMenuActive = true;
			ContextMenu.Visible = true;
			ContextMenuAni?.Kill();
			ContextMenuAni = ContextMenu.CreateTween();
			ContextMenuAni.SetParallel(true);
			ContextMenuAni.TweenProperty(ContextMenu, "modulate", new Color(1f, 1f, 1f, 1f), 0.5f).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
			ContextMenuAni.TweenProperty(ContextMenu, "position", GetViewport().GetMousePosition(), 0.5f).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
		}
		else if (Input.IsMouseButtonPressed(MouseButton.Left) && ContextMenuActive) // Hide context menu if left mouse button is pressed outside of it
		{
			Vector2 mousePos = GetViewport().GetMousePosition();
			Rect2 contextRect = new Rect2(ContextMenu.Position, ContextMenu.Size);
			if (!contextRect.HasPoint(mousePos))
			{
				ContextMenu.SetMeta("SongID", -1);
				ContextMenuAni?.Kill();
				ContextMenuAni = ContextMenu.CreateTween();
				ContextMenuAni.SetParallel(true);
				ContextMenuAni.TweenProperty(ContextMenu, "modulate", new Color(1f, 1f, 1f, 0f), 0.5f).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
				ContextMenuAni.TweenProperty(ContextMenu, "position", GetViewport().GetMousePosition(), 0.5f).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
				ContextMenuAni.TweenProperty(ContextMenu, "visible", false, 0.5f).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
				ContextMenuActive = false;
			}
		}

		if (SettingsOperator.Start_reloadLeaderboard && SettingsOperator.Beatmaps.Count > 0)
		{
			SettingsOperator.Start_reloadLeaderboard = false;
			GD.Print("Loading Leaderboard for: " + SettingsOperator.BeatmapID);
			ApiOperator.ReloadLeaderboard(SettingsOperator.BeatmapID);
		}
		if ((bool)SettingsOperator.Sessioncfg["reloaddb"])
		{
			SettingsOperator.Sessioncfg["reloaddb"] = false;
			GetTree().ReloadCurrentScene();
		}
		check_modscreen();
		scrollBar.MaxValue = SettingsOperator.Beatmaps.Count;

		if (!AnimationSong)
		{
			AnimationSong = !AnimationSong;
			scrollBar.Value = SettingsOperator.SongID;
		}
		checksongpanel();

		if (Input.IsActionJustPressed("Songup"))
		{
			if (SettingsOperator.SongID - 1 >= 0 && SettingsOperator.SongID != -1)
			{
				SettingsOperator.SelectSongID(SettingsOperator.SongID - 1);
			}
			else if (SettingsOperator.SongID != -1)
			{
				SettingsOperator.SelectSongID((int)SettingsOperator.Beatmaps.Count - 1);
			}
			scrollmode(exactvalue: SettingsOperator.SongID);
		}
		else if (Input.IsActionJustPressed("Songdown"))
		{
			if (SettingsOperator.SongID + 1 < (int)SettingsOperator.Beatmaps.Count && SettingsOperator.SongID != -1)
			{
				SettingsOperator.SelectSongID(SettingsOperator.SongID + 1);
			}
			else if (SettingsOperator.SongID != -1)
			{
				SettingsOperator.SelectSongID(0);
			}
			scrollmode(exactvalue: SettingsOperator.SongID);
		}
		else if (Input.IsActionJustPressed("Mod"))
		{
			_Mods_show();
		}
		else if (Input.IsActionJustPressed("Random"))
		{
			_on_random();
		}
		else if (Input.IsActionJustPressed("Collections"))
		{
		}
		else if (Input.IsActionJustPressed("scrolldown") && scrollBar.Value + 1 < SettingsOperator.Beatmaps.Count && SongPanelAccess)
		{
			if (scrollvelocity < 0)
			{
				scrollvelocity = 0;
			}
			scrollvelocity = Math.Min(scrollvelocity + 1, 2);
			scrollmode(1 + scrollvelocity);
		}
		else if (Input.IsActionJustPressed("scrollup") && scrollBar.Value - 1 > -1 && SongPanelAccess)
		{
			if (scrollvelocity > 0)
			{
				scrollvelocity = 0;
			}
			scrollvelocity = Math.Max(scrollvelocity - 1, -2);
			scrollmode(-1 + scrollvelocity);
		}
		else if (Input.IsActionJustPressed("ui_accept") && SettingsOperator.Beatmaps.Count > 0)
		{
			_Start();
		}
		else if (MusicCard.Connection_Button && OldSongID == SettingsOperator.SongID)
		{
			_Start();
			MusicCard.Connection_Button = false;
		}
		else if (MusicCard.Connection_Button && OldSongID != SettingsOperator.SongID)
		{
			scrollmode(exactvalue: SettingsOperator.SongID);
			MusicCard.Connection_Button = false;
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
	private void _Mods_show()
	{
		if (ModScreen_Tween != null)
		{
			ModScreen_Tween.Kill();
		}
		ModScreen_Tween = ModScreen.CreateTween();
		var colour = new Color(1f, 1f, 1f, 1f);
		var pos = new Vector2(0, 0);
		if (ModsScreenActive)
		{
			ModsScreenActive = false;
			colour = new Color(0f, 0f, 0f, 0f);
			pos = new Vector2(0, GetViewportRect().Size.Y);
		}
		else
		{
			ModsScreenActive = true;
			ModScreen.Position = new Vector2(0, GetViewportRect().Size.Y);
		}
		ModScreen_Tween.SetParallel(true);
		ModScreen_Tween.TweenProperty(ModScreen, "position", pos, 0.5f).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
		ModScreen_Tween.Play();
	}
	private void _resetreplay()
	{
		SettingsOperator.SpectatorMode = false;
	}

	private void _Start()
	{
		if (ModsScreenActive)
		{
			_Mods_show();
		}
		Sample.PlaySample("res://SelectableSkins/Slia/Sounds/play.wav");


		if (ModsOperator.Mods["npc"])
		{
			Gameplay.reload_npcleaderboard();
		}

		AnimationScene(1);
		SettingsOperator.loopaudio = false;
	}
	private void _on_back_pressed()
	{
		AnimationScene(1);
		if (SettingsOperator.CreateSelectingBeatmap) GetNode<SceneTransition>("/root/Transition").Switch("res://Panels/Screens/Create.tscn");
		else GetNode<SceneTransition>("/root/Transition").Switch("res://Panels/Screens/home_screen.tscn");
	}

	private void _openexternal()
	{
		OS.ShellOpen(SettingsOperator.Configuration["api"] + "beatmap/" + SettingsOperator.BeatmapID);
	}
	///<summary>
	/// Check Rank status
	/// </summary>
	private void CheckRankStatus()
	{
		RankStatus.Set("Rankid", ApiOperator.RankedStatus);
	}
}