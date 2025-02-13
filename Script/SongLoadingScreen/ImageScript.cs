 using Godot;
using System;

public partial class ImageScript : TextureRect
{
	// Called when the node enters the scene tree for the first time.
	public SettingsOperator SettingsOperator {get;set;}
	public override void _Ready()
	{
		SettingsOperator = GetNode<SettingsOperator>("/root/SettingsOperator");
		Texture = (Texture2D)SettingsOperator.Sessioncfg["background"];
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}
}
