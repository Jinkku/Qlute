using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class SongSelect : Control
{
	// Called when the node enters the scene tree for the first time.
	 [Export] private TextureRect textureRect;
	public static SettingsOperator SettingsOperator { get; set; }
	public PackedScene musiccardtemplate;
	public Label SongTitle { get; set; }
	public Label SongArtist { get; set; }
	public Label SongMapper { get; set; }
	public Label Songpp { get; set; }
	public Label Debugtext { get; set; }
	public Label SongBPM { get; set; }
	public Label SongLen { get; set; }
	public Label SongAccuracy { get; set; }
	public List<object> SongEntry = new List<object>();
	public int SongETick { get; set; }
	public Texture2D ImageCache {get;set;}
	public string ImageURL {get;set;}
	public PanelContainer ModScreen { get; set; }
	public Label Diff { get; set; }
	public ScrollBar scrollBar { get; set; }
	public PanelContainer Info {get;set;}
    public Tween scrolltween { get; private set; }
	private int scrollvelocity {get;set;}
	public Label RankedStatus {get;set;}
	private bool AnimationSong {get;set;}
	private int SongLoaded {get;set;}

    int startposition = 30;
	int scrolloldvalue {get;set;}
	private void _valuechangedscroll(float value){
		SongETick = 0;
		startposition = (int)GetViewportRect().Size.Y/2 - 166;
		Debugtext.Text = $"{value.ToString()}/{SongLoaded.ToString()}";
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
		SongPanel.Size = new Vector2(window_size.X/2.5f, window_size.Y-150);
		SongPanel.Position = new Vector2(window_size.X-(window_size.X/2.5f), 105);
		Info = GetNode<PanelContainer>("SongDetails/Info");
		Info.Size = new Vector2(GetViewportRect().Size.X/2.3f,Info.Size.Y);
	}
	public void AddSongList(int id)
	{
		
		var button = musiccardtemplate.Instantiate();
		GetNode<Control>("SongPanel").AddChild(button);
		SongEntry.Add(button);
		var background = SettingsOperator.Beatmaps[id]["path"].ToString()+SettingsOperator.Beatmaps[id]["background"].ToString();
		var childButton = button.GetNode<Button>(".");
		var SongTitle = button.GetNode<Label>("MarginContainer/VBoxContainer/SongTitle");
		var SongArtist = button.GetNode<Label>("MarginContainer/VBoxContainer/SongArtist");
		var SongMapper = button.GetNode<Label>("MarginContainer/VBoxContainer/SongMapper");
		var TextureRect = button.GetNode<TextureRect>("SongBackgroundPreview/BackgroundPreview");
		var Rating = button.GetNode<Label>("MarginContainer/VBoxContainer/InfoBox/Rating");
		var Version = button.GetNode<Label>("MarginContainer/VBoxContainer/InfoBox/Version");
		childButton.Position = new Vector2(0, startposition + (83*id));
		SongTitle.Text = SettingsOperator.Beatmaps[id]["Title"].ToString();
		SongArtist.Text = SettingsOperator.Beatmaps[id]["Artist"].ToString();
		SongMapper.Text = "Created by " + SettingsOperator.Beatmaps[id]["Mapper"].ToString();
		Version.Text = SettingsOperator.Beatmaps[id]["Version"].ToString();
		Rating.Text = "Lv. " + float.Parse(SettingsOperator.Beatmaps[id]["levelrating"].ToString()).ToString("0");
		childButton.Name = id.ToString();
		childButton.ClipText = true;
		childButton.SetMeta("bg", background);
		childButton.SetMeta("SongID", id);
		if (background != ImageURL){
			ImageCache = SettingsOperator.LoadImage(background);
		}
		TextureRect.Texture = ImageCache;
	}
	private void _on_random(){
		SettingsOperator.SelectSongID(SettingsOperator.RndSongID());
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
		RankedStatus = GetNode<Label>("SongDetails/Info/Plasa/RankedStatus");
		musiccardtemplate = GD.Load<PackedScene>("res://Panels/SongSelectButtons/MusicCard.tscn");
		SongETick = 0;


		Debugtext = new Label();
		Debugtext.ZIndex = 1024;
		Debugtext.Text = "X3";
		Debugtext.Position = new Vector2(100,100);
		AddChild(Debugtext);
		Diff = GetNode<Label>("SongDetails/Info/Plasa/Difficulty");
		ModScreen = GetNode<PanelContainer>("ModsScreen");
		SettingsOperator = GetNode<SettingsOperator>("/root/SettingsOperator");
		SongTitle = GetNode<Label>("SongDetails/Info/Plasa/Title");
		SongArtist = GetNode<Label>("SongDetails/Info/Plasa/Artist");
		Songpp = GetNode<Label>("SongDetails/Info/Plasa/Points");
		SongBPM = GetNode<Label>("SongDetails/Info/Plasa/BPM");
		SongLen = GetNode<Label>("SongDetails/Info/Plasa/Length");
		SongMapper = GetNode<Label>("SongDetails/Info/Plasa/Mapper");
		SongAccuracy = GetNode<Label>("SongDetails/Info/Plasa/Accuracy");
		var timex = DateTime.Now.Millisecond;
		scrollBar.Value = (int)SettingsOperator.Sessioncfg["SongID"];

		
//		for(int i = 0; i < SettingsOperator.Beatmaps.Count();i++)
//		{	
//			AddSongList(i);
//		}
		GD.Print("Finished about " + (DateTime.Now.Millisecond-timex) + "ms");
		

		_res_resize();
		checksongpanel();
		GD.Print((int)SettingsOperator.Sessioncfg["SongID"]);
	}
	// M
	private void ScrollSongs()
	{
			// Calculate visible range with larger buffer to reduce flickering
			float viewportHeight = GetViewportRect().Size.Y;
			int visibleItemCount = (int)(viewportHeight / 83) + 4; // Increased buffer items
			int startIndex = Math.Max(0, (int)scrollBar.Value - 2);
			int endIndex = Math.Min(SettingsOperator.Beatmaps.Count, startIndex + visibleItemCount);

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
			Songpp.Text = "+" + (SettingsOperator.Gameplaycfg["maxpp"]*ModsMulti.multiplier).ToString("N0")+"pp";
			SongMapper.Text = "Created by " + SettingsOperator.Sessioncfg["beatmapmapper"]?.ToString() ?? "";
			SongBPM.Text = "BPM - " + ((int)SettingsOperator.Sessioncfg["beatmapbpm"]*AudioPlayer.Instance.PitchScale).ToString("N0") ?? "";
			SongLen.Text = TimeSpan.FromMilliseconds(SettingsOperator.Gameplaycfg["timetotal"]/AudioPlayer.Instance.PitchScale).ToString(@"mm\:ss") ?? "00:00";
			SongAccuracy.Text = "Accuracy Lv. " + ((int)SettingsOperator.Sessioncfg["beatmapaccuracy"]).ToString("00") ?? "Accuracy Lv. 00";


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
	}

	private void SetControlsVisibility(bool visible)
	{
		SongArtist.Visible = visible;
		Songpp.Visible = visible;
		SongMapper.Visible = visible;
		SongLen.Visible = visible;
		SongBPM.Visible = visible;
		SongAccuracy.Visible = visible;
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double _delta)
	{
		if ((bool)SettingsOperator.Sessioncfg["reloaddb"]){
			SettingsOperator.Sessioncfg["reloaddb"] = false;
			GetTree().ReloadCurrentScene();
			
		}
		scrollBar.MaxValue = SettingsOperator.Beatmaps.Count;
		
		if (!AnimationSong){
			AnimationSong = !AnimationSong;
			scrollmode(exactvalue: (int)SettingsOperator.Sessioncfg["SongID"]);
		}
		ScrollSongs();
		checksongpanel();

		if (Input.IsActionJustPressed("Songup")){
			if ((int)SettingsOperator.Sessioncfg["SongID"]-1 >=0 && (int)SettingsOperator.Sessioncfg["SongID"] != -1){
				SettingsOperator.SelectSongID((int)SettingsOperator.Sessioncfg["SongID"]-1);
			} else if ((int)SettingsOperator.Sessioncfg["SongID"] != -1){
				SettingsOperator.SelectSongID((int)SettingsOperator.Beatmaps.Count-1);
			}
			scrollmode(exactvalue: (int)SettingsOperator.Sessioncfg["SongID"]);

		}else if (Input.IsActionJustPressed("Songdown")){
			if ((int)SettingsOperator.Sessioncfg["SongID"]+1 <(int)SettingsOperator.Beatmaps.Count && (int)SettingsOperator.Sessioncfg["SongID"] != -1){
				SettingsOperator.SelectSongID((int)SettingsOperator.Sessioncfg["SongID"]+1);
			} else if ((int)SettingsOperator.Sessioncfg["SongID"] != -1){
				SettingsOperator.SelectSongID(0);
			}
			scrollmode(exactvalue: (int)SettingsOperator.Sessioncfg["SongID"]);

		}else if (Input.IsActionJustPressed("Mod")){
			_Mods_show();
		}else if (Input.IsActionJustPressed("Random")){
			_on_random();
			scrollmode(exactvalue: (int)SettingsOperator.Sessioncfg["SongID"]);

		}else if (Input.IsActionJustPressed("Collections")){

		}else if (Input.IsActionJustPressed("scrolldown") && scrollBar.Value+1 < SettingsOperator.Beatmaps.Count){
			if (scrollvelocity < 0){
				scrollvelocity = 0;
			}
			scrollvelocity = Math.Min(scrollvelocity + 1,2);
			scrollmode(1 + scrollvelocity);
		}else if (Input.IsActionJustPressed("scrollup")&& scrollBar.Value-1 > -1){
			if (scrollvelocity > 0){
				scrollvelocity = 0;
			}
			scrollvelocity = Math.Max(scrollvelocity - 1,-2);
			scrollmode(-1 + scrollvelocity);
		}
		//Debugtext.Text = scrolly.ToString();
	}
	
	private void _on_animation_player_animation_finished(){

	}
	private void _Mods_show(){
		ModScreen.Visible = !ModScreen.Visible;
	}
	private void _Start(){
		//AudioPlayer.Instance.Stop();
        Notify.Post("Game still in beta!, no score submissions yet!");
		GetNode<SceneTransition>("/root/Scene").Switch("res://Panels/Screens/SongLoadingScreen.tscn");
		SettingsOperator.loopaudio = false;
		//
	}
	private void _on_back_pressed(){
		GetNode<SceneTransition>("/root/Scene").Switch("res://Panels/Screens/home_screen.tscn");
	}
}