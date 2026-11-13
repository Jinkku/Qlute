using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

/// <summary>
/// Submits a beatmap set folder to the Qlute server (Chikara: apiv2/submit/...).
/// The folder is zipped into an .osz and sent in pieces, because the web server refuses bodies over 1 MB.
/// The server gives the set and its difficulties ids, which are written back into the local .osu files
/// (BeatmapID / BeatmapSetID), so scores can be submitted and submitting again updates the same set.
/// </summary>
public static class BeatmapSubmit
{
	public const long MaxBytes = 64L * 1024 * 1024;

	private static readonly string[] SkippedExtensions = { ".mp4", ".avi", ".flv", ".wmv", ".mkv", ".webm", ".mov", ".m4v", ".mpg", ".mpeg", ".qrf" };
	private static readonly System.Net.Http.HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(3) };

	public class Difficulty
	{
		public string File = "", Version = "";
		public int BeatmapId;
	}

	public class Result
	{
		public int SetId;
		public bool Updated;
		public string Url = "";
		public List<Difficulty> Beatmaps = new();
		public List<string> Skipped = new(), Removed = new();
	}

	/// <summary>True while an upload is running (one at a time).</summary>
	public static bool Busy { get; private set; }

	/// <summary>Bytes sent of the running upload, for a progress bar.</summary>
	public static long Sent { get; private set; }
	public static long Total { get; private set; }

	/// <summary>Submits the set in <paramref name="folder"/> and shows how it went as notifications. Null when it failed.</summary>
	public static async Task<Result> SubmitWithNotifications(string folder, string label)
	{
		if (Busy)
		{
			Notify.Post("A beatmap is already being submitted. Wait for it to finish.", Type: NotificationIcons.NotificationType.Warning);
			return null;
		}
		if (!SettingsOperator.SessionConfig.Loggedin || string.IsNullOrEmpty(ApiOperator.PasswordHash))
		{
			Notify.Post("Log in to submit beatmaps.", Type: NotificationIcons.NotificationType.Warning);
			return null;
		}
		Notify.Post($"Submitting {label}...", ProgressGetter: () => (int)(Sent / 1024), Max: () => (int)Math.Max(1, Total / 1024),
			Type: NotificationIcons.NotificationType.Upload);
		var (result, error) = await Submit(folder);
		if (error != null)
		{
			Notify.Post("Couldn't submit the beatmap:\n" + error, Type: NotificationIcons.NotificationType.Warning);
			return null;
		}
		var names = string.Join(", ", result.Beatmaps.Select(b => b.Version));
		var text = $"{(result.Updated ? "Updated" : "Submitted")} {label}\n{names}";
		if (result.Skipped.Count > 0) text += $"\nLeft out (not osu!mania 4K): {string.Join(", ", result.Skipped)}";
		if (result.Removed.Count > 0) text += $"\nRemoved from the server: {string.Join(", ", result.Removed)}";
		text += "\nClick to open its page.";
		Notify.Post(text, uri: PlayerApi.Base.TrimEnd('/') + result.Url, Type: NotificationIcons.NotificationType.Send);
		return result;
	}

	/// <summary>(result, null) or (null, readable error).</summary>
	public static async Task<(Result result, string error)> Submit(string folder)
	{
		if (Busy) return (null, "A beatmap is already being submitted.");
		if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return (null, "That beatmap's folder is gone.");
		Busy = true;
		Sent = 0;
		Total = 0;
		string token = null;
		try
		{
			byte[] osz;
			try
			{
				osz = await Task.Run(() => Pack(folder));
			}
			catch (Exception e)
			{
				return (null, $"Couldn't pack the beatmap ({e.Message}).");
			}
			if (osz.Length > MaxBytes) return (null, $"The beatmap is too big ({osz.Length / (1024 * 1024)} MB, {MaxBytes / (1024 * 1024)} MB max). Remove videos or large files from its folder.");
			Total = osz.Length;

			var (start, error) = await Send("start", new StringContent(new JsonObject { ["size"] = osz.Length, ["name"] = Path.GetFileName(folder) + ".osz" }.ToJsonString(), Encoding.UTF8, "application/json"));
			if (error != null) return (null, error);
			token = OnlineApi.Str(start, "token");
			var chunk = (int)OnlineApi.Long(start, "chunk_size", 768 * 1024);
			if (string.IsNullOrEmpty(token)) return (null, "The server sent something unexpected.");

			int tries = 0;
			while (Sent < osz.Length)
			{
				var piece = new ByteArrayContent(osz, (int)Sent, (int)Math.Min(chunk, osz.Length - Sent));
				piece.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
				var (reply, chunkError) = await Send($"chunk/{token}?offset={Sent}", piece, allowConflict: true);
				if (chunkError != null)
				{
					if (++tries > 3) return (null, chunkError);
					await Task.Delay(1000 * tries);
					continue;
				}
				tries = 0;
				Sent = OnlineApi.Long(reply, "received", Sent);
			}

			var (done, finishError) = await Send($"finish/{token}", null);
			token = null; // the server drops the upload either way
			if (finishError != null) return (null, finishError);

			var result = new Result
			{
				SetId = (int)OnlineApi.Long(done, "setid"),
				Updated = OnlineApi.Bool(done, "updated"),
				Url = OnlineApi.Str(done, "url") ?? "",
				Beatmaps = (done["beatmaps"] as JsonArray)?.OfType<JsonObject>().Select(o => new Difficulty
				{
					File = OnlineApi.Str(o, "file") ?? "", Version = OnlineApi.Str(o, "version") ?? "",
					BeatmapId = (int)OnlineApi.Long(o, "beatmapid"),
				}).ToList() ?? new(),
				Skipped = Strings(done, "skipped"),
				Removed = Strings(done, "removed"),
			};
			ApplyIds(folder, result);
			return (result, null);
		}
		finally
		{
			if (token != null) _ = Send($"cancel/{token}", null);
			Busy = false;
		}
	}

	/// <summary>The set folder as an .osz. Videos are left out (they're big and the server doesn't need them).</summary>
	private static byte[] Pack(string folder)
	{
		using var ms = new MemoryStream();
		using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
		{
			foreach (var file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
			{
				var rel = Path.GetRelativePath(folder, file).Replace("\\", "/");
				if (rel.Split('/').Any(p => p.StartsWith("."))) continue;
				var ext = Path.GetExtension(file).ToLowerInvariant();
				if (SkippedExtensions.Contains(ext)) continue;
				// Audio and pictures are already compressed.
				var level = ext is ".osu" or ".wav" or ".txt" or ".osb" ? CompressionLevel.Optimal : CompressionLevel.NoCompression;
				zip.CreateEntryFromFile(file, rel, level);
				if (ms.Length > MaxBytes) break; // too big anyway; Submit says so
			}
		}
		return ms.ToArray();
	}

	/// <summary>Writes the server's ids into the local .osu files and the song list.</summary>
	private static void ApplyIds(string folder, Result result)
	{
		foreach (var d in result.Beatmaps)
		{
			var path = Path.Combine(folder, d.File).Replace("\\", "/");
			try
			{
				if (!File.Exists(path)) continue;
				var text = File.ReadAllText(path);
				File.WriteAllText(path, SetMetadata(text, d.BeatmapId, result.SetId), new UTF8Encoding(false));
			}
			catch (Exception e)
			{
				GD.PrintErr($"[Submit] Couldn't write the ids into {path}: {e.Message}");
				continue;
			}
			foreach (var legend in SettingsOperator.Beatmaps.Where(b => SamePath(b.Rawurl, path)))
			{
				legend.BeatmapID = d.BeatmapId;
				legend.BeatmapSetID = result.SetId;
				if (!result.Updated) RankStatusStore.Update(d.BeatmapId, RankStatusLegend.Unranked); // new set: saved as Unranked
				if (legend.ID == SettingsOperator.SessionConfig.SongID)
				{
					SettingsOperator.SessionConfig.BeatmapID = d.BeatmapId;
					SettingsOperator.SessionConfig.BeatmapSetID = result.SetId;
				}
			}
		}
	}

	private static bool SamePath(string a, string b) =>
		!string.IsNullOrEmpty(a) && string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.Ordinal);

	/// <summary>Sets BeatmapID / BeatmapSetID in [Metadata], keeping the file's line endings.</summary>
	public static string SetMetadata(string text, int beatmapId, int setId)
	{
		var nl = text.Contains("\r\n") ? "\r\n" : "\n";
		var lines = text.Replace("\r\n", "\n").Split('\n').ToList();
		var start = lines.FindIndex(l => l.Trim() == "[Metadata]");
		if (start < 0)
		{
			var at = lines.FindIndex(l => l.Trim() == "[Difficulty]");
			if (at < 0) at = lines.Count;
			lines.InsertRange(at, new[] { "[Metadata]", "" });
			start = at;
		}
		var end = lines.FindIndex(start + 1, l => l.TrimStart().StartsWith("["));
		if (end < 0) end = lines.Count;
		var pending = new Dictionary<string, string> { ["BeatmapID"] = beatmapId.ToString(), ["BeatmapSetID"] = setId.ToString() };
		for (int i = start + 1; i < end; i++)
		{
			var colon = lines[i].IndexOf(':');
			if (colon < 0) continue;
			var key = lines[i][..colon].Trim();
			if (pending.Remove(key, out var value)) lines[i] = $"{key}:{value}";
		}
		if (pending.Count > 0)
		{
			var at = end;
			while (at - 1 > start && string.IsNullOrWhiteSpace(lines[at - 1])) at--;
			lines.InsertRange(at, pending.Select(p => $"{p.Key}:{p.Value}"));
		}
		return string.Join(nl, lines);
	}

	private static List<string> Strings(JsonObject o, string key) =>
		(o?[key] as JsonArray)?.Select(n => n is JsonValue v && v.TryGetValue<string>(out var s) ? s : null).Where(s => s != null).ToList() ?? new();

	/// <summary>POST apiv2/submit/{path}. Returns (reply, null) or (reply-or-null, readable error).</summary>
	private static async Task<(JsonObject json, string error)> Send(string path, HttpContent content, bool allowConflict = false)
	{
		try
		{
			using var req = new HttpRequestMessage(HttpMethod.Post, PlayerApi.Base + "apiv2/submit/" + path);
			req.Headers.TryAddWithoutValidation("Username", ApiOperator.Username);
			req.Headers.TryAddWithoutValidation("Password", ApiOperator.PasswordHash);
			req.Headers.TryAddWithoutValidation("User-Agent", ApiOperator.AgentString ?? "Qlute");
			req.Content = content ?? new StringContent("");
			using var res = await Http.SendAsync(req);
			JsonObject json = null;
			try { json = JsonNode.Parse(await res.Content.ReadAsStringAsync()) as JsonObject; } catch { }
			// 409 = the server already has a different amount of the upload: carry on from what it says
			if (!res.IsSuccessStatusCode && !(allowConflict && (int)res.StatusCode == 409))
				return (json, OnlineApi.Str(json, "error") ?? $"The server said {(int)res.StatusCode}.");
			return json == null ? (null, "The server sent something unexpected.") : (json, null);
		}
		catch (Exception e)
		{
			return (null, $"Couldn't reach the server ({e.Message}).");
		}
	}
}
