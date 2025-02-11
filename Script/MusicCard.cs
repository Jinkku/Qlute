using Godot;
using System;

public partial class MusicCard : Button
{
	// Called when the node enters the scene tree for the first time.
	public static SettingsOperator SettingsOperator { get; set; }
	
	public Button self { get ;set; }
	public TextureRect Cover { get; set; }
	public TextureRect Preview { get; set; }
	public int SongID { get; set; }
	public override void _Ready()
	{
		SettingsOperator = GetNode<SettingsOperator>("/root/SettingsOperator");
		self = GetNode<Button>(".");
		Cover = GetTree().Root.GetNode<TextureRect>("Song Select/BeatmapBackground");
		Preview = GetNode<TextureRect>("./SongBackgroundPreview/BackgroundPreview");
	}

	public void _on_draw(){
	}

	public void _on_hidden(){
	}
	
	public void _on_pressed(){
		int songID = (int)self.GetMeta("SongID");
		SettingsOperator.SelectSongID(songID);
	}
	public override void _Process(double _delta)
	{
		self.ButtonPressed = SettingsOperator.Sessioncfg["SongID"].ToString().Equals(self.GetMeta("SongID").ToString());
	}
}
