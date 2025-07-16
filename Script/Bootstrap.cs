using Godot;
using System;
using System.Security.Cryptography.X509Certificates;

public partial class Bootstrap : Control
{
	public  SettingsOperator SettingsOperator { get; set; }
	public Control Home {get;set;}
	public override void _Ready()
	{		
		AnimationPlayer animationPlayer = GetNode<AnimationPlayer>("./AnimationPlayer");
		SettingsOperator = GetNode<SettingsOperator>("/root/SettingsOperator");
		animationPlayer.Play("Intro");
		SettingsOperator.Sessioncfg["toppanelhide"] = true;
	}
	public void _intro_finished(string animationame){
		if (SettingsOperator.Beatmaps.Count>0) {
			SettingsOperator.SelectSongID(SettingsOperator.RndSongID());
		}
		SettingsOperator.toppaneltoggle();
		GetNode<SceneTransition>("/root/Transition").Switch("res://Panels/Screens/home_screen.tscn");
	}
}
