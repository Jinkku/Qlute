using Godot;
using System;

public partial class Accuracy : Label
{
	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		var Acc = SettingsOperator.Gameplaycfg.Accuracy.ToString("P2");
		if (SettingsOperator.Gameplaycfg.Max + SettingsOperator.Gameplaycfg.Great + SettingsOperator.Gameplaycfg.Meh + SettingsOperator.Gameplaycfg.Bad == 0){
			Acc = "100.00%";
		}
		Text = Acc;
	}
}
