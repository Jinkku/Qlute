using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

/// <summary>
/// Favourites, comments and the leaderboard of a beatmap set (Chikara online/beatmapsocial.py + views.py),
/// for the beatmap info overlay and the profile's "Favourite Beatmaps".
/// </summary>
public static class BeatmapSocialApi
{
	public class Comment
	{
		public int Id;
		public string Username = "";
		public string Pfp = "";
		public string Text = "";
		public long Created;
		public bool CanDelete;
	}

	public class Score
	{
		public string Username = "";
		public double Points;
		public long Score_, Combo, Max, Good, Meh, Bad, Time;
		public string Mods = "";
		public double Speed = 1;
		public string Pfp = "";
	}

	/// <summary>Full difficulty rows (apiv2/getdifficulties): what the catalog doesn't carry, like OD.</summary>
	public class DiffDetails
	{
		public int BeatmapId;
		public double Od;
		public int Notes;
		public string Created = "";
	}

	private static readonly System.Net.Http.HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };
	private static readonly JsonSerializerOptions Lenient = new() { NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString };

	public static bool LoggedIn => OnlineApi.Available;

	private static async Task<(JsonNode json, string error)> Send(HttpRequestMessage req)
	{
		try
		{
			var res = await Http.SendAsync(req);
			var body = await res.Content.ReadAsStringAsync();
			JsonNode json = null;
			try { json = JsonNode.Parse(body); } catch (JsonException) { }
			if (!res.IsSuccessStatusCode)
				return (null, OnlineApi.Str(json as JsonObject, "reason") ?? $"The server said {(int)res.StatusCode}.");
			return (json, null);
		}
		catch (Exception e)
		{
			return (null, $"Couldn't reach the server ({e.Message}).");
		}
	}

	private static HttpRequestMessage Get(string path) => LoggedIn
		? PlayerApi.Authed(HttpMethod.Get, path)
		: new HttpRequestMessage(HttpMethod.Get, PlayerApi.Base + path);

	// ---- favourites ----

	public static async Task<(int count, bool favorited)> FavoriteState(int setId)
	{
		var (json, _) = await Send(Get($"apiv2/favorites/{setId}"));
		var o = json as JsonObject;
		return ((int)OnlineApi.Long(o, "count"), OnlineApi.Bool(o, "favorited"));
	}

	/// <summary>Adds / removes a favourite: (favourited now, count, error).</summary>
	public static async Task<(bool favorited, int count, string error)> SetFavorite(int setId, bool add)
	{
		if (!LoggedIn) return (false, 0, "Log in to favourite beatmaps.");
		var (json, error) = await Send(PlayerApi.Authed(HttpMethod.Post, $"apiv2/favorites/{(add ? "add" : "remove")}/{setId}"));
		var o = json as JsonObject;
		return (OnlineApi.Bool(o, "favorited"), (int)OnlineApi.Long(o, "count"), error);
	}

	/// <summary>A player's favourite sets, in the apiv2/search card shape (50 per page).</summary>
	public static async Task<(int count, List<BrowseCatalogLegend> sets, string error)> Favorites(string username, int page = 0)
	{
		var (json, error) = await Send(Get($"apiv2/favorites?user={Uri.EscapeDataString(username)}&page={page}"));
		if (json is not JsonObject o) return (0, null, error ?? "The server sent something odd.");
		List<BrowseCatalogLegend> sets;
		try { sets = o["sets"]?.Deserialize<List<BrowseCatalogLegend>>(Lenient) ?? new(); }
		catch (Exception e) { return (0, null, $"Couldn't read the favourites ({e.Message})."); }
		return ((int)OnlineApi.Long(o, "count"), sets, null);
	}

	/// <summary>Sets a player uploaded or claimed, one status tab ("1" ranked, "4" special, "0" unranked), 50 per page.</summary>
	public static async Task<(List<BrowseCatalogLegend> sets, string error)> Uploads(string username, string status, int page = 0)
	{
		var (json, error) = await Send(Get($"apiv2/search?uploader={Uri.EscapeDataString(username)}&status={status}&sort=newest&page={page}"));
		if (json is not JsonArray a) return (null, error ?? "The server sent something odd.");
		try { return (a.Deserialize<List<BrowseCatalogLegend>>(Lenient) ?? new(), null); }
		catch (Exception e) { return (null, $"Couldn't read the beatmaps ({e.Message})."); }
	}

	// ---- comments ----

	private static Comment ReadComment(JsonObject c) => new()
	{
		Id = (int)OnlineApi.Long(c, "id"),
		Username = OnlineApi.Str(c, "username") ?? "",
		Pfp = OnlineApi.Str(c, "pfp") ?? "",
		Text = OnlineApi.Str(c, "text") ?? "",
		Created = OnlineApi.Long(c, "created"),
		CanDelete = OnlineApi.Bool(c, "can_delete"),
	};

	public static async Task<(int total, List<Comment> comments, string error)> Comments(int setId, int page)
	{
		var (json, error) = await Send(Get($"apiv2/comments/{setId}?page={page}"));
		if (json is not JsonObject o) return (0, null, error ?? "The server sent something odd.");
		var list = (o["comments"] as JsonArray)?.OfType<JsonObject>().Select(ReadComment).ToList() ?? new();
		return ((int)OnlineApi.Long(o, "total"), list, null);
	}

	public static async Task<(Comment comment, string error)> PostComment(int setId, string text, int beatmapId)
	{
		if (!LoggedIn) return (null, "Log in to comment.");
		var req = PlayerApi.Authed(HttpMethod.Post, $"apiv2/comments/{setId}");
		var body = new JsonObject { ["text"] = text, ["beatmapid"] = beatmapId };
		req.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
		var (json, error) = await Send(req);
		if (error != null) return (null, error);
		return ((json as JsonObject)?["comment"] is JsonObject c ? ReadComment(c) : null, null);
	}

	public static async Task<string> DeleteComment(int id)
	{
		if (!LoggedIn) return "Log in first.";
		var (_, error) = await Send(PlayerApi.Authed(HttpMethod.Post, $"apiv2/comments/delete/{id}"));
		return error;
	}

	// ---- leaderboard / difficulty details (the website's beatmap page reads the same) ----

	/// <summary>Best score per player, highest pp first (like the website).</summary>
	public static async Task<(List<Score> scores, string error)> Leaderboard(int beatmapId)
	{
		var req = new HttpRequestMessage(HttpMethod.Get, PlayerApi.Base + "apiv2/getleaderboard");
		req.Headers.TryAddWithoutValidation("BEATMAPID", beatmapId.ToString());
		var (json, error) = await Send(req);
		if (error != null) return (null, error);
		var rows = (json as JsonArray)?.OfType<JsonObject>().Select(r => new Score
		{
			Username = OnlineApi.Str(r, "username") ?? "",
			Points = Num(r, "points"),
			Score_ = OnlineApi.Long(r, "score"),
			Combo = OnlineApi.Long(r, "combo"),
			Max = OnlineApi.Long(r, "MAX"), Good = OnlineApi.Long(r, "GOOD"),
			Meh = OnlineApi.Long(r, "MEH"), Bad = OnlineApi.Long(r, "BAD"),
			Mods = OnlineApi.Str(r, "mods") ?? "",
			Speed = r["speed_multi"] == null ? 1 : Num(r, "speed_multi"),
			Time = OnlineApi.Long(r, "time"),
			Pfp = OnlineApi.Str(r, "pfp") ?? "",
		}).ToList() ?? new();
		var best = rows.GroupBy(r => r.Username.ToLowerInvariant()).Select(g => g.OrderByDescending(r => r.Points).First());
		return (best.OrderByDescending(r => r.Points).ThenByDescending(r => r.Score_).ToList(), null);
	}

	public static async Task<Dictionary<int, DiffDetails>> Difficulties(int setId)
	{
		var req = new HttpRequestMessage(HttpMethod.Get, PlayerApi.Base + "apiv2/getdifficulties");
		req.Headers.TryAddWithoutValidation("BEATMAPSETID", setId.ToString());
		var (json, _) = await Send(req);
		var result = new Dictionary<int, DiffDetails>();
		foreach (var r in (json as JsonArray)?.OfType<JsonObject>() ?? Enumerable.Empty<JsonObject>())
		{
			var d = new DiffDetails
			{
				BeatmapId = (int)OnlineApi.Long(r, "beatmapid"),
				Od = Num(r, "Accuracy"),
				Notes = (int)OnlineApi.Long(r, "notecount"),
				Created = OnlineApi.Str(r, "created") ?? "",
			};
			result[d.BeatmapId] = d;
		}
		return result;
	}

	private static double Num(JsonObject o, string key)
	{
		try { return o?[key]?.GetValue<double>() ?? 0; }
		catch { return double.TryParse(o?[key]?.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0; }
	}

	/// <summary>The website's grade + accuracy for a score.</summary>
	public static (string grade, double acc) Grade(Score s)
	{
		double total = s.Max + s.Good + s.Meh + s.Bad;
		if (total <= 0) return ("?", 0);
		var acc = (s.Max + s.Good / 2.0 + s.Meh / 3.0) / total * 100;
		var g = acc >= 100 ? "SS" : acc > 95 && s.Bad == 0 ? "S" : acc > 90 ? "A" : acc > 85 ? "B" : acc > 69 ? "C" : acc < 1 ? "?" : "D";
		return (g, acc);
	}
}
