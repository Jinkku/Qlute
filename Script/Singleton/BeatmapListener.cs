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

	public void CheckAndExtractOszFiles()
	{
		foreach (string file in Directory.GetFiles(SettingsOperator.downloadsdir, "*.osz"))
		{
			string beatmapDir = Path.Combine(SettingsOperator.beatmapsdir, Path.GetFileNameWithoutExtension(file));
			GD.Print(beatmapDir);
			if (!Directory.Exists(beatmapDir))
			{
				Directory.CreateDirectory(beatmapDir);
			}
			else
			{
				GD.Print("Directory already exists for " + file + ", cancelling extraction and removing file.");
				File.Delete(file);
				continue;
			}
			System.IO.Compression.ZipFile.ExtractToDirectory(file, beatmapDir);
			File.Delete(file);
			GD.Print("Extracted and moved " + file + " to " + beatmapDir);
			GD.Print("Parsing " + beatmapDir);
			Parse_BeatmapDir(beatmapDir);
			GD.Print("Parsed...");
		}
		foreach (string file in Directory.GetFiles(SettingsOperator.downloadsdir, "*.zip")){
			System.IO.Compression.ZipFile.ExtractToDirectory(file, SettingsOperator.downloadsdir);
			File.Delete(file);
			GD.Print("Extracted pack" + file);

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
		CheckAndExtractOszFiles();
	}
}
