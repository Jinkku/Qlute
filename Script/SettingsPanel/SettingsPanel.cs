using Godot;
using System;

public partial class SettingsPanel : Control
{
	// Called when the node enters the scene tree for the first time.
	public static SettingsOperator SettingsOperator { get; set; }
	public static OptionButton Windowmode { get; set; }
	public static HSlider BackgroundDim { get; set; }
	public static Button OffsetButton { get; set; }
	public static HSlider OffsetSlider { get; set; }
	public static Label OffsetTicker { get; set; }
	public override void _Ready(){
		SettingsOperator = GetNode<SettingsOperator>("/root/SettingsOperator");
		Windowmode = GetNode<OptionButton>("ColorRect/ScrollContainer/VBoxContainer/WindowSelector");
		Windowmode.Selected = int.TryParse(SettingsOperator.GetSetting("windowmode")?.ToString(), out int mode) ? mode : 0;
		BackgroundDim = GetNode<HSlider>("ColorRect/ScrollContainer/VBoxContainer/BackgroundDim");
		OffsetButton = GetNode<Button>("ColorRect/ScrollContainer/VBoxContainer/AudioOffsetAuto");
		OffsetSlider = GetNode<HSlider>("ColorRect/ScrollContainer/VBoxContainer/AudioOffset");
		OffsetTicker = GetNode<Label>("ColorRect/ScrollContainer/VBoxContainer/AudioNotice2");
		BackgroundDim.Value = SettingsOperator.backgrounddim;
		OffsetButton.Text = "Set offset by last played song (" + SettingsOperator.Getms() + "ms)";
		var offset = SettingsOperator.GetSetting("audiooffset") != null ? float.Parse(SettingsOperator.GetSetting("audiooffset").ToString()) : 0;
		OffsetSlider.Value = 200-offset;
	}
	// Called every frame. 'delta' is the elapsed time since the previous frame.
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
