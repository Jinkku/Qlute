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
		var masterv = AudioPlayer.MasterVol;
		SampleSlider.Value = samplev;
		GD.Print(AudioPlayer.Instance.VolumeDb);
		//MusicSlider.Value = Math.Pow(10, masterv / 10) * 100;
		MasterSlider.Value = masterv;
	}
	
	private void _master(float value)
	{
		AudioPlayer.Instance.VolumeDb = AudioPlayer.ToDB(value);
		AudioPlayer.MasterVol = (int)value;
		MasterLabel.Text = "Music " + (int)value + "%";
		SettingsOperator.SetSetting("master", (int)value);
	}
	private void _sample(float value)
	{
		SampleLabel.Text = "Sample " + (int)value + "%";
		SettingsOperator.SetSetting("sample", (int)value);
		SettingsOperator.SampleVol = (int)value;
	}
}
