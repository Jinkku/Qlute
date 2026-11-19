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
		SetupScrollSpeed();
		AddToggle("Show beatmap storyboards", StoryboardPlayer.SettingKey);
		AddToggle("Show beatmap shaders", BeatmapShaderLayer.SettingKey);
		AddToggle("Pause when the game window loses focus", FocusPause.SettingKey);
	}

	/// <summary>A setting switch placed right under the "Fade out when on break" one.</summary>
	private void AddToggle(string text, string key)
	{
		var toggle = new CheckButton
		{
			Text = text,
			ButtonPressed = Check.CheckBoolValue(SettingsOperator.GetSetting(key)?.ToString() ?? "true"),
		};
		NFadeout.GetParent().AddChild(toggle);
		NFadeout.GetParent().MoveChild(toggle, NFadeout.GetIndex() + 1 + _added++);
		toggle.Toggled += on => SettingsOperator.SetSetting(key, on);
	}
	private int _added;

	private HSlider _scrollSpeed;
	private Label _scrollSpeedValue;

	private void SetupScrollSpeed()
	{
		_scrollSpeed = GetNode<HSlider>("Rows/ScrollSpeedRow/ScrollSpeed");
		_scrollSpeedValue = GetNode<Label>("Rows/ScrollSpeedRow/ScrollSpeedValue");
		_scrollSpeed.MinValue = ScrollSpeed.Min;
		_scrollSpeed.MaxValue = ScrollSpeed.Max;
		_scrollSpeed.SetValueNoSignal(ScrollSpeed.Value);
		ShowScrollSpeed(ScrollSpeed.Value, 0);
		_scrollSpeed.ValueChanged += v => { if (!Mathf.IsEqualApprox((float)v, ScrollSpeed.Value)) ScrollSpeed.Set((float)v); };
		ScrollSpeed.Changed += ShowScrollSpeed; // F3 / F4 while this is open
		TreeExiting += () => ScrollSpeed.Changed -= ShowScrollSpeed;
	}

	private void ShowScrollSpeed(float speed, int direction)
	{
		_scrollSpeed.SetValueNoSignal(speed);
		_scrollSpeedValue.Text = $"{speed:0} · {ScrollSpeed.MaxTimeRange / speed:0} ms";
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
