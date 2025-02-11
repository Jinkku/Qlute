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
		_Process(0);
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double _delta)
	{
		Vector2 mousePos = GetViewport().GetMousePosition();
        Vector2 screenSize = GetViewportRect().Size;

        float offsetX = (mousePos.X / screenSize.X * 10) - 10;
        float offsetY = (mousePos.Y / screenSize.Y * 10) - 10;

        Position = new Vector2(offsetX, offsetY);

		if (Texture != SettingsOperator.Sessioncfg["background"]){
			Texture = (Texture2D)SettingsOperator.Sessioncfg["background"];
			Size = new Vector2(GetViewportRect().Size[0]+20,GetViewportRect().Size[1]+20);
			Position = new Vector2(GetViewportRect().Size[0]-5,GetViewportRect().Size[1]-5);
			GD.Print("a");
		}
	}
}
