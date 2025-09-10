using Godot;
using System;

public partial class FpsModes : OptionButton
{
	public override void _Ready()
	{
		// Set the selected index based on the current setting
		int fpsMode = int.TryParse(SettingsOperator.GetSetting("fpsmode")?.ToString(), out int mode) ? mode : 0;
		Selected = fpsMode;
	}
	private void _fpsselected(int index)
	{
		SettingsOperator.SetSetting("fpsmode", index);
		GetNode<SettingsOperator>("/root/SettingsOperator").RefreshFPS();
	}
}
