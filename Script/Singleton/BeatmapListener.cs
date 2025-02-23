using Godot;
using System;
using System.IO;
using System.Runtime.CompilerServices;
public partial class BeatmapListener : Node
{
	public static SettingsOperator SettingsOperator { get; set; }
	public void Parse_BeatmapDir(string dir)
	{
		var files = Directory.GetFiles(dir, "*.osu");
		Array.Sort(files, (x, y) => new FileInfo(x).Length.CompareTo(new FileInfo(y).Length));
		foreach (string file in files)
		{
			//GD.Print(file);
			SettingsOperator.Parse_Beatmapfile(file);
			SettingsOperator.Sessioncfg["reloadbdb"] = true;
		}
	}
	public override void _Ready()
	{
		SettingsOperator = GetNode<SettingsOperator>("/root/SettingsOperator");
		GD.Print(SettingsOperator.beatmapsdir);
		string[] directories = { SettingsOperator.homedir, SettingsOperator.beatmapsdir, SettingsOperator.downloadsdir, SettingsOperator.replaydir, SettingsOperator.screenshotdir, SettingsOperator.skinsdir };
		foreach (string tmp in directories)
		{
			if (Directory.Exists(tmp))
			{
				if (tmp == SettingsOperator.beatmapsdir)
				{
					GD.Print("Checking for beatmaps...");
					foreach (string Dir in Directory.GetDirectories(SettingsOperator.beatmapsdir))
					{
						GD.Print(Dir);
						Parse_BeatmapDir(Dir);
					}
				}
				GD.Print("Found " + tmp);
			}
			else
			{
				GD.Print("Creating " + tmp);
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
		foreach (string beatmapfile in Directory.GetFiles(SettingsOperator.downloadsdir))
		{

		}
	}
}
