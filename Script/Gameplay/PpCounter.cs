using Godot;
using System;

public partial class PpCounter : Label
{
	private int _last = int.MinValue;
	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		int pp = (int)SettingsOperator.Gameplaycfg.pp;
		if (pp == _last) return;
		_last = pp;
		Text = pp.ToString("N0") + "pp";
	}
}
