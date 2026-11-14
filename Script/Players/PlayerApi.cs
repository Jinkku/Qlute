using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

/// <summary>
/// Public player data from the website: who's online (apiv2/getonline) and profiles (apiv2/getuser/NAME).
/// "Online" means the player's game posted apiv2/setstatus in the last minute.
/// </summary>
public static class PlayerApi
{
	public class Player
	{
		public long Id;
		public string Username = "";
		public bool Online;
		public string Status = "";
		public long LastSeen;
		public long Rank;
		public long Points;
		public string Pfp;
		/// <summary>Friends list only: they added us back.</summary>
		public bool Mutual;
	}

	public class Play
	{
		public string Beatmap = "", Artist = "", Difficulty = "", Mods = "";
		public double Points, Speed = 1;
		public long Combo, Max, Great, Meh, Bad, Created, BeatmapSetId;
	}

	public class Medal
	{
		public string Title = "", Desc = "", Hint = "";
		public bool Achieved;
	}

	public class Profile : Player
	{
		public bool Restricted;
		/// <summary>"mod" / "admin" for the Qlute team, else empty.</summary>
		public string TeamRole = "";
		public long Score, Level, LevelPct, MaxCombo, Joined;
		/// <summary>Profile banner (users.bannerpath), empty when none.</summary>
		public string Banner = "";
		/// <summary>Uploaded / claimed sets per status tab: "1" ranked, "4" special, "0" unranked.</summary>
		public Dictionary<string, long> BeatmapCounts = new();
		public bool CountsFromServer;
		public double Accuracy;
		public string PlaytimeText = "";
		/// <summary>"About me", set on the website's settings page (empty when none).</summary>
		public string Bio = "";
		public long JMax, JGreat, JMeh, JBad;
		public List<Medal> Medals = new();
		public List<Play> Best = new();
		public List<Play> Recent = new();
	}

