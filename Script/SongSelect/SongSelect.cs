using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class SongSelect : Control
{
	// Called when the node enters the scene tree for the first time.
	 [Export] private TextureRect textureRect;
	public SettingsOperator SettingsOperator { get; set; }
	public PackedScene musiccardtemplate;
	public Label SongTitle { get; set; }
	public Label SongArtist { get; set; }
	public Label SongMapper { get; set; }
	private Label LevelRating { get; set; }
	private Label RankStatus { get; set; }
	private Label NoBeatmap { get; set; }
	public Label Songpp { get; set; }
	public Label Debugtext { get; set; }
	public Label SongBPM { get; set; }
	public Label SongLen { get; set; }
	public Button StartButton { get; set; }
	public List<object> SongEntry = new List<object>();
	public int SongETick { get; set; }
	public Texture2D ImageCache {get;set;}
	public string ImageURL {get;set;}
	public Control ModScreen { get; set; }
	public Tween ModScreen_Tween { get; set; }
	public Label Diff { get; set; }
	public ScrollBar scrollBar { get; set; }
	public PanelContainer Info {get;set;}
    public Tween scrolltween { get; private set; }
	private int scrollvelocity {get;set;}
	public Label RankedStatus {get;set;}
	private bool AnimationSong {get;set;}
	private int SongLoaded {get;set;}
	private bool ContextMenuActive {get;set;}
	private Tween ContextMenuAni {get;set;}
	private PanelContainer ContextMenu {get;set;}
	private OptionButton LeaderboardType {get;set;}
	

    int startposition = 0;
	int scrolloldvalue {get;set;}
	private void _valuechangedscroll(float value){ // Part of the loading Beatmaps
		SongETick = 0;
		startposition = ((int)GetViewportRect().Size.Y/2) - 166;
		//Debugtext.Text = $"{value.ToString()}/{SongLoaded.ToString()}";
		ScrollSongs();
	}



	private void scrollmode(int ement = 0, int exactvalue = 0)
	{
		double value = ement != 0 ? scrollBar.Value + ement : exactvalue;
		scrolltween?.Kill();
		scrolltween = CreateTween();
		scrolltween.TweenProperty(scrollBar, "value", value, 0.5f).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
		scrolltween.Play();
	}
	public void _res_resize(){
		var window_size = GetViewportRect().Size;
		Control SongPanel = GetNode<Control>("SongPanel");
		Control SongDetails = GetNode<Control>("SongDetails");
		SongDetails.Size = new Vector2(SongDetails.Size.X, window_size.Y-100-50);
		SongDetails.Position = new Vector2(0, 100);
		SongPanel.Size = new Vector2(window_size.X/2.5f, window_size.Y-150);
		SongPanel.Position = new Vector2(window_size.X-(window_size.X/2.5f), 105);
	}
	public void AddSongList(int id)
	{
		var background = SettingsOperator.Beatmaps[id].Path + SettingsOperator.Beatmaps[id].Background;
		var button = musiccardtemplate.Instantiate().GetNode<Button>(".");
		var SongTitle = button.GetNode<Label>("MarginContainer/VBoxContainer/SongTitle");
		var SongArtist = button.GetNode<Label>("MarginContainer/VBoxContainer/SongArtist");
		var SongMapper = button.GetNode<Label>("MarginContainer/VBoxContainer/SongMapper");
		var TextureRect = button.GetNode<TextureRect>("SongBackgroundPreview/BackgroundPreview");
		var Rating = button.GetNode<Label>("MarginContainer/VBoxContainer/InfoBox/Rating");
		var Version = button.GetNode<Label>("MarginContainer/VBoxContainer/InfoBox/Version");
		button.Position = new Vector2(0, startposition + (83*id));
		SongTitle.Text = SettingsOperator.Beatmaps[id].Title;
		SongArtist.Text = SettingsOperator.Beatmaps[id].Artist;
		SongMapper.Text = "Created by " + SettingsOperator.Beatmaps[id].Mapper;
		Version.Text = SettingsOperator.Beatmaps[id].Version.ToString();
		Rating.Text = "Lv. " + SettingsOperator.Beatmaps[id].Levelrating.ToString("0");
		button.Name = id.ToString();
		button.ClipText = true;
		button.SetMeta("background", background);
		button.SetMeta("SongID", id);
		GetNode<Control>("SongPanel").AddChild(button);
		SongEntry.Add(button);
		//if (background != ImageURL){
		//	ImageCache = SettingsOperator.LoadImage(background);
		//}
		//TextureRect.Texture = ImageCache;
	}
	private void _on_random(){
		SettingsOperator.SelectSongID(SettingsOperator.RndSongID());
		scrollmode(exactvalue: (int)SettingsOperator.Sessioncfg["SongID"]);
	}


	// used later
	private void startanimation(){
		var SongDetails = GetNode<TextureRect>("SongDetails");
		SongDetails.Position = new Vector2(-SongDetails.Size.X,SongDetails.Position.Y);
		SongDetails.Modulate = new Color(0f,0f,0f,0f);
		var SongSelectAnimation = CreateTween();
		SongSelectAnimation.SetParallel(true);
		SongSelectAnimation.TweenProperty(SongDetails, "position", new Vector2(0,SongDetails.Position.Y), 1f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		SongSelectAnimation.TweenProperty(SongDetails, "modulate", new Color(1f,1f,1f,1f), 1f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		SongSelectAnimation.Play();
	}

	public override void _Ready()
	{
		SettingsOperator.loopaudio = true;
		scrollBar = GetNode<VScrollBar>("SongPanel/VScrollBar");
		RankedStatus = GetNode<Label>("SongDetails/SongPanelInfoSpacer/SongPanelInfo/Box1/RankedStatus");
		ContextMenu = GetNode<PanelContainer>("ContextMenu");
		ContextMenu.Visible = false;
		musiccardtemplate = GD.Load<PackedScene>("res://Panels/SongSelectButtons/MusicCard.tscn");
		NoBeatmap = GetNode<Label>("SongPanel/NoBeatmap");
		NoBeatmap.Visible = true;
		SongETick = 0;
		startposition = ((int)GetViewportRect().Size.Y/2) - 166;


		SettingsOperator.Marathon = false;
		Diff = GetNode<Label>("SongDetails/SongPanelInfoSpacer/SongPanelInfo/Box5/Difficulty");
		ModScreen = GetNode<Control>("ModsScreen");
		ModScreen.Visible = true;
		SettingsOperator = GetNode<SettingsOperator>("/root/SettingsOperator");
		SongTitle = GetNode<Label>("SongControl/AmazingPillar/Title");
		LevelRating = GetNode<Label>("SongDetails/SongPanelInfoSpacer/SongPanelInfo/Box5/Level");
		RankStatus = GetNode<Label>("SongDetails/SongPanelInfoSpacer/SongPanelInfo/Box1/RankedStatus");
		SongArtist = GetNode<Label>("SongDetails/SongPanelInfoSpacer/SongPanelInfo/Box1/Artist");
		Songpp = GetNode<Label>("SongDetails/SongPanelInfoSpacer/SongPanelInfo/Box2/Points");
		SongBPM = GetNode<Label>("SongDetails/SongPanelInfoSpacer/SongPanelInfo/Box3/BPM");
		SongLen = GetNode<Label>("SongDetails/SongPanelInfoSpacer/SongPanelInfo/Box3/Length");
		SongMapper = GetNode<Label>("SongDetails/SongPanelInfoSpacer/SongPanelInfo/Box2/Mapper");
		LeaderboardType = GetNode<OptionButton>("SongDetails/SongPanelInfoSpacer/SongPanelInfo/Box4/Leaderboards");
		StartButton = GetNode<Button>("BottomBar/Start");
		StartButton.Visible = false; // Start the button off with being hidden.
		scrollBar.Value = (int)SettingsOperator.Sessioncfg["SongID"];
		LeaderboardType.Selected = SettingsOperator.LeaderboardType;


		check_modscreen();
		

		_res_resize();
		checksongpanel();
	}
	// Animation for Start Button DUH

	public Tween StartTween {get;set;}
	private Color idlestartcolour = new Color(0.5f,0.5f,0.5f,0.5f);
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
	private void _start_unfocus(){
		StartTween?.Kill();
		StartTween = StartButton.CreateTween();
		StartTween.TweenProperty(StartButton, "self_modulate", idlestartcolour, 0.2f).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
		StartTween.Play();
	}
	private void ScrollSongs()
	{
			// Calculate visible range with larger buffer to reduce flickering
			float viewportHeight = GetViewportRect().Size.Y;
			int visibleItemCount = (int)(viewportHeight / 83); // Increased buffer items
			int startIndex = Math.Max(0, (int)scrollBar.Value - (visibleItemCount / 2) - 1);
			int endIndex = Math.Min(SettingsOperator.Beatmaps.Count, startIndex + visibleItemCount + 1);

			// Remove items outside visible range
			for (int i = SongEntry.Count - 1; i >= 0; i--)
			{
				if (SongEntry[i] is Button button)
				{
					int buttonIndex = int.Parse(button.Name);
					if (buttonIndex < startIndex || buttonIndex >= endIndex)
					{
						button.QueueFree();
						SongEntry.RemoveAt(i);
					}
				}
			}

			// Add missing visible items
			for (int i = startIndex; i < endIndex; i++)
			{
				if (!SongEntry.Any(entry => entry is Button btn && btn.Name == i.ToString()))
				{
					AddSongList(i);
				}
				// Update positions of existing entries
				if (SongEntry.FirstOrDefault(e => e is Button btn && btn.Name == i.ToString()) is Button entry)
				{
					entry.ZIndex = 0; // Ensure proper layering
					entry.Position = new Vector2(0, startposition + (83 * i) - (83 * (float)scrollBar.Value));
				}
			}
	}
	// Manages SongDetails

	private void checksongpanel()
	{
		SongTitle.Text = SettingsOperator.Sessioncfg["beatmaptitle"]?.ToString() ?? "No song selected.";
		if (SettingsOperator.Beatmaps.Count > 0 && SettingsOperator.Sessioncfg["beatmaptitle"] != null)
		{
			// Update song details
			SongArtist.Text = SettingsOperator.Sessioncfg["beatmapartist"]?.ToString() ?? "";
			Songpp.Text = "+" + (SettingsOperator.Gameplaycfg.maxpp * ModsMulti.multiplier).ToString("N0") + "pp";
			SongMapper.Text = "Created by " + SettingsOperator.Sessioncfg["beatmapmapper"]?.ToString() ?? "";
			LevelRating.Text = "Lv. " + SettingsOperator.LevelRating.ToString("N0") ?? "Lv. 0";
			SongBPM.Text = "BPM - " + ((int)SettingsOperator.Sessioncfg["beatmapbpm"] * AudioPlayer.Instance.PitchScale).ToString("N0") ?? "";
			SongLen.Text = TimeSpan.FromMilliseconds((SettingsOperator.Gameplaycfg.TimeTotalGame / 0.001f) / AudioPlayer.Instance.PitchScale).ToString(@"mm\:ss") ?? "00:00";


			SetControlsVisibility(true);
		}
		else
		{
			SetControlsVisibility(false);
		}

		if (SettingsOperator.Sessioncfg["beatmapdiff"] != null)
		{
			Diff.Text = SettingsOperator.Sessioncfg["beatmapdiff"].ToString();
		}
		else
		{
			Diff.Text = "";
		}

		if (SettingsOperator.Beatmaps.Count > 0 && (int)SettingsOperator.Sessioncfg["SongID"] == -1)
		{
			SettingsOperator.SelectSongID(0);
			scrollmode(exactvalue: (int)SettingsOperator.Sessioncfg["SongID"]);
		}
	}

	private void SetControlsVisibility(bool visible)
	{
		SongArtist.Visible = visible;
		Songpp.Visible = visible;
		SongMapper.Visible = visible;
		SongLen.Visible = visible;
		SongBPM.Visible = visible;
		LevelRating.Visible = visible;
		RankStatus.Visible = visible;
	}
	public void check_modscreen(){
		if (!ModsScreenActive){
			ModScreen.Position = new Vector2(0,GetViewportRect().Size.Y);
		}
	}

	private void _leaderboardmode(int index)
	{
		SettingsOperator.SetSetting("leaderboardtype", index);
		_reloadLeaderboard();
	}

	private void _reloadLeaderboard()
	{
		if (SettingsOperator.Sessioncfg["osubeatid"] != null)
		{
			ApiOperator.ReloadLeaderboard((int)SettingsOperator.Sessioncfg["osubeatid"]);
		}
		else
		{
			GD.PrintErr("No osubeatid found in Sessioncfg, cannot reload leaderboard.");
		}
	}
	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double _delta)
	{
		// Show Play button if SongID is set.
		StartButton.Visible = ((int)SettingsOperator.Sessioncfg["SongID"] != -1);
		NoBeatmap.Visible = (SettingsOperator.Beatmaps.Count < 1);

		if (Input.IsMouseButtonPressed(MouseButton.Right) && SettingsOperator.SongIDHighlighted != -1)
		{
			ContextMenu.SetMeta("SongID", SettingsOperator.SongIDHighlighted);
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

		if (SettingsOperator.Start_reloadLeaderboard && ApiOperator.LeaderboardStatus == 0 && SettingsOperator.Beatmaps.Count > 0)
		{
			SettingsOperator.Start_reloadLeaderboard = false;
			GD.Print("Loading Leaderboard for: " + SettingsOperator.Sessioncfg["osubeatid"]);
			_reloadLeaderboard();
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
			scrollmode(exactvalue: (int)SettingsOperator.Sessioncfg["SongID"]);
		}
		ScrollSongs();
		checksongpanel();

		if (Input.IsActionJustPressed("Songup"))
		{
			if ((int)SettingsOperator.Sessioncfg["SongID"] - 1 >= 0 && (int)SettingsOperator.Sessioncfg["SongID"] != -1)
			{
				SettingsOperator.SelectSongID((int)SettingsOperator.Sessioncfg["SongID"] - 1);
			}
			else if ((int)SettingsOperator.Sessioncfg["SongID"] != -1)
			{
				SettingsOperator.SelectSongID((int)SettingsOperator.Beatmaps.Count - 1);
			}
			scrollmode(exactvalue: (int)SettingsOperator.Sessioncfg["SongID"]);
		}
		else if (Input.IsActionJustPressed("Songdown"))
		{
			if ((int)SettingsOperator.Sessioncfg["SongID"] + 1 < (int)SettingsOperator.Beatmaps.Count && (int)SettingsOperator.Sessioncfg["SongID"] != -1)
			{
				SettingsOperator.SelectSongID((int)SettingsOperator.Sessioncfg["SongID"] + 1);
			}
			else if ((int)SettingsOperator.Sessioncfg["SongID"] != -1)
			{
				SettingsOperator.SelectSongID(0);
			}
			scrollmode(exactvalue: (int)SettingsOperator.Sessioncfg["SongID"]);
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
		else if (MusicCard.Connection_Button)
		{
			scrollmode(exactvalue: (int)SettingsOperator.Sessioncfg["SongID"]);
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
	private void _Mods_show(){
		if (ModScreen_Tween != null){
			ModScreen_Tween.Kill();
		}
		ModScreen_Tween = ModScreen.CreateTween();
		var colour = new Color(1f,1f,1f,1f);
		var pos = new Vector2(0,0);
		if (ModsScreenActive) {
			ModsScreenActive = false;
			colour = new Color(0f,0f,0f,0f);
			pos = new Vector2(0,GetViewportRect().Size.Y);
		}else {
			ModsScreenActive = true;
			ModScreen.Position = new Vector2(0,GetViewportRect().Size.Y);
		}
		ModScreen_Tween.SetParallel(true);
		ModScreen_Tween.TweenProperty(ModScreen, "position", pos , 0.5f).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
		ModScreen_Tween.Play();
	}
	private void _resetreplay()
	{
		SettingsOperator.SpectatorMode = false;
	}

	private void _Start(){
		if (ModsScreenActive){
			_Mods_show();
		}
		Sample.PlaySample("res://Skin/Sounds/play.wav");


		if (ModsOperator.Mods["npc"])
		{
			Gameplay.reload_npcleaderboard();
		}

		
		var SongDetails = GetNode<TextureRect>("SongDetails");
		var SongPanel = GetNode<Control>("SongPanel");
		var BottomBar = GetNode<Control>("BottomBar");
		var SongControl = GetNode<PanelContainer>("SongControl");
		var LoadScreen = GD.Load<PackedScene>("res://Panels/Screens/SongLoadingScreen.tscn").Instantiate().GetNode<Control>(".");
		LoadScreen.Modulate = new Color(0,0,0,0);
		GetTree().CurrentScene.AddChild(LoadScreen);
		var Ani = CreateTween();
		Ani.SetParallel(true);
		Ani.TweenProperty(SongDetails, "position", new Vector2(-SongDetails.Size.X,SongDetails.Position.Y), 0.5f).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
		Ani.TweenProperty(SongPanel, "position", new Vector2(SongPanel.Position.X+SongPanel.Size.X,SongPanel.Position.Y), 0.5f).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
		Ani.TweenProperty(SongPanel, "modulate", new Color(0,0,0,0), 0.5f).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
		Ani.TweenProperty(BottomBar, "position", new Vector2(BottomBar.Position.X,BottomBar.Position.Y+BottomBar.Size.Y), 0.5f).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
		Ani.TweenProperty(SongControl, "position", new Vector2(SongControl.Position.X,-SongControl.Size.Y), 0.5f).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
		Ani.TweenProperty(LoadScreen, "modulate", new Color(1,1,1,1), 0.5f).SetTrans(Tween.TransitionType.Linear);
		Ani.Play();
		SettingsOperator.loopaudio = false;
	}
	private void _on_back_pressed(){
		if (SettingsOperator.CreateSelectingBeatmap) GetNode<SceneTransition>("/root/Transition").Switch("res://Panels/Screens/Create.tscn");
		else GetNode<SceneTransition>("/root/Transition").Switch("res://Panels/Screens/home_screen.tscn");
	}
}