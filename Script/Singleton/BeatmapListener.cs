using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

public class BeatmapLegend
{
	public int ID { get; set; } = 0;
	public int SetID { get; set; } = 0;
	public string Title { get; set; } = null;
	public string Artist { get; set; } = null;
	public string Mapper { get; set; } = null;
	public int KeyCount { get; set; } = 4;
	public string Version { get; set; } = null;
	public double pp { get; set; } = 0.0;
	public int Osubeatid { get; set; } = -1; // osu!mania compatibility
	public int Osubeatidset { get; set; } = -1;
	public int Beatid { get; set; } = -1; // Beatmap ID for Qlute-made beatmaps
	public int Beatidset { get; set; } = -1; // Beatmap ID for Qlute-made beatmaps
	public float Bpm { get; set; } = 0.0f;
	public List<DanceCounter> Dance { get; set; } = new List<DanceCounter>();
	public float Timetotal { get; set; } = 0; // in milliseconds
	public float Levelrating { get; set; } = 0.0f; // Level rating for the beatmap
	public double Accuracy { get; set; } = 0.0; // Accuracy Level for the beatmap (not been used yet)
	public string Background { get; set; } = null; // Background image path
	public string Audio { get; set; } = null; // Audio file path
	public string Rawurl { get; set; } = null; // Raw URL for the beatmap, used for downloading
	public string Path { get; set; } = null; // Path for the beatmap
}
public partial class BeatmapListener : Node
{
	private SettingsOperator SettingsOperator { get; set; }
	public int SetID { get; set; } = 0; // Set ID for the beatmap, used for grouping beatmaps together
	public void Parse_BeatmapDir(string dir)
	{
		var files = Directory.GetFiles(dir, "*.osu");
		Array.Sort(files, (x, y) => new FileInfo(x).Length.CompareTo(new FileInfo(y).Length));
		foreach (string file in files)
		{
			SettingsOperator.Parse_Beatmapfile(file.Replace("\\","/"),SetID: SetID); // Parse the beatmap file and add it to the beatmaps list
			SettingsOperator.Sessioncfg["reloaddb"] = true;
		}
		SetID++; // Increment SetID for each beatmap directory parsed
	}

	public void CheckAndExtractOszFiles()
	{
		foreach (string file in Directory.GetFiles(SettingsOperator.downloadsdir, "*.osz"))
		{
			string beatmapDir = Path.Combine(SettingsOperator.beatmapsdir, Path.GetFileNameWithoutExtension(file));
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
			Notify.Post("Imported " + Path.GetFileName(file));
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
		string[] directories = { SettingsOperator.homedir,SettingsOperator.tempdir, SettingsOperator.beatmapsdir, SettingsOperator.downloadsdir, SettingsOperator.replaydir, SettingsOperator.screenshotdir, SettingsOperator.skinsdir, SettingsOperator.exportdir };
		foreach (string tmp in directories)
		{
			if (Directory.Exists(tmp))
			{
				if (tmp == SettingsOperator.beatmapsdir)
				{
					GD.Print("Checking for beatmaps...");
					string[] dirs = Directory.GetDirectories(SettingsOperator.beatmapsdir)
					.Select(d => new DirectoryInfo(d))      // convert to DirectoryInfo
					.OrderBy(d => d.CreationTime)          // sort oldest → newest
					.Select(d => d.FullName)               // convert back to string paths
					.ToArray();                            // get string array
					foreach (string Dir in dirs)
					{
						var newDir = Dir.Replace("\\", "/");
						GD.Print(newDir);
						Parse_BeatmapDir(newDir);
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
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		CheckAndExtractOszFiles();
	}
}
