using Godot;
using System;

public partial class Bootstrap : Control
{
	public static SettingsOperator SettingsOperator { get; set; }
	public override void _Ready()
	{		
		AnimationPlayer animationPlayer = GetNode<AnimationPlayer>("./AnimationPlayer");
		SettingsOperator = GetNode<SettingsOperator>("/root/SettingsOperator");
		animationPlayer.Play("Intro");
		SettingsOperator.Sessioncfg["toppanelhide"] = true;
		SettingsOperator.SelectSongID(SettingsOperator.RndSongID());
	}
	public void _intro_finished(string animationame){
		GetTree().ChangeSceneToFile("res://Panels/Screens/home_screen.tscn");
		SettingsOperator.toppaneltoggle();
	}
}
