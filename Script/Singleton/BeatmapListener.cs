using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

public class BeatmapLegend
{
	/// <summary>
	/// For checking if the pp has been updated. if not then updates it at boot. 
	/// </summary>
	public int ppversion = 1;
	/// <summary>
	/// ID for internal set
	/// </summary>
	public int ID { get; set; } = 0;
	/// <summary>
	/// Set ID for internal set
	/// </summary>
	public int SetID { get; set; } = 0;
	/// <summary>
	/// Title in translation
	/// </summary>
	public string Title { get; set; } = null;
	/// <summary>
	/// Title in pure unicode
	/// </summary>
	public string TitleUnicode { get; set; } = null;
	/// <summary>
	/// Artist in translation
	/// </summary>
	public string Artist { get; set; } = null;
	/// <summary>
	/// Artist in pure unicode
	/// </summary>
	public string ArtistUnicode { get; set; } = null;
	/// <summary>
	/// Sample set
	/// </summary>
	public string SampleSet { get; set; } = "Normal";
	/// <summary>
	/// Mapper who made the beatmap
	/// </summary>
	public string Mapper { get; set; } = null;
	/// <summary>
	/// Key count
	/// </summary>
	public int KeyCount { get; set; } = 4;
	/// <summary>
	/// Difficulty name
	/// </summary>
	public string Version { get; set; } = null;
	/// <summary>
	/// old pp system
	/// </summary>
	public float pp { get; set; } = 0.0f;
	/// <summary>
	/// ppv2 value of every hit object. Loaded from the .osu on first use (gameplay), because the
	/// startup cache only keeps the song-select metadata.
	/// </summary>
	public List<float> ppv2sets { get { EnsureDetails(); return _ppv2sets; } set => _ppv2sets = value; }
	private List<float> _ppv2sets;
	private List<DanceCounter> _dance;
	private void EnsureDetails()
	{
		if (_ppv2sets != null && _dance != null) return;
		if (string.IsNullOrEmpty(Rawurl) || !System.IO.File.Exists(Rawurl))
		{
			_ppv2sets ??= new List<float>();
			_dance ??= new List<DanceCounter>();
			return;
		}
		var full = SettingsOperator.ParseBeatmapLegend(Rawurl);
		_ppv2sets = full._ppv2sets;
		_dance = full._dance;
	}
	/// <summary>
	/// Beatmap ID for the dedicated server
	/// </summary>
	public int BeatmapID { get; set; } = -1;
	/// <summary>
	/// Beatmap Set ID for the dedicated server
	/// </summary>
	public int BeatmapSetID { get; set; } = -1;
	/// <summary>
	/// Beats per minute
	/// </summary>
	public float Bpm { get; set; } = 0.0f;
	/// <summary>
	/// Dance mode time sets.
	/// </summary>
	public List<DanceCounter> Dance { get { EnsureDetails(); return _dance; } set => _dance = value; }
	/// <summary>
	/// Preview audio segment for the beatmap
	/// </summary>
	public float PreviewTime { get; set; } = 0;
	/// <summary>
	/// Total duration of the beatmap.
	/// </summary>
	public float Timetotal { get; set; } = 0;
	/// <summary>
	/// Level for the beatmap.
	/// </summary>
	public float Levelrating { get; set; } = 0.0f;
	/// <summary>
	/// Accuracy Difficulty for the beatmap
	/// </summary>
	public float Accuracy { get; set; } = 0.0f;
	/// <summary>
	/// Background of the beatmap
	/// </summary>
	public string Background { get; set; } = null;
	/// <summary>
	/// Audio path
	/// </summary>
	public string Audio { get; set; } = null;
	/// <summary>
	/// Note Count
	/// </summary>
	public int NoteCount { get; set; } = 0;
	/// <summary>
	/// Raw URL for the beatmap, used for downloading
	/// </summary>
	public string Rawurl { get; set; } = null;
	/// <summary>
	/// Path of the beatmap
	/// </summary>
	public string Path { get; set; } = null;
	/// <summary>
	/// Rank Status
	/// </summary>
	public int RankStatus { get; set; } = RankStatusLegend.Unknown;
	/// <summary>
	/// Gamemode ID
	/// 0 = Mania
	/// 1 = Taiko
	/// 2 = Dash! (WIP)
	/// 3 = Tap! (WIP)
	/// </summary>
	public int GameModeID { get; set; } = 0;
}

