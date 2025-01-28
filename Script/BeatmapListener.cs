using Godot;
using System;
using System.IO;
using System.Runtime.CompilerServices;
public partial class BeatmapListener : Node
{
	public string homedir = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile) + "/.qlute";
	public string beatmapsdir => homedir + "/beatmaps";
	public static SettingsOperator SettingsOperator { get; set; }
	public override void _Ready(){
		SettingsOperator = GetNode<SettingsOperator>("/root/SettingsOperator");
		foreach (string Dir in SettingsOperator.BeatmapsURLs){
			GD.Print("Scratching..." + Dir);
		}
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		
	}
}
