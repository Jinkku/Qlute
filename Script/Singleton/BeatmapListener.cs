using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

public class BeatmapLegend
{
	public int ppversion = 1; // pp version to check if beatmap needs to be refreshed.
	public int ID { get; set; } = 0;
	public int SetID { get; set; } = 0;
	public string Title { get; set; } = null;
	public string TitleUnicode { get; set; } = null;
	public string Artist { get; set; } = null;
	public string ArtistUnicode { get; set; } = null;
	public string SampleSet { get; set; } = "Normal";
	public string Mapper { get; set; } = null;
	public int KeyCount { get; set; } = 4;
	public string Version { get; set; } = null;
	public double pp { get; set; } = 0.0;
	public List<double> ppv2sets { get; set; } = new List<double>();
	public int BeatmapID { get; set; } = -1;
	public int BeatmapSetID { get; set; } = -1;
	public float Bpm { get; set; } = 0.0f;
	public List<DanceCounter> Dance { get; set; } = new List<DanceCounter>();
	public float PreviewTime { get; set; } = 0; // Preview Time
	public float Timetotal { get; set; } = 0; // in milliseconds
	public float Levelrating { get; set; } = 0.0f; // Level rating for the beatmap
	public double Accuracy { get; set; } = 0.0; // Accuracy Level for the beatmap (not been used yet)
	public string Background { get; set; } = null; // Background image path
	public string Audio { get; set; } = null; // Audio file path
	public string Rawurl { get; set; } = null; // Raw URL for the beatmap, used for downloading
	public string Path { get; set; } = null; // Path for the beatmap
}

public static class SampleSet
{
	public static List<string> Normal = new List<string>(["normal-hitnormal.wav", "normal-hitwhistle.wav", "normal-hitfinish.wav", "normal-hitclap.wav"]);
	public static List<string> Soft = new List<string>(["soft-hitnormal.wav", "soft-hitwhistle.wav", "soft-hitfinish.wav", "soft-hitclap.wav"]);
	public static List<string> Drum = new List<string>(["drum-hitnormal.wav", "drum-hitwhistle.wav", "drum-hitfinish.wav", "drum-hitclap.wav"]);
	public static List<string> Type = new List<string>(["Normal", "Soft", "Drum"]);
}

public partial class BeatmapListener : Node
{
	private SettingsOperator SettingsOperator { get; set; }
	public int SetID { get; set; } = 0; // Set ID for the beatmap, used for grouping beatmaps together
	public string Parse_BeatmapDir(string dir)
	{
		var Name = "";
		var files = Directory.GetFiles(dir, "*.osu");
		Array.Sort(files, (x, y) => new FileInfo(x).Length.CompareTo(new FileInfo(y).Length));
		foreach (string file in files)
		{
			Name = SettingsOperator.Parse_Beatmapfile(file.Replace("\\", "/"), SetID: SetID); // Parse the beatmap file and add it to the beatmaps list
			SettingsOperator.Sessioncfg["reloaddb"] = true;
		}
		SetID++; // Increment SetID for each beatmap directory parsed
		return Name;
	}

	private void ImportBeatmap(string file, bool dd = false)
	{
		string beatmapDir = Path.Combine(SettingsOperator.beatmapsdir, Path.GetFileNameWithoutExtension(file));
		if (!Directory.Exists(beatmapDir))
		{
			Directory.CreateDirectory(beatmapDir);
		}
		else
		{
			GD.Print("Beatmap already exists for " + file + ", cancelling extraction and removing file.");
			if (!dd) File.Delete(file);
		}
		System.IO.Compression.ZipFile.ExtractToDirectory(file, beatmapDir);
		File.Delete(file);
		GD.Print("Extracted and moved " + file + " to " + beatmapDir);
		GD.Print("Parsing " + beatmapDir);
		var Name = Parse_BeatmapDir(beatmapDir);
		GD.Print("Parsed...");
		Notify.Post("Imported\n" + Name);
	}
	public void CheckAndExtractOszFiles()
	{
		foreach (string file in Directory.GetFiles(SettingsOperator.downloadsdir, "*.osz"))
		{
			ImportBeatmap(file);
		}
	}

	private void ParseFileDrop(String[] files)
	{
		foreach (string file in files)
		{
			if (file.EndsWith(".qsf") || file.EndsWith(".osz"))
			{
				ImportBeatmap(file);
			}
		}
	}
	
	public override void _Ready()
	{
		GetWindow().FilesDropped += ParseFileDrop;
		SettingsOperator = GetNode<SettingsOperator>("/root/SettingsOperator");
		GD.Print(SettingsOperator.beatmapsdir);
		string[] directories = { SettingsOperator.homedir, SettingsOperator.tempdir, SettingsOperator.beatmapsdir, SettingsOperator.downloadsdir, SettingsOperator.replaydir, SettingsOperator.screenshotdir, SettingsOperator.skinsdir, SettingsOperator.exportdir };
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
