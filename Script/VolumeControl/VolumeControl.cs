using Godot;
using System;

public partial class VolumeControl : PanelContainer
{
	// Called when the node enters the scene tree for the first time.
	private HSlider MasterSlider;
	private HSlider MusicSlider;
	private HSlider SampleSlider;
	private Label MasterLabel;
	private Label SampleLabel;
	public SettingsOperator SettingsOperator { get; set; }
	public override void _Ready()
	{
		SettingsOperator = GetNode<SettingsOperator>("/root/SettingsOperator");
		MasterSlider = GetNode<HSlider>("VBoxContainer/MASTER");
		MasterLabel = GetNode<Label>("VBoxContainer/Music");
		//MusicSlider = GetNode<HSlider>("VBoxContainer/MUSIC");
		SampleSlider = GetNode<HSlider>("VBoxContainer/EFFECTS");
		SampleLabel = GetNode<Label>("VBoxContainer/Sample");
		var samplev = SettingsOperator.SampleVol;
		var masterv = SettingsOperator.MasterVol;
		SampleSlider.Value = samplev;
		//MusicSlider.Value = Math.Pow(10, masterv / 10) * 100;
		MasterSlider.Value = masterv;
	}

	private void _master(float value)
	{
		AudioPlayer.Instance.VolumeDb = (int)(Math.Log10(value / 100) * 20) - 5; // -5 to adjust the volume to a more NOT loud level and cap it
		MasterLabel.Text = "Music " + (int)value + "%";
		SettingsOperator.SetSetting("master", (int)value);
		SettingsOperator.MasterVol = (int)value;
	}
	private void _sample(float value)
	{
		Sample.Instance.VolumeDb = (int)(Math.Log10(value / 100) * 20) - 5;
		SampleLabel.Text = "Sample " + (int)value + "%";
		SettingsOperator.SetSetting("sample", (int)value);
		SettingsOperator.SampleVol = (int)value;
	}

}
