using Godot;
using System;

public partial class Display : PanelContainer
{
	private SettingsOperator SettingsOperator { get; set; }
	public OptionButton Windowmode { get; set; }
	public HSlider BackgroundDim { get; set; }
	private CheckButton ShowUnicode { get; set; }
	private CheckButton NFlashing { get; set; }
	private CheckButton NDarkening { get; set; }
	private CheckButton NFadeout { get; set; }
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		SettingsOperator = GetNode<SettingsOperator>("/root/SettingsOperator");
		BackgroundDim = GetNode<HSlider>("Rows/BackgroundDim");
		ShowUnicode = GetNode<CheckButton>("Rows/OriginalLanguage");
		NFlashing = GetNode<CheckButton>("Rows/Flashing");
		NDarkening = GetNode<CheckButton>("Rows/Darkening");
		NFadeout = GetNode<CheckButton>("Rows/Fadeout");
		Windowmode = GetNode<OptionButton>("Rows/WindowSelector");
		ShowUnicode.ButtonPressed = Check.CheckBoolValue(SettingsOperator.GetSetting("showunicode").ToString());
		NFlashing.ButtonPressed = Check.CheckBoolValue(SettingsOperator.GetSetting("dflashing").ToString());
		NDarkening.ButtonPressed = Check.CheckBoolValue(SettingsOperator.GetSetting("ddarkening").ToString());
		NFadeout.ButtonPressed = Check.CheckBoolValue(SettingsOperator.GetSetting("dfadeout").ToString());
		Windowmode.Selected = int.TryParse(SettingsOperator.GetSetting("windowmode")?.ToString(), out int mode) ? mode : 0;
		BackgroundDim.Value = SettingsOperator.SessionConfig.Backgrounddim;
	}
	
	private void _backgrounddim_started(float value)
	{
		SettingsOperator.SessionConfig.Backgrounddim = (int)BackgroundDim.Value;
	}
	private void _backgrounddim_ended(int value)
	{
		SettingsOperator.SetSetting("backgrounddim", BackgroundDim.Value);
	}

	private void _changed_resolution(int index)
	{
		SettingsOperator.changeres(index);
	}

	private void Flashing()
	{
		SettingsOperator.SetSetting("dflashing", NFlashing.ButtonPressed);
	}

	private void Darkening()
	{
		SettingsOperator.SetSetting("ddarkening", NDarkening.ButtonPressed);
	}

	private void Fadeout()
	{
		SettingsOperator.SetSetting("dfadeout", NFadeout.ButtonPressed);
	}
	
	private void _originallanguage()
	{
		SettingsOperator.SetSetting("showunicode", ShowUnicode.ButtonPressed);
		SettingsOperator.ReloadInfo();
	}
}
