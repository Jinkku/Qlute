using Godot;
using System;
using System.Threading.Tasks;
using QluteStoryboard;

/// <summary>
/// Reads and parses the chart (and its storyboard) on a worker thread while the song loading screen is up,
/// so opening the gameplay scene doesn't freeze on a big map (a 29 MB / 1M-line mania file took ~2 s there).
/// The game modes take the results with the Take methods; anything not preloaded (another file, a mod change,
/// a parse error) is simply loaded the old way on the main thread.
/// </summary>
public static class ChartPreload
{
	private static readonly object Gate = new();
	private static Task _task;
	private static int _generation;
	private static string _path;
	private static int _mode;
	private static bool _random;
	private static int _seed;
	private static string[] _lines;
	private static object _chart;
	private static StoryboardDoc _storyboard;
	private static bool _storyboardChecked; // the worker looked for a storyboard (_storyboard null = there is none)

	/// <summary>True while the worker is still reading / parsing.</summary>
	public static bool Busy => _task != null && !_task.IsCompleted;

	/// <summary>Start loading this beatmap for this game mode (0 Mania, 1 Taiko, 3 Tap!). Call on the main thread.</summary>
	/// <param name="beatmap">Also load its details (RatedNotes, Dance, ppv2) if they aren't yet: that parses the .osu too.</param>
	public static void Start(string path, int mode, BeatmapLegend beatmap = null)
	{
		Clear();
		if (string.IsNullOrEmpty(path) || !FileAccess.FileExists(path)) return;
		// Everything the worker needs from settings / mods is read here, on the main thread.
		bool random = ModsOperator.Mods.TryGetValue("random", out var r) && r;
		bool storyboard = Check.CheckBoolValue(SettingsOperator.GetSetting(StoryboardPlayer.SettingKey)?.ToString() ?? "true");
		int seed = new Random().Next(1, 214562543);
		int generation;
		lock (Gate)
		{
			generation = ++_generation;
			_path = path;
			_mode = mode;
			_random = random;
			_seed = seed;
		}
		_task = Task.Run(() =>
		{
			string[] lines = null;
			object chart = null;
			StoryboardDoc doc = null;
			try { if (beatmap != null && !beatmap.HasDetails()) beatmap.LoadDetails(); }
			catch (Exception e) { GD.PrintErr($"[ChartPreload] details: {e.Message}"); }
			BeatmapHash.Of(path); // cached for the score submission at the end (no hashing a big map on the main thread then)
			try
			{
				if (mode == 3) lines = ReadLines(path); // Tap! parses every section itself
				else
				{
					// Stream the file: the hit objects are parsed as they're read and never kept, so a 1M-line map
					// doesn't leave a million live strings (their garbage collections paused the main thread too).
					// Only the lines before [HitObjects] are kept, for the storyboard ([General] + [Events]).
					var header = new System.Collections.Generic.List<string>();
					var stream = StreamLines(path, header);
					chart = mode switch
					{
						0 => Gameplay.ParseChart(stream, seed, random),
						1 => TaikoGameCore.ParseChart(stream),
						_ => null,
					};
					lines = header.ToArray();
				}
			}
			catch (Exception e) { GD.PrintErr($"[ChartPreload] chart: {e.Message}"); chart = null; }
			if (storyboard && lines != null)
			{
				try
				{
					doc = StoryboardDoc.LoadForBeatmap(path, lines);
					if (doc.IsEmpty) doc = null; else doc.CompileAll();
				}
				catch (Exception e) { GD.PrintErr($"[ChartPreload] storyboard: {e.Message}"); doc = null; }
			}
			lock (Gate)
			{
				if (generation != _generation) return; // cancelled or replaced meanwhile
				_chart = chart;
				_storyboard = doc;
				_storyboardChecked = storyboard && lines != null;
				_lines = mode == 3 ? lines : null; // only Tap! needs the raw lines; don't keep 29 MB around otherwise
			}
		});
	}

	/// <summary>Forget everything (and ignore a worker that is still running).</summary>
	public static void Clear()
	{
		lock (Gate)
		{
			_generation++;
			_path = null;
			_chart = null;
			_lines = null;
			_storyboard = null;
			_storyboardChecked = false;
		}
		_task = null;
	}

	/// <summary>The file's lines one at a time (same split as <see cref="ReadLines"/>); lines before [HitObjects] also go into <paramref name="header"/>.</summary>
	private static System.Collections.Generic.IEnumerable<string> StreamLines(string path, System.Collections.Generic.List<string> header)
	{
		bool inHitObjects = false;
		if (!path.Contains("://") && System.IO.File.Exists(path))
		{
			using var reader = new System.IO.StreamReader(path);
			while (reader.ReadLine() is { } line)
			{
				if (!inHitObjects) { header.Add(line); inHitObjects = line.Trim() == "[HitObjects]"; }
				yield return line;
			}
			yield break;
		}
		foreach (var line in ReadLines(path))
		{
			if (!inHitObjects) { header.Add(line); inHitObjects = line.Trim() == "[HitObjects]"; }
			yield return line;
		}
	}

	/// <summary>The file's lines, split the way the game modes always did (GetAsText().Split("\n")).</summary>
	public static string[] ReadLines(string path)
	{
		// Plain .NET for files on disk: Godot's FileAccess.GetAsText on a worker thread stalled the main thread
		// (~0.6 s for a 29 MB map). res:// / user:// paths still go through Godot.
		if (!path.Contains("://") && System.IO.File.Exists(path))
			return System.IO.File.ReadAllText(path).Split("\n");
		using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
		return file == null ? Array.Empty<string>() : file.GetAsText().Split("\n");
	}

	/// <summary>The preloaded chart for this file and mode (null if there isn't one). Taken once.</summary>
	public static T TakeChart<T>(string path, int mode, out int seed) where T : class
	{
		seed = 0;
		if (Busy) _task.Wait(); // normally finished: the loading screen waits for it
		lock (Gate)
		{
			if (_path != path || _mode != mode || _chart is not T chart) return null;
			bool random = ModsOperator.Mods.TryGetValue("random", out var r) && r;
			if (random != _random) return null; // mods changed after the loading screen started
			seed = _seed;
			_chart = null;
			return chart;
		}
	}

	/// <summary>Preloaded lines for this file, else read now.</summary>
	public static string[] TakeLines(string path)
	{
		if (Busy) _task.Wait();
		lock (Gate)
		{
			if (_path == path && _lines != null)
			{
				var lines = _lines;
				_lines = null;
				return lines;
			}
		}
		return ReadLines(path);
	}

	/// <summary>
	/// True when the storyboard of this file was preloaded: <paramref name="doc"/> is it, compiled, or null when the
	/// map has none. False = not preloaded, load it yourself. Taken once.
	/// </summary>
	public static bool TryTakeStoryboard(string path, out StoryboardDoc doc)
	{
		doc = null;
		if (Busy) _task.Wait();
		lock (Gate)
		{
			if (_path != path || !_storyboardChecked) return false;
			doc = _storyboard;
			_storyboard = null;
			_storyboardChecked = false;
			return true;
		}
	}
}