public static class RankStatusLegend
{
	public static readonly int Ranked = 1;
	public static readonly int Unranked = 0;
	public static readonly int Special = 2;
	public static readonly int Unknown = -1;
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
			SettingsOperator.SessionConfig.ReloadDB = true;
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
	/// <summary>Downloads being unzipped in the background right now.</summary>
	private readonly HashSet<string> _extracting = new();

	/// <summary>Imports new downloads. Unzipping happens off the main thread so the game (chat, multiplayer
	/// lobby...) doesn't freeze while a big beatmap is extracted; adding it to the library happens back here.</summary>
	public void CheckAndExtractOszFiles()
	{
		foreach (string file in Directory.GetFiles(SettingsOperator.downloadsdir, "*.osz"))
		{
			if (!_extracting.Add(file)) continue;
			string beatmapDir = Path.Combine(SettingsOperator.beatmapsdir, Path.GetFileNameWithoutExtension(file));
			System.Threading.Tasks.Task.Run(() =>
			{
				try
				{
					Directory.CreateDirectory(beatmapDir);
					System.IO.Compression.ZipFile.ExtractToDirectory(file, beatmapDir, overwriteFiles: true);
					File.Delete(file);
					return true;
				}
				catch (Exception e)
				{
					GD.PrintErr($"Couldn't extract {file}: {e.Message}");
					try { File.Move(file, file + ".failed", overwrite: true); } catch { /* leave it */ }
					return false;
				}
			}).ContinueWith(t => Callable.From(() =>
			{
				_extracting.Remove(file);
				if (!t.Result) { Notify.Post("Couldn't import " + Path.GetFileName(file), Type: NotificationIcons.NotificationType.Warning); return; }
				GD.Print("Extracted and moved " + file + " to " + beatmapDir);
				var name = Parse_BeatmapDir(beatmapDir.Replace("\\", "/"));
				Notify.Post("Imported\n" + name);
			}).CallDeferred());
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
	
	/// <summary>
	/// Startup scan: every set folder (oldest first), each folder's .osu files (smallest first),
	/// same order and IDs as before. Unchanged files come from BeatmapCache; the rest are parsed
	/// in parallel and the cache is rewritten.
	/// </summary>
	private void LoadAllBeatmaps()
	{
		var watch = System.Diagnostics.Stopwatch.StartNew();
		var cache = BeatmapCache.Load();

		var sets = new DirectoryInfo(SettingsOperator.beatmapsdir).GetDirectories()
			.OrderBy(d => d.CreationTime)
			.Select(d => d.GetFiles("*.osu").OrderBy(f => f.Length)
				.Select(f => (path: f.FullName.Replace("\\", "/"), stamp: BeatmapCache.StampOf(f))).ToArray())
			.ToArray();
		var all = sets.SelectMany(x => x).ToArray();

		var legends = new BeatmapLegend[all.Length];
		var misses = new List<int>();
		for (int i = 0; i < all.Length; i++)
		{
			if (cache.TryGetValue(all[i].path, out var hit) && hit.stamp == all[i].stamp) legends[i] = hit.legend;
			else misses.Add(i);
		}
		System.Threading.Tasks.Parallel.ForEach(misses, i =>
		{
			try { legends[i] = SettingsOperator.ParseBeatmapLegend(all[i].path, details: false); }
			catch (Exception e) { GD.PrintErr($"[Qlute] Couldn't read {all[i].path}: {e.Message}"); }
		});

		int k = 0;
		foreach (var set in sets)
		{
			foreach (var _ in set)
			{
				var legend = legends[k++];
				if (legend == null) continue;
				legend.ID = SettingsOperator.Beatmaps.Count;
				legend.SetID = SetID;
				SettingsOperator.Beatmaps.Add(legend);
			}
			SetID++;
		}
		if (all.Length > 0) SettingsOperator.SessionConfig.ReloadDB = true;

		if (misses.Count > 0 || cache.Count != all.Length)
			BeatmapCache.Save(Enumerable.Range(0, all.Length).Where(i => legends[i] != null)
				.Select(i => (all[i].path, all[i].stamp, legends[i])).ToList());
		GD.Print($"[Qlute] Loaded {SettingsOperator.Beatmaps.Count} beatmaps in {watch.ElapsedMilliseconds} ms ({all.Length - misses.Count} cached, {misses.Count} parsed)");
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
					LoadAllBeatmaps();
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
	private double _oszScanTimer = 0;
	public override void _Process(double delta)
	{
		// Scanning the filesystem every frame is expensive (thousands of syscalls/sec at high FPS); once a second is plenty.
		_oszScanTimer -= delta;
		if (_oszScanTimer > 0) return;
		_oszScanTimer = 1.0;
		CheckAndExtractOszFiles();
	}
}
