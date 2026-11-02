using Godot;
using System;

public partial class Accuracy : Label
{
	private float _lastAcc = float.NaN;
	private bool _lastEmpty;
	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		bool empty = SettingsOperator.Gameplaycfg.Max + SettingsOperator.Gameplaycfg.Great + SettingsOperator.Gameplaycfg.Meh + SettingsOperator.Gameplaycfg.Bad == 0;
		float acc = SettingsOperator.Gameplaycfg.Accuracy;
		if (empty == _lastEmpty && acc.Equals(_lastAcc)) return;
		_lastEmpty = empty;
		_lastAcc = acc;
		Text = empty ? "100.00%" : acc.ToString("P2");
	}
}
