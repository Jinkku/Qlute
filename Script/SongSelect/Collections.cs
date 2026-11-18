using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

/// <summary>
/// Beatmap collections: named lists of difficulties, like osu!'s. Saved in &lt;userdata&gt;/collections.json.
/// A difficulty is remembered by its .osu path inside the beatmaps folder, so collections survive restarts and
/// re-scans (indices into SettingsOperator.Beatmaps don't).
/// </summary>
public static class Collections
{
	private class Entry
	{
		public string Name { get; set; } = "";
		public List<string> Maps { get; set; } = new();
	}

	private static List<Entry> _list;
	/// <summary>Bumped on every change so open screens can redraw.</summary>
	public static int Version { get; private set; }

	private static string FilePath => SettingsOperator.homedir + "/collections.json";

	private static List<Entry> List
	{
		get
		{
			if (_list != null) return _list;
			try
			{
				_list = File.Exists(FilePath) ? JsonSerializer.Deserialize<List<Entry>>(File.ReadAllText(FilePath)) : null;
			}
			catch (Exception e)
			{
				GD.PrintErr($"[Collections] Couldn't read {FilePath}: {e.Message}");
				try { File.Copy(FilePath, FilePath + ".broken", overwrite: true); } catch { /* keep going with an empty list */ }
			}
			_list ??= new List<Entry>();
			return _list;
		}
	}

	private static void Save()
	{
		Version++;
		try
		{
			string tmp = FilePath + ".tmp";
			File.WriteAllText(tmp, JsonSerializer.Serialize(List, new JsonSerializerOptions { WriteIndented = true }));
			File.Move(tmp, FilePath, overwrite: true);
		}
		catch (Exception e)
		{
			GD.PrintErr($"[Collections] Couldn't save: {e.Message}");
			Notify.Post("Couldn't save your collections", Description: e.Message, Type: NotificationIcons.NotificationType.Warning);
		}
	}

	/// <summary>The key a difficulty is stored under: its .osu path relative to the beatmaps folder.</summary>
	public static string KeyOf(BeatmapLegend b)
	{
		if (b?.Rawurl == null) return null;
		var path = b.Rawurl.Replace("\\", "/");
		var root = SettingsOperator.beatmapsdir.TrimEnd('/') + "/";
		return path.StartsWith(root) ? path[root.Length..] : path;
	}

	public static IReadOnlyList<string> Names => List.Select(e => e.Name).ToList();

	public static bool Exists(string name) => Find(name) != null;

	private static Entry Find(string name) => List.FirstOrDefault(e => string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase));

	/// <summary>Difficulties in the collection that are in the library right now.</summary>
	public static int CountInLibrary(string name)
	{
		var e = Find(name);
		if (e == null) return 0;
		var keys = new HashSet<string>(e.Maps);
		return SettingsOperator.Beatmaps.Count(b => keys.Contains(KeyOf(b)));
	}

	/// <summary>Set of keys for fast filtering (null if there's no such collection).</summary>
	public static HashSet<string> KeysOf(string name)
	{
		var e = Find(name);
		return e == null ? null : new HashSet<string>(e.Maps);
	}

	public static bool Contains(string name, BeatmapLegend b) => Find(name)?.Maps.Contains(KeyOf(b)) == true;

	/// <summary>Collections this difficulty is in.</summary>
	public static List<string> CollectionsOf(BeatmapLegend b)
	{
		var key = KeyOf(b);
		return key == null ? new List<string>() : List.Where(e => e.Maps.Contains(key)).Select(e => e.Name).ToList();
	}

	/// <summary>Null when it worked, else why not.</summary>
	public static string Create(string name)
	{
		name = (name ?? "").Trim();
		if (name.Length == 0) return "Give the collection a name.";
		if (name.Length > 60) return "That name is too long (60 characters at most).";
		if (Exists(name)) return $"You already have a collection called \"{name}\".";
		List.Add(new Entry { Name = name });
		Save();
		return null;
	}

	public static string Rename(string name, string newName)
	{
		var e = Find(name);
		newName = (newName ?? "").Trim();
		if (e == null) return "That collection doesn't exist anymore.";
		if (newName.Length == 0) return "Give the collection a name.";
		if (newName.Length > 60) return "That name is too long (60 characters at most).";
		if (!string.Equals(name, newName, StringComparison.OrdinalIgnoreCase) && Exists(newName)) return $"You already have a collection called \"{newName}\".";
		e.Name = newName;
		Save();
		return null;
	}

	public static void Delete(string name)
	{
		if (List.RemoveAll(e => string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase)) > 0) Save();
	}

	/// <summary>Adds the difficulties (skipping ones already in it). Returns how many were added.</summary>
	public static int Add(string name, IEnumerable<BeatmapLegend> maps)
	{
		var e = Find(name);
		if (e == null) return 0;
		int added = 0;
		foreach (var key in maps.Select(KeyOf).Where(k => k != null))
		{
			if (e.Maps.Contains(key)) continue;
			e.Maps.Add(key);
			added++;
		}
		if (added > 0) Save();
		return added;
	}

	public static int Remove(string name, IEnumerable<BeatmapLegend> maps)
	{
		var e = Find(name);
		if (e == null) return 0;
		var keys = new HashSet<string>(maps.Select(KeyOf).Where(k => k != null));
		int removed = e.Maps.RemoveAll(keys.Contains);
		if (removed > 0) Save();
		return removed;
	}
}
