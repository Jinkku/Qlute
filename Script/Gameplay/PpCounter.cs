using Godot;
using System;

public partial class PpCounter : Label
{

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		Text = ((int)SettingsOperator.Gameplaycfg.pp).ToString("N0") + "pp";
	}
}
