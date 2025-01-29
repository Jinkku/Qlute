using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class SongSelect : Control
{
	// Called when the node enters the scene tree for the first time.
	
	public static SettingsOperator SettingsOperator { get; set; }
	public List<object> SongEntry = new List<object>();
	public void AddSongList(string song,Vector2 pos)
	{
		var button = new Button();
		SongEntry.Add(button);
		AddChild(button);
		button.SetPosition(pos);
		button.SetSize(new Vector2(320, 115));
		button.Text = song;
		button.Name = song;
		button.ClipText = true;


	}
	public override void _Ready()
	{
		int SongEtick = 0;
		SettingsOperator = GetNode<SettingsOperator>("/root/SettingsOperator");
		foreach (var song in SettingsOperator.Beatmaps)

		{
			GD.Print(song["Title"]);
			AddSongList(song["Artist"].ToString()+" - "+song["Title"].ToString()+"\nDiff:"+song["Version"].ToString()+" ("+song["pp"].ToString()+"pp)", new Vector2(0, 50+ (115*SongEtick)));
			SongEtick++;
		}
		//AnimationPlayer AniPlayer = GetNode<AnimationPlayer>("AnimationPlayer");
		//AniPlayer.Play("RESET");
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double _delta)
	{
		var window_size = GetViewportRect().Size.X;
		foreach (Button self in SongEntry)
		{
			var Y = self.Position.Y;
			self.SetPosition(new Vector2(window_size-self.Size.X-8,Y));

		}
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
