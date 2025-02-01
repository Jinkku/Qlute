using Godot;
using System;

public partial class BeatmapBackground : TextureRect
{
	// Called when the node enters the scene tree for the first time.
	public static SettingsOperator SettingsOperator { get; set; }
	public TextureRect self { get ;set; }
	public override void _Ready()
	{
		SettingsOperator = GetNode<SettingsOperator>("/root/SettingsOperator");
		self = GetNode<TextureRect>(".");
		Texture2D bg = (Texture2D)SettingsOperator.Sessioncfg["background"];
		if (bg != null){
		self.Texture = bg;
		};
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double _delta)
	{
	}
}
