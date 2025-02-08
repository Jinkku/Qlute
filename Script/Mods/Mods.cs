using Godot;
using System;

public partial class Mods : PanelContainer
{
	public static SettingsOperator SettingsOperator { get; set; }
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		SettingsOperator = GetNode<SettingsOperator>("/root/SettingsOperator");
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}
	private void _on_ht_pressed(){
		AudioPlayer.Instance.PitchScale = 0.5f;
		SettingsOperator.Sessioncfg["songspeed"] = 0.5f;
	}
	private void _on_dt_pressed(){
		AudioPlayer.Instance.PitchScale = 1.25f;
		SettingsOperator.Sessioncfg["songspeed"] = 1.25f;
	}
}
