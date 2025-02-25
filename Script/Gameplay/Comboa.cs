using Godot;
using System;

public partial class Comboa : Label
{
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		_Process(0);
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		if (SettingsOperator.Gameplaycfg["combo"] == 0){
			Visible = false;
		} else{
			Visible = true;
			Text = SettingsOperator.Gameplaycfg["combo"].ToString("N0");
		}
	}
}
