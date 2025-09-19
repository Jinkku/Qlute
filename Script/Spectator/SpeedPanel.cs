using Godot;
using System;

public partial class SpeedPanel : PanelContainer
{
	private Label SpeedMultiplier { get; set; }
	private HSlider SpeedSlider { get; set; }
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		SpeedMultiplier = GetNode<Label>("Rows/SpeedMulti");
		SpeedSlider = GetNode<HSlider>("Rows/Speed");
	}
	private void CheckSpeedValue()
	{
		SpeedMultiplier.Text = $"{AudioPlayer.Instance.PitchScale.ToString("0.00")}x";
		SpeedSlider.Value = AudioPlayer.Instance.PitchScale;
	}
	private void _speed_change(float value)
	{
		if (AudioPlayer.Instance.Playing)
		{
			AudioPlayer.Instance.PitchScale = value;
		}
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		CheckSpeedValue();
	}
}
