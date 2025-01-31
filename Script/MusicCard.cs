using Godot;
using System;

public partial class MusicCard : Button
{
	// Called when the node enters the scene tree for the first time.
	public static SettingsOperator SettingsOperator { get; set; }
	
	public Button self { get ;set; }
	public TextureRect Cover { get; set; }
	public int SongID { get; set; }
	public override void _Ready()
	{
		SettingsOperator = GetNode<SettingsOperator>("/root/SettingsOperator");
		self = GetNode<Button>(".");
		Cover = GetNode<TextureRect>("../../BeatmapBackground");
		SongID = int.Parse(self.Name);
	}

	
	public void _on_pressed(){
		GD.Print("Pressed");
		var TextureRect = GetNode<TextureRect>("./SongBackgroundPreview/BackgroundPreview");
		Cover.Texture = TextureRect.Texture;
	}
	public override void _Process(double _delta)
	{
		

	}
}
