using Godot;
using System;
using System.IO;
using System.Runtime.CompilerServices;
public partial class BeatmapListener : Node
{
	public static SettingsOperator SettingsOperator { get; set; }
	public override void _Ready(){
		SettingsOperator = GetNode<SettingsOperator>("/root/SettingsOperator");
		GD.Print(SettingsOperator.beatmapsdir);
		string[] directories = { SettingsOperator.homedir, SettingsOperator.beatmapsdir, SettingsOperator.downloadsdir, SettingsOperator.replaydir, SettingsOperator.screenshotdir, SettingsOperator.skinsdir };
		foreach (string tmp in directories){
			if (Directory.Exists(tmp)) {
				if (tmp == SettingsOperator.beatmapsdir){
					GD.Print("Checking for beatmaps...");
					foreach (string Dir in Directory.GetDirectories(SettingsOperator.beatmapsdir)){
						GD.Print(Dir);	
						foreach (string file in Directory.GetFiles(Dir, "*.osu")) {
							//GD.Print(file);
							SettingsOperator.Parse_Beatmapfile(file);
							SettingsOperator.Sessioncfg["reloadbdb"] = true;
						}
					}
				}
				GD.Print("Found "+ tmp);
			} else {
				GD.Print("Creating "+ tmp);
				System.IO.Directory.CreateDirectory(tmp);
			}


	}
//		foreach (string Dir in SettingsOperator.BeatmapsURLs){
//			GD.Print("Scratching..." + Dir);
//		}
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		
	}
}
