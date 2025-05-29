using Godot;
using System;


public partial class FpsShow : CheckButton
{
	// Called when the node enters the scene tree for the first time.
	public SettingsOperator SettingsOperator { get; set; }
	public override void _Ready()
	{
		SettingsOperator = GetNode<SettingsOperator>("/root/SettingsOperator");
		ButtonPressed = Check.CheckBoolValue(SettingsOperator.GetSetting("showfps").ToString());
	}

	private void _toggled()
	{
		SettingsOperator.SetSetting("showfps", ButtonPressed);
	}
}
