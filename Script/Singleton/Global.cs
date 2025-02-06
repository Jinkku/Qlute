using Godot;
using System;
using System.IO;
using System.Linq;

public partial class Global : Node
{
	// Called when the node enters the scene tree for the first time.
	public static SettingsOperator SettingsOperator { get; set; }
	public override void _Ready()
	{
		SettingsOperator = GetNode<SettingsOperator>("/root/SettingsOperator");
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		if (Input.IsActionJustPressed("screenshot"))
			{
				var filename = "/screenshot_" + ((int)Directory.GetFiles(SettingsOperator.screenshotdir).Count()+1) + ".jpg";
				var image = GetViewport().GetTexture().GetImage();
				image.SaveJpg(SettingsOperator.screenshotdir + filename);
				GD.Print(filename);
			}
	}
}
