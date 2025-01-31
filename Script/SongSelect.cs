using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class SongSelect : Control
{
	// Called when the node enters the scene tree for the first time.
	 [Export] private TextureRect textureRect;
	public static SettingsOperator SettingsOperator { get; set; }
	public List<object> SongEntry = new List<object>();
	public int SongETick { get; set; }
	public void _res_resize(){
		var window_size = GetViewportRect().Size;
		Control SongPanel = GetNode<Control>("SongPanel");
		SongPanel.Size = new Vector2(window_size.X/3, window_size.Y);
		SongPanel.Position = new Vector2(window_size.X-(window_size.X/3), 0);
		foreach (Button self in SongEntry)
		{
			var Y = self.Position.Y;
			self.SetPosition(new Vector2(0,Y));
			self.Size = new Vector2(window_size.X/3, 115);

		}
	}
	public void AddSongList(string song,string artist,string mapper,int lv,string background,string path,Vector2 pos)
	{
		var button = GD.Load<PackedScene>("res://Panels/SongSelectButtons/MusicCard.tscn").Instantiate();
		SongEntry.Add(button);
		GetNode<Control>("SongPanel").AddChild(button);
		var childButton = button.GetNode<Button>(".");
		var SongTitle = button.GetNode<Label>("./SongTitle");
		var SongArtist = button.GetNode<Label>("./SongArtist");
		var SongMapper = button.GetNode<Label>("./SongMapper");
		var TextureRect = button.GetNode<TextureRect>("./SongBackgroundPreview/BackgroundPreview");
		var Rating = button.GetNode<Label>("./LevelRating/Rating");
		childButton.Position = pos;
		childButton.Size = new Vector2(320, 115);
		SongTitle.Text = song;
		SongArtist.Text = artist;
		SongMapper.Text = mapper;
		Rating.Text = "Lv. "+lv.ToString("0");
		childButton.Name = SongETick.ToString();
		childButton.ClipText = true;
		childButton.SetMeta("beatmapurl", path);
		TextureRect.Texture = SettingsOperator.LoadImage(background);

	}
	public override void _Ready()
	{
		SongETick = 0;
		SettingsOperator = GetNode<SettingsOperator>("/root/SettingsOperator");
		foreach (var song in SettingsOperator.Beatmaps)

		{
			GD.Print(song["Title"]);
			//" ("+song["pp"].ToString()+"pp)" \nDiff:"+song["Version"].ToString()
			AddSongList(song["Title"].ToString(),song["Artist"].ToString(),"mapped by " + song["Mapper"].ToString(),(int)float.Parse(song["levelrating"].ToString()),song["path"].ToString()+song["background"].ToString(),song["rawurl"].ToString(), new Vector2(0, 50+ (115*SongETick)));
			SongETick++;
		}
		_res_resize();
		//AnimationPlayer AniPlayer = GetNode<AnimationPlayer>("AnimationPlayer");
		//AniPlayer.Play("RESET");
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double _delta)
	{
	}
	
	private void _on_animation_player_animation_finished(){

	}
	private void _Start(){
		//AnimationPlayer AniPlayer = GetNode<AnimationPlayer>("AnimationPlayer");
		//AniPlayer.Play("SongSelect-Start");
	}
	private void _on_back_pressed(){
		GetTree().ChangeSceneToFile("res://Panels/Screens/home_screen.tscn");
	}
}