	private static readonly System.Net.Http.HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };
	private static readonly Dictionary<string, ImageTexture> Pictures = new();
	private static readonly Dictionary<string, List<Action<ImageTexture>>> PicturesLoading = new();

	/// <summary>The "api" setting; in debug builds QLUTE_API_BASE overrides it (local testing, like QLUTE_WS_BASE).</summary>
	public static string Base
	{
		get
		{
#if DEBUG
			var env = System.Environment.GetEnvironmentVariable("QLUTE_API_BASE");
			if (!string.IsNullOrEmpty(env)) return env.TrimEnd('/') + "/";
#endif
			return SettingsOperator.GetSetting("api")?.ToString() ?? "https://qlute.jinkku.moe/";
		}
	}

	public static string WebProfileUrl(string username) => $"{Base.TrimEnd('/')}/user/{Uri.EscapeDataString(username)}";

	/// <summary>(online count, players) or null if the server can't be reached.</summary>
	public static async Task<(long count, List<Player> players)?> FetchOnline()
	{
		try
		{
			var json = JsonNode.Parse(await Http.GetStringAsync(Base + "apiv2/getonline")) as JsonObject;
			if (json == null) return null;
			var list = (json["players"] as JsonArray)?.OfType<JsonObject>().Select(p => ReadPlayer(p, new Player())).ToList() ?? new();
			return (OnlineApi.Long(json, "count", list.Count), list);
		}
		catch (Exception e)
		{
			GD.Print($"Couldn't load the online player list: {e.Message}");
			return null;
		}
	}

	// ---- Friends (apiv2/friends, one-way like osu!: both ways = mutual) ----

	/// <summary>Our friends by name (value: mutual), as of the last <see cref="FetchFriends"/>.</summary>
	public static readonly Dictionary<string, bool> Friends = new(StringComparer.OrdinalIgnoreCase);
	/// <summary>Bumped whenever <see cref="Friends"/> changes, so open panels can redraw.</summary>
	public static int FriendsVersion { get; private set; }

	public static bool IsFriend(string name) => name != null && Friends.ContainsKey(name);

	public static System.Net.Http.HttpRequestMessage Authed(System.Net.Http.HttpMethod method, string path)
	{
		var req = new System.Net.Http.HttpRequestMessage(method, Base + path);
		req.Headers.TryAddWithoutValidation("Username", ApiOperator.Username ?? "");
		req.Headers.TryAddWithoutValidation("Password", ApiOperator.PasswordHash ?? "");
		req.Headers.TryAddWithoutValidation("User-Agent", ApiOperator.AgentString ?? "Qlute");
		return req;
	}

	/// <summary>Our friends, online first (null + reason if it failed). Also refreshes <see cref="Friends"/>.</summary>
	public static async Task<(List<Player> friends, string error)> FetchFriends()
	{
		if (!OnlineApi.Available) return (null, "Log in to see your friends.");
		try
		{
			var res = await Http.SendAsync(Authed(System.Net.Http.HttpMethod.Get, "apiv2/friends"));
			var json = JsonNode.Parse(await res.Content.ReadAsStringAsync()) as JsonObject;
			if (!res.IsSuccessStatusCode || json == null)
				return (null, OnlineApi.Str(json, "reason") ?? $"The server said {(int)res.StatusCode}.");
			var list = (json["friends"] as JsonArray)?.OfType<JsonObject>().Select(p => ReadPlayer(p, new Player())).ToList() ?? new();
			Friends.Clear();
			foreach (var f in list) Friends[f.Username] = f.Mutual;
			FriendsVersion++;
			return (list, null);
		}
		catch (Exception e)
		{
			return (null, $"Couldn't reach the server ({e.Message}).");
		}
	}

	public static async void RefreshFriends() => await FetchFriends();

	/// <summary>Adds or removes a friend. Returns an error to show, or null.</summary>
	public static async Task<string> SetFriend(string name, bool add)
	{
		if (!OnlineApi.Available) return "Log in to add friends.";
		try
		{
			var res = await Http.SendAsync(Authed(System.Net.Http.HttpMethod.Post, $"apiv2/friends/{(add ? "add" : "remove")}/{Uri.EscapeDataString(name)}"));
			var json = JsonNode.Parse(await res.Content.ReadAsStringAsync()) as JsonObject;
			if (!res.IsSuccessStatusCode) return OnlineApi.Str(json, "reason") ?? $"The server said {(int)res.StatusCode}.";
			if (add) Friends[name] = OnlineApi.Bool(json, "mutual");
			else Friends.Remove(name);
			FriendsVersion++;
			return null;
		}
		catch (Exception e)
		{
			return $"Couldn't reach the server ({e.Message}).";
		}
	}

	/// <summary>The profile, or null with a reason.</summary>
	public static async Task<(Profile profile, string error)> FetchProfile(string username)
	{
		try
		{
			var res = await Http.GetAsync(Base + "apiv2/getuser/" + Uri.EscapeDataString(username));
			var json = JsonNode.Parse(await res.Content.ReadAsStringAsync()) as JsonObject;
			if (!res.IsSuccessStatusCode || json == null || OnlineApi.Long(json, "error") != 0)
				return (null, OnlineApi.Str(json, "reason") ?? $"The server said {(int)res.StatusCode}.");
			var p = ReadPlayer(json, new Profile());
			p.Restricted = OnlineApi.Bool(json, "restricted");
			p.TeamRole = OnlineApi.Str(json, "team_role") ?? "";
			p.Score = OnlineApi.Long(json, "score");
			p.Level = OnlineApi.Long(json, "level", 1);
			// Older servers don't send level_pct: same formula as the website (views.py leveltemp).
			p.LevelPct = json["level_pct"] != null ? OnlineApi.Long(json, "level_pct") : LevelPercent(p.Score);
			p.Banner = OnlineApi.Str(json, "banner") ?? "";
			p.CountsFromServer = json["beatmap_counts"] is JsonObject;
			if (json["beatmap_counts"] is JsonObject counts)
				foreach (var (k, _) in counts) p.BeatmapCounts[k] = OnlineApi.Long(counts, k);
			p.MaxCombo = OnlineApi.Long(json, "max_combo");
			p.Joined = OnlineApi.Long(json, "joined");
			p.Accuracy = Double(json, "accuracy");
			p.PlaytimeText = OnlineApi.Str(json, "playtime_text") ?? "";
			p.Bio = OnlineApi.Str(json, "bio") ?? "";
			if (json["judgements"] is JsonObject j)
			{
				p.JMax = OnlineApi.Long(j, "max"); p.JGreat = OnlineApi.Long(j, "great");
				p.JMeh = OnlineApi.Long(j, "meh"); p.JBad = OnlineApi.Long(j, "bad");
			}
			if (json["medals"] is JsonArray medals)
				p.Medals = medals.OfType<JsonObject>().Select(m => new Medal
				{
					Title = OnlineApi.Str(m, "title") ?? "", Desc = OnlineApi.Str(m, "desc") ?? "", Hint = OnlineApi.Str(m, "hint") ?? "", Achieved = OnlineApi.Bool(m, "achieved"),
				}).ToList();
			p.Best = ReadPlays(json["best"]);
			p.Recent = ReadPlays(json["recent"]);
			return (p, null);
		}
		catch (Exception e)
		{
			return (null, $"Couldn't reach the server ({e.Message}).");
		}
	}

	private static readonly Dictionary<string, (string url, DateTime at)> PictureUrls = new();
	private static readonly Dictionary<string, List<Action<ImageTexture>>> PictureUrlsLoading = new();

	/// <summary>
	/// A player's profile picture by account name (apiv2/pfps). Only call this for names the server has verified
	/// (online rooms), never for LAN nicknames. Nothing is called back for unknown names.
	/// </summary>
	public static async void PictureFor(string username, Action<ImageTexture> done)
	{
		if (string.IsNullOrWhiteSpace(username) || username is "Guest" or "None") return;
		if (PictureUrls.TryGetValue(username, out var known) && DateTime.UtcNow - known.at <= TimeSpan.FromMinutes(10))
		{
			if (!string.IsNullOrEmpty(known.url)) LoadPicture(known.url, done);
			return;
		}
		// Several cards can ask for the same name at once (room list + lobby); all of them get the answer.
		if (PictureUrlsLoading.TryGetValue(username, out var waiting)) { waiting.Add(done); return; }
		var list = new List<Action<ImageTexture>> { done };
		PictureUrlsLoading[username] = list;
		try
		{
			var json = JsonNode.Parse(await Http.GetStringAsync(Base + "apiv2/pfps?names=" + Uri.EscapeDataString(username))) as JsonObject;
			known = (OnlineApi.Str(json, username), DateTime.UtcNow);
			PictureUrls[username] = known;
		}
		catch (Exception e)
		{
			GD.Print($"Couldn't look up {username}'s picture: {e.Message}");
			return;
		}
		finally { PictureUrlsLoading.Remove(username); }
		if (string.IsNullOrEmpty(known.url)) return;
		foreach (var cb in list) LoadPicture(known.url, cb);
	}

	/// <summary>Loads (and caches) a picture; the callback runs on the main thread, possibly right away.</summary>
	public static void LoadPicture(string url, Action<ImageTexture> done)
	{
		if (string.IsNullOrEmpty(url)) return;
		if (Pictures.TryGetValue(url, out var cached)) { done(cached); return; }
		if (PicturesLoading.TryGetValue(url, out var waiting)) { waiting.Add(done); return; }
		PicturesLoading[url] = new List<Action<ImageTexture>> { done };
		DownloadPicture(url);
	}

	private static async void DownloadPicture(string url)
	{
		await ApiOperator.DownloadImage(url, tex => Pictures[url] = tex); // logs and skips on failure
		if (!PicturesLoading.Remove(url, out var list) || !Pictures.TryGetValue(url, out var tex)) return;
		foreach (var cb in list)
		{
			try { cb(tex); } catch (ObjectDisposedException) { } // the card asking for it was closed
		}
	}

	/// <summary>views.py leveltemp: level = ranked score * this.</summary>
	public const double LevelMultiplier = 0.0000005;

	/// <summary>Progress to the next level, 0-99 (views.py user()).</summary>
	public static long LevelPercent(long score)
	{
		var raw = score * LevelMultiplier;
		return raw >= 1 ? (long)((raw - Math.Floor(raw)) * 100) : (long)(raw * 100);
	}

	/// <summary>True when the server sent beatmap_counts (newer servers); otherwise the client counts them itself.</summary>
	public static bool HasCounts(Profile p) => p.CountsFromServer;

	/// <summary>"3 Months Ago", like the website's timeform().</summary>
	public static string Ago(long unixSeconds)
	{
		if (unixSeconds <= 0) return "Never";
		var t = Math.Abs((DateTimeOffset.UtcNow - DateTimeOffset.FromUnixTimeSeconds(unixSeconds)).TotalSeconds);
		(double size, string unit) = t >= 31536000 ? (31536000d, "Year") : t >= 2630000 ? (2630000d, "Month") : t >= 86400 ? (86400d, "Day")
			: t >= 3600 ? (3600d, "Hour") : t >= 60 ? (60d, "Minute") : (1d, "Second");
		var x = (long)(t / size);
		return $"{x} {unit}{(x > 1 ? "s" : "")} Ago";
	}

	/// <summary>Grade the same way the website does (from Perfect / Great / Meh / Bad).</summary>
	public static string Grade(Play s)
	{
		var total = s.Max + s.Great + s.Meh + s.Bad;
		if (total == 0) return "?";
		var acc = (s.Max + s.Great / 2.0 + s.Meh / 3.0) / total * 100;
		if (acc >= 100) return "SS";
		if (acc > 95 && s.Bad == 0) return "S";
		if (acc > 90) return "A";
		if (acc > 85) return "B";
		if (acc > 69) return "C";
		if (acc < 1) return "?";
		return "D";
	}

	private static T ReadPlayer<T>(JsonObject o, T p) where T : Player
	{
		p.Id = OnlineApi.Long(o, "id");
		p.Username = OnlineApi.Str(o, "username") ?? "?";
		p.Online = OnlineApi.Bool(o, "online");
		p.Status = OnlineApi.Str(o, "status") ?? "";
		p.LastSeen = OnlineApi.Long(o, "last_seen");
		p.Rank = OnlineApi.Long(o, "rank");
		p.Points = OnlineApi.Long(o, "points");
		p.Pfp = OnlineApi.Str(o, "pfp");
		p.Mutual = OnlineApi.Bool(o, "mutual");
		return p;
	}

	private static List<Play> ReadPlays(JsonNode node) =>
		(node as JsonArray)?.OfType<JsonObject>().Select(s => new Play
		{
			Beatmap = OnlineApi.Str(s, "beatmap") ?? "", Artist = OnlineApi.Str(s, "artist") ?? "",
			Difficulty = OnlineApi.Str(s, "difficulty") ?? "", Mods = OnlineApi.Str(s, "mods") ?? "",
			Points = Double(s, "points"), Speed = Double(s, "speed", 1), Combo = OnlineApi.Long(s, "combo"),
			Max = OnlineApi.Long(s, "max"), Great = OnlineApi.Long(s, "great"), Meh = OnlineApi.Long(s, "meh"), Bad = OnlineApi.Long(s, "bad"),
			Created = OnlineApi.Long(s, "created"), BeatmapSetId = OnlineApi.Long(s, "beatmapset_id"),
		}).ToList() ?? new();

	private static double Double(JsonObject o, string key, double fallback = 0)
	{
		try
		{
			if (o?[key] is not JsonValue v) return fallback;
			if (v.TryGetValue<double>(out var d)) return d;
			if (v.TryGetValue<long>(out var l)) return l;
		}
		catch { }
		return fallback;
	}
}
