using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

/// <summary>
/// Song select's filters and sort (saved in the "songfilter" setting). <see cref="Apply"/> turns the library + search
/// text into the ordered list of difficulties to show. A set's difficulties always stay together (easiest first), so
/// two sets of the same song never get mixed up.
/// </summary>
public static class SongFilter
{
	public enum SortMode { Added, Title, Artist, Mapper, Difficulty, Length, Bpm }

	public static readonly (SortMode mode, string name)[] Sorts =
	{
		(SortMode.Added, "Date added"), (SortMode.Title, "Title"), (SortMode.Artist, "Artist"), (SortMode.Mapper, "Mapper"),
		(SortMode.Difficulty, "Difficulty"), (SortMode.Length, "Length"), (SortMode.Bpm, "BPM"),
	};
	/// <summary>Status filter: -2 = any, else RankStatusLegend (1 ranked, 2 special, 0 unranked).</summary>
	public static readonly (int value, string name)[] Statuses = { (-2, "Any"), (1, "Ranked"), (2, "Special"), (0, "Unranked") };
	/// <summary>Map mode filter (BeatmapLegend.GameModeID): -2 = any.</summary>
	public static readonly (int value, string name)[] Modes = { (-2, "Any"), (0, "Mania"), (1, "Taiko"), (3, "Tap!") };

	public class State
	{
		public SortMode Sort { get; set; } = SortMode.Added;
		public bool Descending { get; set; }
		public int Status { get; set; } = -2;
		public int Mode { get; set; } = -2;
		public int LevelMin { get; set; } = 0;
		public int LevelMax { get; set; } = 100;
		/// <summary>Only this collection (null = all beatmaps).</summary>
		public string Collection { get; set; }
	}

	public const string SettingKey = "songfilter";
	private static State _state;
	public static State Current => _state ??= Load();

	private static State Load()
	{
		try
		{
			var raw = SettingsOperator.GetSetting(SettingKey)?.ToString();
			if (!string.IsNullOrWhiteSpace(raw)) return JsonSerializer.Deserialize<State>(raw) ?? new State();
		}
		catch (Exception e) { GD.PrintErr($"[SongFilter] Couldn't read saved filters: {e.Message}"); }
		return new State();
	}

	public static void Save() => SettingsOperator.SetSetting(SettingKey, JsonSerializer.Serialize(Current));

	public static void Reset()
	{
		_state = new State { Sort = Current.Sort, Descending = Current.Descending }; // keep the sort, clear the filters
		Save();
	}

	/// <summary>How many filters are narrowing the list (for the button's badge). Sorting doesn't count.</summary>
	public static int ActiveCount
	{
		get
		{
			var s = Current;
			int n = 0;
			if (s.Status != -2) n++;
			if (s.Mode != -2) n++;
			if (s.LevelMin > 0 || s.LevelMax < 100) n++;
			if (!string.IsNullOrEmpty(s.Collection)) n++;
			return n;
		}
	}

	/// <summary>Indices into SettingsOperator.Beatmaps to show, in order. <paramref name="extra"/> = an extra condition (LAN picker).</summary>
	public static List<int> Apply(string query, Func<int, bool> extra = null)
	{
		var s = Current;
		var maps = SettingsOperator.Beatmaps;
		var words = (query ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
		HashSet<string> inCollection = string.IsNullOrEmpty(s.Collection) ? null : Collections.KeysOf(s.Collection);
		if (!string.IsNullOrEmpty(s.Collection) && inCollection == null) { s.Collection = null; Save(); } // it was deleted

		var keep = new List<int>();
		for (int i = 0; i < maps.Count; i++)
		{
			var b = maps[i];
			if (b == null) continue;
			if (extra != null && !extra(i)) continue;
			if (s.Status != -2 && b.RankStatus != s.Status) continue;
			if (s.Mode != -2 && b.GameModeID != s.Mode) continue;
			int level = (int)b.Levelrating;
			if (s.LevelMin > 0 && level < s.LevelMin) continue; // untouched ends don't filter (odd maps can be <0 or >100)
			if (s.LevelMax < 100 && level > s.LevelMax) continue;
			if (inCollection != null && !inCollection.Contains(Collections.KeyOf(b))) continue;
			if (words.Length > 0 && !words.All(w => Matches(b, w))) continue;
			keep.Add(i);
		}

		// Group by set (folder), order the sets, then each set's difficulties easiest first.
		var groups = keep.GroupBy(i => SetKey(maps[i])).Select(g => g.ToList()).ToList();
		IOrderedEnumerable<List<int>> ordered = s.Sort switch
		{
			SortMode.Title => Order(groups, g => Text(maps[g[0]].Title), s.Descending),
			SortMode.Artist => Order(groups, g => Text(maps[g[0]].Artist), s.Descending),
			SortMode.Mapper => Order(groups, g => Text(maps[g[0]].Mapper), s.Descending),
			SortMode.Difficulty => OrderNum(groups, g => g.Max(i => maps[i].Levelrating), s.Descending),
			SortMode.Length => OrderNum(groups, g => g.Max(i => maps[i].Timetotal), s.Descending),
			SortMode.Bpm => OrderNum(groups, g => g.Max(i => maps[i].Bpm), s.Descending),
			_ => OrderNum(groups, g => g.Min(), s.Descending), // library order = when it was added
		};
		return ordered.ThenBy(g => g.Min())
			.SelectMany(g => g.OrderBy(i => maps[i].Levelrating).ThenBy(i => maps[i].NoteCount).ThenBy(i => i))
			.ToList();
	}

	private static IOrderedEnumerable<List<int>> Order(List<List<int>> g, Func<List<int>, string> key, bool desc) =>
		desc ? g.OrderByDescending(key, StringComparer.OrdinalIgnoreCase) : g.OrderBy(key, StringComparer.OrdinalIgnoreCase);

	private static IOrderedEnumerable<List<int>> OrderNum(List<List<int>> g, Func<List<int>, double> key, bool desc) =>
		desc ? g.OrderByDescending(key) : g.OrderBy(key);

	private static string Text(string s) => s ?? "";

	/// <summary>Which set a difficulty belongs to: its folder (two sets of the same song are separate folders).</summary>
	public static string SetKey(BeatmapLegend b) => (b.Path ?? System.IO.Path.GetDirectoryName(b.Rawurl ?? "") ?? "").Replace("\\", "/").TrimEnd('/');

	private static bool Matches(BeatmapLegend b, string w) =>
		Has(b.Title, w) || Has(b.TitleUnicode, w) || Has(b.Artist, w) || Has(b.ArtistUnicode, w) || Has(b.Mapper, w) || Has(b.Version, w);

	private static bool Has(string field, string w) => field != null && field.Contains(w, StringComparison.OrdinalIgnoreCase);

	/// <summary>A colour that stays the same for a set (from its folder name), to tell sets of the same song apart.</summary>
	public static Color SetColor(BeatmapLegend b)
	{
		uint h = 2166136261;
		foreach (char c in SetKey(b)) { h ^= c; h *= 16777619; }
		return Color.FromHsv((h % 360) / 360f, 0.55f, 0.95f);
	}

	/// <summary>Number of difficulties in the library from the same set.</summary>
	public static int SetSize(BeatmapLegend b)
	{
		var key = SetKey(b);
		return SettingsOperator.Beatmaps.Count(x => x != null && SetKey(x) == key);
	}
}
