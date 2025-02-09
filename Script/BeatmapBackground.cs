using Godot;
using System;

public partial class BeatmapBackground : TextureRect
{
	// Called when the node enters the scene tree for the first time.
	public static SettingsOperator SettingsOperator { get; set; }
	public TextureRect self { get ;set; }
	Texture2D bg {get;set;}
	public override void _Ready()
	{
		SettingsOperator = GetNode<SettingsOperator>("/root/SettingsOperator");
		self = GetNode<TextureRect>(".");
		bg = (Texture2D)SettingsOperator.Sessioncfg["background"];
		if (bg != null){
		self.Texture = bg;
		};
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double _delta)
	{
		Vector2 mousePos = GetViewport().GetMousePosition();
		Vector2 screensize = GetViewportRect().Size;
		self.Position = new Vector2((mousePos[0]/screensize.X*10)-10, (mousePos[1]/screensize.Y*10)-10);

	}
}
