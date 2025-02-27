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
	public int scrolly = 0;
	public Texture2D ImageCache {get;set;}
	public string ImageURL {get;set;}
	public PanelContainer ModScreen { get; set; }
	public Label Diff { get; set; }
	public ScrollBar scrollBar { get; set; }
	public PanelContainer Info {get;set;}
	int startposition = 30;
	public void _res_resize(){
		var window_size = GetViewportRect().Size;
		Control SongPanel = GetNode<Control>("SongPanel");
		SongPanel.Size = new Vector2(window_size.X/2.5f, window_size.Y-150);
		SongPanel.Position = new Vector2(window_size.X-(window_size.X/2.5f), 105);
		Info = GetNode<PanelContainer>("SongDetails/Info");
		Info.Size = new Vector2(GetViewportRect().Size.X/2.3f,Info.Size.Y);
	}
	public void _scrolling(){
		scrolly = (int)scrollBar.Value;
		SongETick = 0;
		foreach (Button self in SongEntry)
		{
			var Y = self.Position.Y;
			self.Position = new Vector2(0, startposition+(SongETick*83) - (83*scrolly));
			SongETick++;
		}
	}
	public void AddSongList(string song,string artist,string mapper,int lv,string background,string path,float pp, string difficulty,string audio,Vector2 pos)
	{

		var button = musiccardtemplate.Instantiate();
		SongEntry.Add(button);
		GetNode<Control>("SongPanel").AddChild(button);
		var childButton = button.GetNode<Button>(".");
		var SongTitle = button.GetNode<Label>("./SongTitle");
		var SongArtist = button.GetNode<Label>("./SongArtist");
		var SongMapper = button.GetNode<Label>("./SongMapper");
		var TextureRect = button.GetNode<TextureRect>("./SongBackgroundPreview/BackgroundPreview");
		var Rating = button.GetNode<Label>("./PanelContainer/HBoxContainer/LevelRating/Rating");
		var Version = button.GetNode<Label>("./PanelContainer/HBoxContainer/Difficulty/Version");
		childButton.Position = pos;
		SongTitle.Text = song;
		SongArtist.Text = artist;
		SongMapper.Text = "Created by " + mapper;
		Version.Text = difficulty;
		Rating.Text = "Lv. "+lv.ToString("0");
		childButton.Name = SongETick.ToString();
		childButton.ClipText = true;
		childButton.SetMeta("bg", background);
		childButton.SetMeta("SongID", SongETick);
		if (background != ImageURL){
			ImageCache = SettingsOperator.LoadImage(background);
		}
		TextureRect.Texture = ImageCache;
	}
	private void _on_random(){
		SettingsOperator.SelectSongID(SettingsOperator.RndSongID());
	}
	public override void _Ready()
	{
		SettingsOperator.loopaudio = true;
		scrollBar = GetNode<VScrollBar>("SongPanel/VScrollBar");
		musiccardtemplate = GD.Load<PackedScene>("res://Panels/SongSelectButtons/MusicCard.tscn");
		SongETick = 0;
		//Debugtext = new Label();
		//Debugtext.ZIndex = 1024;
		//Debugtext.Text = "X3";
		//Debugtext.Position = new Vector2(100,100);
		//AddChild(Debugtext);
		Diff = GetNode<Label>("SongDetails/Info/Plasa/Difficulty");
		ModScreen = GetNode<PanelContainer>("ModsScreen");
		SettingsOperator = GetNode<SettingsOperator>("/root/SettingsOperator");
		var timex = DateTime.Now.Second;
		foreach (var song in SettingsOperator.Beatmaps)
		{	
			AddSongList(song["Title"].ToString(),
			song["Artist"].ToString(),
			song["Mapper"].ToString(),
			(int)float.Parse(song["levelrating"].ToString()),
			song["path"].ToString()+song["background"].ToString(),
			song["rawurl"].ToString(),(float)song["pp"]
			,(string)song["Version"],song["path"].ToString()+song["audio"].ToString()
			, new Vector2(0, startposition + (83*SongETick)));
			SongETick++;
		}
		scrolly = (int)SettingsOperator.Sessioncfg["SongID"];
		GD.Print("Finished about " + (DateTime.Now.Second-timex) + "s");
		SongTitle = GetNode<Label>("SongDetails/Info/Plasa/Title");
		SongArtist = GetNode<Label>("SongDetails/Info/Plasa/Artist");
		Songpp = GetNode<Label>("SongDetails/Info/Plasa/Points");
		SongBPM = GetNode<Label>("SongDetails/Info/Plasa/BPM");
		SongLen = GetNode<Label>("SongDetails/Info/Plasa/Length");
		SongMapper = GetNode<Label>("SongDetails/Info/Plasa/Mapper");
		SongAccuracy = GetNode<Label>("SongDetails/Info/Plasa/Accuracy");
		_res_resize();
		_scrolling();
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double _delta)
	{
		if ((bool)SettingsOperator.Sessioncfg["reloaddb"]){
			SettingsOperator.Sessioncfg["reloaddb"] = false;
			GetTree().ReloadCurrentScene();
			
		}
		scrollBar.MaxValue = SettingsOperator.Beatmaps.Count;
		SongTitle.Text = SettingsOperator.Sessioncfg["beatmaptitle"]?.ToString() ?? "No song selected";
		if (SettingsOperator.Sessioncfg["beatmaptitle"] != null){
		SongArtist.Text = SettingsOperator.Sessioncfg["beatmapartist"]?.ToString() ?? "";
		Songpp.Text = "+" + (SettingsOperator.Gameplaycfg["maxpp"]*ModsMulti.multiplier).ToString("N0")+"pp";
		SongMapper.Text = "Created by " + SettingsOperator.Sessioncfg["beatmapmapper"]?.ToString() ?? "";
		SongBPM.Text = "BPM - " + ((int)SettingsOperator.Sessioncfg["beatmapbpm"]*AudioPlayer.Instance.PitchScale).ToString("N0") ?? "";
		SongLen.Text = TimeSpan.FromMilliseconds(SettingsOperator.Gameplaycfg["timetotal"]/AudioPlayer.Instance.PitchScale).ToString(@"mm\:ss") ?? "00:00";
		SongAccuracy.Text = "Accuracy Lv. " + ((int)SettingsOperator.Sessioncfg["beatmapaccuracy"]).ToString("00") ?? "Accuracy Lv. 00";
		SongArtist.Visible = true;
		Songpp.Visible = true;
		SongMapper.Visible = true;
		SongLen.Visible = true;
		SongBPM.Visible = true;
		SongAccuracy.Visible = true;}
		else {
			SongArtist.Visible = false;
			Songpp.Visible = false;
			SongMapper.Visible = false;
			SongAccuracy.Visible = false;
			SongBPM.Visible = false;
			SongLen.Visible = false;
		}
		if (SettingsOperator.Sessioncfg["beatmapdiff"] != null){
		Diff.Text = SettingsOperator.Sessioncfg["beatmapdiff"].ToString();}
		else {
			Diff.Text = "";
		}
		if (Input.IsActionJustPressed("Songup")){
			if ((int)SettingsOperator.Sessioncfg["SongID"]-1 >0 && (int)SettingsOperator.Sessioncfg["SongID"] != -1){
				SettingsOperator.SelectSongID((int)SettingsOperator.Sessioncfg["SongID"]-1);
			} else if ((int)SettingsOperator.Sessioncfg["SongID"] != -1){
				SettingsOperator.SelectSongID((int)SettingsOperator.Beatmaps.Count-1);
			}

		}else if (Input.IsActionJustPressed("Songdown")){
			if ((int)SettingsOperator.Sessioncfg["SongID"]+1 <(int)SettingsOperator.Beatmaps.Count && (int)SettingsOperator.Sessioncfg["SongID"] != -1){
				SettingsOperator.SelectSongID((int)SettingsOperator.Sessioncfg["SongID"]+1);
			} else if ((int)SettingsOperator.Sessioncfg["SongID"] != -1){
				SettingsOperator.SelectSongID(0);
			}

		}else if (Input.IsActionJustPressed("Mod")){
			_Mods_show();
		}else if (Input.IsActionJustPressed("Random")){
			_on_random();

		}else if (Input.IsActionJustPressed("Collections")){

		}else if (Input.IsActionJustPressed("scrolldown") && scrolly+1 < SettingsOperator.Beatmaps.Count){
			scrollBar.Value++;
			_scrolling();
		}else if (Input.IsActionJustPressed("scrollup")&& scrolly-1 > -1){
			scrollBar.Value--;
			_scrolling();
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
		GetTree().ChangeSceneToFile("res://Panels/Screens/SongLoadingScreen.tscn");
		SettingsOperator.loopaudio = false;
		//
	}
	private void _on_back_pressed(){
		GetTree().ChangeSceneToFile("res://Panels/Screens/home_screen.tscn");
	}
}
