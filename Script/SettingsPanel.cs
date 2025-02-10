using Godot;
using System;

public partial class SettingsPanel : Control
{
	// Called when the node enters the scene tree for the first time.
	public static SettingsOperator SettingsOperator { get; set; }
	public static OptionButton Windowmode { get; set; }
	public override void _Ready(){
		SettingsOperator = GetNode<SettingsOperator>("/root/SettingsOperator");
		Windowmode = GetNode<OptionButton>("ColorRect/ScrollContainer/VBoxContainer/WindowSelector");
		Windowmode.Selected = int.TryParse(SettingsOperator.GetSetting("windowmode")?.ToString(), out int mode) ? mode : 0;
	}
	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}
	private void _changed_resolution(int index)
	{
		SettingsOperator.changeres(index);
	}
}
