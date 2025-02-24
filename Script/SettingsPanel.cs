using Godot;
using System;

public partial class SettingsPanel : Control
{
	// Called when the node enters the scene tree for the first time.
	public static SettingsOperator SettingsOperator { get; set; }
	public static OptionButton Windowmode { get; set; }
	public static HSlider BackgroundDim { get; set; }
	public static CheckButton SongUnicode { get; set; }
	public override void _Ready(){
		SettingsOperator = GetNode<SettingsOperator>("/root/SettingsOperator");
		Windowmode = GetNode<OptionButton>("ColorRect/ScrollContainer/VBoxContainer/WindowSelector");
		Windowmode.Selected = int.TryParse(SettingsOperator.GetSetting("windowmode")?.ToString(), out int mode) ? mode : 0;
		BackgroundDim = GetNode<HSlider>("ColorRect/ScrollContainer/VBoxContainer/BackgroundDim");
		SongUnicode = GetNode<CheckButton>("ColorRect/ScrollContainer/VBoxContainer/Unicode");
		bool songUnicodeSetting = (bool)SettingsOperator.GetSetting("songunicode") as bool? ?? false;
		GD.Print(SettingsOperator.GetSetting("songunicode"),songUnicodeSetting);
		SongUnicode.ButtonPressed = songUnicodeSetting;
		BackgroundDim.Value = SettingsOperator.backgrounddim;
	}
	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}
	private void _on_unicode(){
		SettingsOperator.SetSetting("songunicode",SongUnicode.ButtonPressed);
	}
	private void _changed_resolution(int index)
	{
		SettingsOperator.changeres(index);
	}
	private void _backgrounddim_started(float value)
	{
		SettingsOperator.backgrounddim = (int)BackgroundDim.Value;
		
	}
	private void _backgrounddim_ended(int value)
	{
		SettingsOperator.SetSetting("backgrounddim",BackgroundDim.Value.ToString());
		
	}
}
