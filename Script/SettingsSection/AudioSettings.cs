using Godot;
using System;

public partial class AudioSettings : PanelContainer
{
	private SettingsOperator SettingsOperator { get; set; }
	public ButtonFade OffsetButton { get; set; }
	public HSlider OffsetSlider { get; set; }
	public Label OffsetTicker { get; set; }
	public Label AoaTicker { get; set; }
	private float MS { get; set; }
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		OffsetButton = GetNode<ButtonFade>("Rows/AudioOffsetAuto");
		OffsetSlider = GetNode<HSlider>("Rows/AudioOffset");
		OffsetTicker = GetNode<Label>("Rows/AudioTicker");
		AoaTicker = GetNode<Label>("Rows/AoaTicker");
		var offset = float.Parse(SettingsOperator.GetSetting("audiooffset").ToString());
		if (offset != 0)
		{
			offset = SettingsOperator.GetSetting("audiooffset") != null ? offset : 0;
		}
		else
		{
			offset = 0;
		}
		OffsetSlider.Value = 200 - offset;
		OffsetTicker.Text = "Audio offset - " + (OffsetSlider.Value - 200).ToString("N0") + "ms";
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		MS = SettingsOperator.Getms();
		if (float.IsNaN(MS))
		{
			MS = -1;
			AoaTicker.Text = "Please play a map before you can set your offset!!";
			OffsetButton.StringText = "Play first!";
			OffsetButton.Disabled = true;
		}
		else
		{
			AoaTicker.Text = "your offset from last played song is (" + MS.ToString("N0") + "ms)";
			OffsetButton.StringText = "Set Offset";
			OffsetButton.Disabled = false;
		}
	}
	private void _on_audio_offset_value_changed(float value)
	{
		SettingsOperator.SetSetting("audiooffset", 200 - value);
		OffsetTicker.Text = "Audio offset - " + (value - 200).ToString("N0") + "ms";
	}
	private void _aoautoset()
	{
		SettingsOperator.SetSetting("audiooffset", (int)MS);
		OffsetSlider.Value = 200 + MS;
		OffsetTicker.Text = "Audio offset - " + (OffsetSlider.Value - 200).ToString("N0") + "ms";
	}
	private void _aow()
	{
		GetNode<SceneTransition>("/root/Transition").Switch("res://Panels/Screens/AudioOffset.tscn");
	}
}
