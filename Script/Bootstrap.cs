using Godot;
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;

public partial class Bootstrap : Control
{
	// Called when the node enters the scene tree for the first time.
	int waittime = DateTime.Now.Second;
	public static SettingsOperator SettingsOperator { get; set; }
	public override void _Ready()
	{		
		SettingsOperator = GetNode<SettingsOperator>("/root/SettingsOperator");

		GD.Print(SettingsOperator.beatmapsdir);
		string[] directories = { SettingsOperator.homedir, SettingsOperator.beatmapsdir, SettingsOperator.downloadsdir, SettingsOperator.replaydir, SettingsOperator.screenshotdir, SettingsOperator.skinsdir };
		foreach (string tmp in directories){
			if (Directory.Exists(tmp)) {
				if (tmp == SettingsOperator.beatmapsdir){
					GD.Print("Checking for beatmaps...");
					foreach (string Dir in Directory.GetDirectories(SettingsOperator.beatmapsdir)){
						GD.Print(Dir);
						SettingsOperator.BeatmapsURLs.Add(Dir);
						foreach (string file in Directory.GetFiles(Dir, "*.osu")) {
							GD.Print(file);
							SettingsOperator.Beatmaps.Add(file);
						}
					}
				}
				GD.Print("Found "+ tmp);
			} else {
				GD.Print("Creating "+ tmp);
				System.IO.Directory.CreateDirectory(tmp);
			}


	}}
	public override void _Process(double delta){
		ProgressBar Barz = GetNode<ProgressBar>("ProgressBar");
		var SceneSwitch = GetNode<SceneSwitch>("/root/SceneSwitch");
		Barz.Value = ((DateTime.Now.Second - waittime)/3f)*100;
		if (DateTime.Now.Second - waittime >= 0.5) {
			GD.Print("Finished loading game...");
			SceneSwitch.GotoScene("res://Panels/Screens/home_screen.tscn");
			

		}

	}		
}
