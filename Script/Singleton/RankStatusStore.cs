using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

/// <summary>
/// Saved rank status (Ranked / Unranked / Special / Unknown) of every beatmap in the local library,
/// keyed by online beatmap ID, in rankstatus.json in the user data folder. It lives outside
/// BeatmapCache so it survives cache rebuilds.
///
/// - Startup and imports: saved statuses are applied straight away; beatmaps with nothing saved yet
///   (new imports, or the first run of a build that has this feature) are looked up from the server
///   in batches in the background (apiv2/rankstatus).
/// - Selecting a map: the server is asked again; the status is saved and shown only if it changed.
/// - Failed requests never overwrite a saved status; the lookup is retried later.
/// </summary>
public static class RankStatusStore
{
	private const int BatchSize = 400;
	private static readonly object Gate = new();
	private static Dictionary<int, int> _status;
	private static readonly HashSet<int> _pending = new();
	private static bool _syncRunning;
	private static bool _syncQueued;
	private static readonly System.Net.Http.HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };

	private static string FilePath => SettingsOperator.homedir + "/rankstatus.json";

	/// <summary>Server value -> RankStatusLegend (1 Ranked, 0 Unranked, -1 Unknown, anything else Special).</summary>
	public static int Normalize(int serverValue) =>
		serverValue == 1 ? RankStatusLegend.Ranked
		: serverValue == 0 ? RankStatusLegend.Unranked
		: serverValue == -1 ? RankStatusLegend.Unknown
		: RankStatusLegend.Special;

	private static void EnsureLoadedLocked()
	{
		if (_status != null) return;
		_status = new Dictionary<int, int>();
		try
		{
			if (!File.Exists(FilePath)) return;
			using var doc = JsonDocument.Parse(File.ReadAllText(FilePath));
			if (!doc.RootElement.TryGetProperty("statuses", out var statuses)) return;
			foreach (var p in statuses.EnumerateObject())
				if (int.TryParse(p.Name, out var id) && p.Value.TryGetInt32(out var s)) _status[id] = s;
		}
		catch (Exception e)
		{
			GD.PrintErr($"[RankStatus] Couldn't read {FilePath}, starting fresh: {e.Message}");
			_status.Clear();
		}
	}

	private static void SaveLocked()
	{
		try
		{
			var json = JsonSerializer.Serialize(new
			{
				version = 1,
				statuses = _status.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value),
			});
			var tmp = FilePath + ".tmp";
			File.WriteAllText(tmp, json);
			File.Move(tmp, FilePath, overwrite: true);
		}
		catch (Exception e)
		{
			GD.PrintErr($"[RankStatus] Couldn't save {FilePath}: {e.Message}");
		}
	}

	/// <summary>
	/// Call for every beatmap added to the library (startup load, import, editor). Applies the saved
	/// status, or queues a server lookup if there is none yet.
	/// </summary>
	public static void Attach(BeatmapLegend legend)
	{
		if (legend == null || legend.BeatmapID <= 0) return;
		bool queue = false;
		lock (Gate)
		{
			EnsureLoadedLocked();
			if (_status.TryGetValue(legend.BeatmapID, out var saved)) legend.RankStatus = saved;
			else queue = _pending.Add(legend.BeatmapID);
		}
		if (queue) RequestSync();
	}

	/// <summary>
	/// Records a status from the server for one beatmap (main thread). Returns false and does
	/// nothing if it's the same as the saved one; otherwise saves it and updates the library.
	/// </summary>
	public static bool Update(int beatmapId, int status)
	{
		if (beatmapId <= 0) return false;
		lock (Gate)
		{
			EnsureLoadedLocked();
			_pending.Remove(beatmapId);
			if (_status.TryGetValue(beatmapId, out var old) && old == status) return false;
			_status[beatmapId] = status;
			SaveLocked();
		}
		ApplyToLibrary(new Dictionary<int, int> { [beatmapId] = status });
		return true;
	}

	private static void ApplyToLibrary(Dictionary<int, int> changed)
	{
		if (changed.Count == 0) return;
		foreach (var legend in SettingsOperator.Beatmaps)
			if (legend.BeatmapID > 0 && changed.TryGetValue(legend.BeatmapID, out var s)) legend.RankStatus = s;
		int song = SettingsOperator.SessionConfig.SongID;
		if (song >= 0 && song < SettingsOperator.Beatmaps.Count &&
		    changed.TryGetValue(SettingsOperator.Beatmaps[song].BeatmapID, out var current))
			ApiOperator.RankedStatus = current;
	}

	/// <summary>Coalesces lookups: one batch sync at the end of the frame, however many maps were added.</summary>
	public static void RequestSync()
	{
		lock (Gate)
		{
			if (_syncQueued) return;
			_syncQueued = true;
		}
		Callable.From(SyncPending).CallDeferred();
	}

	private static void SyncPending()
	{
		int[] ids;
		lock (Gate)
		{
			_syncQueued = false;
			if (_syncRunning || _pending.Count == 0) return;
			ids = _pending.ToArray();
			_syncRunning = true;
		}
		string api = SettingsOperator.GetSetting("api")?.ToString() ?? "";
		string agent = ApiOperator.AgentString;
		GD.Print($"[RankStatus] Looking up {ids.Length} beatmap(s) without a saved rank status");
		Task.Run(async () =>
		{
			var results = new Dictionary<int, int>();
			try
			{
				for (int i = 0; i < ids.Length; i += BatchSize)
				{
					var chunk = ids.Skip(i).Take(BatchSize).ToArray();
					var batch = await TryBatch(api, agent, chunk);
					if (batch != null)
					{
						foreach (var (id, st) in batch) results[id] = st;
						continue;
					}
					// Server without apiv2/rankstatus (older build): ask per map with the old endpoint, 4 at a time.
					GD.Print($"[RankStatus] Batch lookup not available, asking per map ({chunk.Length})");
					using var gate = new System.Threading.SemaphoreSlim(4);
					var tasks = chunk.Select(async id =>
					{
						await gate.WaitAsync();
						try { return (id, status: await TrySingle(api, agent, id)); }
						finally { gate.Release(); }
					}).ToArray();
					foreach (var (id, st) in await Task.WhenAll(tasks))
						if (st.HasValue) results[id] = st.Value;
				}
			}
			catch (Exception e)
			{
				// Offline: whatever wasn't answered stays pending and is retried on the next import or startup.
				GD.PrintErr($"[RankStatus] Lookup stopped ({results.Count}/{ids.Length} answered): {e.Message}");
			}
			Callable.From(() => FinishSync(results)).CallDeferred();
		});
	}

	private static async Task<string> Get(string url, string agent)
	{
		using var req = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Get, url);
		if (!string.IsNullOrEmpty(agent)) req.Headers.TryAddWithoutValidation("User-Agent", agent);
		using var resp = await Http.SendAsync(req);
		resp.EnsureSuccessStatusCode();
		return await resp.Content.ReadAsStringAsync();
	}

	/// <summary>apiv2/rankstatus for a chunk; null if the server doesn't have that endpoint (not JSON / no "statuses").</summary>
	private static async Task<Dictionary<int, int>> TryBatch(string api, string agent, int[] chunk)
	{
		var body = await Get($"{api}apiv2/rankstatus?ids={string.Join(",", chunk)}", agent); // network errors propagate (offline)
		try
		{
			using var doc = JsonDocument.Parse(body);
			if (!doc.RootElement.TryGetProperty("statuses", out var statuses)) return null;
			var res = new Dictionary<int, int>();
			foreach (var p in statuses.EnumerateObject())
				if (int.TryParse(p.Name, out var id) && p.Value.TryGetInt32(out var s)) res[id] = Normalize(s);
			return res;
		}
		catch (JsonException) { return null; } // e.g. "Unknown Command ['rankstatus']"
	}

	/// <summary>Old per-map endpoint apiv2/s/{id}; null if it couldn't be read.</summary>
	private static async Task<int?> TrySingle(string api, string agent, int id)
	{
		try
		{
			using var doc = JsonDocument.Parse(await Get($"{api}apiv2/s/{id}", agent));
			return doc.RootElement.TryGetProperty("RankedStatus", out var v) && v.TryGetInt32(out var s) ? Normalize(s) : null;
		}
		catch (Exception) { return null; }
	}

	private static void FinishSync(Dictionary<int, int> results)
	{
		var changed = new Dictionary<int, int>();
		bool more;
		lock (Gate)
		{
			EnsureLoadedLocked();
			foreach (var (id, s) in results)
			{
				_pending.Remove(id);
				if (_status.TryGetValue(id, out var old) && old == s) continue;
				_status[id] = s;
				changed[id] = s;
			}
			if (changed.Count > 0) SaveLocked();
			_syncRunning = false;
			// Maps imported while this batch was running are looked up next; failed ones wait for the next import/startup.
			more = _pending.Except(results.Keys).Any() && results.Count > 0;
		}
		ApplyToLibrary(changed);
		GD.Print($"[RankStatus] Saved {changed.Count} rank status(es)");
		if (more) RequestSync();
	}
}
