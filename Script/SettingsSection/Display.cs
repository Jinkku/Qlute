using Godot;
using System;

public partial class Display : PanelContainer
{
	private SettingsOperator SettingsOperator { get; set; }
	public OptionButton Windowmode { get; set; }
	public HSlider BackgroundDim { get; set; }
	private CheckButton ShowUnicode { get; set; }
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		SettingsOperator = GetNode<SettingsOperator>("/root/SettingsOperator");
		BackgroundDim = GetNode<HSlider>("Rows/BackgroundDim");
		ShowUnicode = GetNode<CheckButton>("Rows/OriginalLanguage");
		Windowmode = GetNode<OptionButton>("Rows/WindowSelector");
		ShowUnicode.ButtonPressed = Check.CheckBoolValue(SettingsOperator.GetSetting("showunicode").ToString());
		Windowmode.Selected = int.TryParse(SettingsOperator.GetSetting("windowmode")?.ToString(), out int mode) ? mode : 0;
		BackgroundDim.Value = SettingsOperator.backgrounddim;
	}
	
	private void _backgrounddim_started(float value)
	{
		SettingsOperator.backgrounddim = (int)BackgroundDim.Value;
	}
	private void _backgrounddim_ended(int value)
	{
		SettingsOperator.SetSetting("backgrounddim", BackgroundDim.Value);
	}

	private void _changed_resolution(int index)
	{
		SettingsOperator.changeres(index);
	}
	
	private void _originallanguage()
	{
		SettingsOperator.SetSetting("showunicode", ShowUnicode.ButtonPressed);
		SettingsOperator.ReloadInfo();
	}
}
