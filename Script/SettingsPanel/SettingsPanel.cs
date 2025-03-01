using Godot;
using System;

public partial class SettingsPanel : Control
{
	// Called when the node enters the scene tree for the first time.
	public static SettingsOperator SettingsOperator { get; set; }
	public OptionButton Windowmode { get; set; }
	public HSlider BackgroundDim { get; set; }
	public Button OffsetButton { get; set; }
	public HSlider OffsetSlider { get; set; }
	public Label OffsetTicker { get; set; }
	public ScrollContainer Scrolls { get; set; }
	public override void _Ready(){
		SettingsOperator = GetNode<SettingsOperator>("/root/SettingsOperator");
		Windowmode = GetNode<OptionButton>("ColorRect/Panels/Scroll/Sections/WindowSelector");
		Windowmode.Selected = int.TryParse(SettingsOperator.GetSetting("windowmode")?.ToString(), out int mode) ? mode : 0;
		BackgroundDim = GetNode<HSlider>("ColorRect/Panels/Scroll/Sections/BackgroundDim");
		OffsetButton = GetNode<Button>("ColorRect/Panels/Scroll/Sections/AudioOffsetAuto");
		OffsetSlider = GetNode<HSlider>("ColorRect/Panels/Scroll/Sections/AudioOffset");
		OffsetTicker = GetNode<Label>("ColorRect/Panels/Scroll/Sections/AudioNotice2");
		Scrolls = GetNode<ScrollContainer>("ColorRect/Panels/Scroll");
		GetNode<Label>("ColorRect/Panels/Scroll/Sections/GodotEngineVersion").Text = $"Godot Version {Engine.GetVersionInfo()["major"]}.{Engine.GetVersionInfo()["minor"]}";
		BackgroundDim.Value = SettingsOperator.backgrounddim;
		OffsetButton.Text = "Set offset by last played song (" + SettingsOperator.Getms() + "ms)";
		var offset = SettingsOperator.GetSetting("audiooffset") != null ? float.Parse(SettingsOperator.GetSetting("audiooffset").ToString()) : 0;
		OffsetSlider.Value = 200-offset;
	}
	// Called every frame. 'delta' is the elapsed time since the previous frame.
	private void _display(){
		Scrolls.GetVScrollBar().Value = GetNode<Label>("ColorRect/Panels/Scroll/Sections/Display").Position.Y;
	}
	private void _audio(){
		Scrolls.GetVScrollBar().Value = GetNode<Label>("ColorRect/Panels/Scroll/Sections/Audio").Position.Y;
	}
	private void _debug(){
		Scrolls.GetVScrollBar().Value = GetNode<Label>("ColorRect/Panels/Scroll/Sections/Debug").Position.Y;
	}
	public override void _Process(double delta)
	{

		Size = new Vector2(Size.X,GetViewportRect().Size.Y-(GetNode<ColorRect>("..").Position.Y+50));
	}
	private void _changed_resolution(int index)
	{
		SettingsOperator.changeres(index);
	}
	private void _on_audio_offset_value_changed(float value)
	{
		SettingsOperator.SetSetting("audiooffset",200-value);
		OffsetTicker.Text = "Audio offset - "+ (OffsetSlider.Value-200).ToString("0") +"ms";
	}
	private void _aow(){
		GetTree().ChangeSceneToFile("res://Panels/Screens/AudioOffset.tscn");
	}private void _aoautoset(){
		SettingsOperator.SetSetting("audiooffset",SettingsOperator.Getms());
		OffsetSlider.Value = 200+SettingsOperator.Getms();
		OffsetTicker.Text = "Audio offset - "+ (OffsetSlider.Value-200).ToString("0") +"ms";
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
