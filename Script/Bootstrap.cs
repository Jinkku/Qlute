using Godot;
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;

public partial class Bootstrap : Control
{
	public static SettingsOperator SettingsOperator { get; set; }
	public override void _Ready()
	{		
		AnimationPlayer animationPlayer = GetNode<AnimationPlayer>("./AnimationPlayer");
		animationPlayer.Play("Intro");
	}
	public void _intro_finished(string animationame){
		SceneSwitch SceneSwitch = GetNode<SceneSwitch>("/root/SceneSwitch");
		SceneSwitch.GotoScene("res://Panels/Screens/home_screen.tscn");
	}
}
