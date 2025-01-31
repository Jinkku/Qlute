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
		SettingsOperator = GetNode<SettingsOperator>("/root/SettingsOperator");
		AnimationPlayer animationPlayer = GetNode<AnimationPlayer>("./AnimationPlayer");
		animationPlayer.Play("Intro");
		GD.Print(SettingsOperator.beatmapsdir);
		string[] directories = { SettingsOperator.homedir, SettingsOperator.beatmapsdir, SettingsOperator.downloadsdir, SettingsOperator.replaydir, SettingsOperator.screenshotdir, SettingsOperator.skinsdir };
		foreach (string tmp in directories){
			if (Directory.Exists(tmp)) {
				if (tmp == SettingsOperator.beatmapsdir){
					GD.Print("Checking for beatmaps...");
					foreach (string Dir in Directory.GetDirectories(SettingsOperator.beatmapsdir)){
						GD.Print(Dir);
						foreach (string file in Directory.GetFiles(Dir, "*.osu")) {
							GD.Print(file);
							SettingsOperator.Parse_Beatmapfile(file);
						}
					}
				}
				GD.Print("Found "+ tmp);
			} else {
				GD.Print("Creating "+ tmp);
				System.IO.Directory.CreateDirectory(tmp);
			}


	}}
	public void _intro_finished(){
	}
	private void _on_timer_timeout(){
		GD.Print("Finished loading game...");
		SceneSwitch SceneSwitch = GetNode<SceneSwitch>("/root/SceneSwitch");
		SceneSwitch.GotoScene("res://Panels/Screens/home_screen.tscn");
	}
	public override void _Process(double _delta){
	}		
}
