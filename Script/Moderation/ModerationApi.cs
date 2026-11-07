using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

/// <summary>
/// Reports, bans and the Qlute team: the website's apiv2/mod/... endpoints (Chikara/moderation/views.py).
/// Team members (the ones with the team medal) get the admin panel; the server checks it on every call.
/// </summary>
public static class ModerationApi
{
	public class Report
	{
		public long Id;
		public string Reporter = "", Target = "", Category = "", CategoryText = "", Details = "", Context = "", Source = "", Status = "";
		public long Created, HandledAt;
		public string HandledBy = "", Note = "";
		public long TargetOpenReports;
		public bool TargetRestricted;
	}

	public class Ban
	{
		public long Id;
		public string Username = "", Reason = "", IssuedBy = "", TimeLeft = "", LiftedBy = "", LiftNote = "";
		public long Created, Expires, LiftedAt, ReportId;
		public bool Permanent, Active;
	}

	public class UserRow
	{
		public string Username = "", Staff = "", Status = "";
		public bool Restricted, CanBan;
		public long Rank, Points, Joined, LastSeen, Scores, ReportsSent;
		public double Accuracy;
		public List<Ban> Bans = new();
		public List<Report> ReportsAgainst = new();
	}

	public class StaffRow
	{
		public string Username = "", Role = "", AddedBy = "";
		public long Added;
	}

	public static readonly (string id, string text)[] Categories =
	{
		("cheating", "Cheating"),
		("profile", "Inappropriate name or profile"),
		("chat", "Chat abuse / harassment"),
		("multiplayer", "Multiplayer abuse"),
		("spam", "Spam"),
		("other", "Other"),
	};

	private static readonly System.Net.Http.HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };

	// ---------------------------------------------------------------- your own team status

	private static string _staffFor;
	public static string Role { get; private set; } = "";
	public static long OpenReports { get; private set; }

	/// <summary>True while logged in as a team member (checked with the server after login).</summary>
	public static bool IsStaff =>
		SettingsOperator.SessionConfig.Loggedin && _staffFor != null && _staffFor == ApiOperator.Username;

	public static bool IsAdmin => IsStaff && Role == "admin";

	/// <summary>Asks the server whether this account is on the team. Call after logging in.</summary>
	public static async void RefreshMe()
	{
		var who = ApiOperator.Username;
		var (json, _) = await Send("me");
		if (who != ApiOperator.Username) return; // logged into another account meanwhile
		var staff = OnlineApi.Bool(json, "staff");
		_staffFor = staff ? who : null;
		Role = staff ? OnlineApi.Str(json, "role") ?? "" : "";
		OpenReports = OnlineApi.Long(json, "open_reports");
	}

	// ---------------------------------------------------------------- everyone

	/// <summary>(message, error): one of them is null.</summary>
	public static async Task<(string message, string error)> SendReport(string target, string category, string details, string context)
	{
		var (json, error) = await Send("report", new JsonObject
		{
			["target"] = target, ["category"] = category, ["details"] = details, ["context"] = context,
		});
		return error != null ? (null, error) : (OnlineApi.Str(json, "message") ?? "Report sent.", null);
	}

	// ---------------------------------------------------------------- team

	public static async Task<(List<Report> list, long open, string error)> Reports(string status, int page = 0)
	{
		var (json, error) = await Send($"reports?status={status}&page={page}");
		if (error != null) return (null, 0, error);
		OpenReports = OnlineApi.Long(json, "open");
		return (Rows(json, ReadReport), OpenReports, null);
	}

	public static async Task<string> CloseReport(long id, string status, string note) =>
		(await Send($"report/{id}/close", new JsonObject { ["status"] = status, ["note"] = note })).error;

	public static async Task<string> ReopenReport(long id) => (await Send($"report/{id}/reopen", new JsonObject())).error;

	public static async Task<(List<UserRow> list, string error)> Users(string query, bool restrictedOnly)
	{
		var (json, error) = await Send($"users?query={Uri.EscapeDataString(query)}&restricted={(restrictedOnly ? 1 : 0)}");
		return error != null ? (null, error) : (Rows(json, ReadUser), null);
	}

	public static async Task<(UserRow user, string error)> User(string username)
	{
		var (json, error) = await Send("user/" + Uri.EscapeDataString(username));
		if (error != null) return (null, error);
		var u = ReadUser(json);
		u.CanBan = OnlineApi.Bool(json, "can_ban");
		u.Status = OnlineApi.Str(json, "status") ?? "";
		u.Accuracy = OnlineApi.Long(json, "accuracy"); // whole percent is plenty here
		u.Scores = OnlineApi.Long(json, "scores");
		u.ReportsSent = OnlineApi.Long(json, "reports_sent");
		u.Bans = (json["bans"] as JsonArray)?.OfType<JsonObject>().Select(ReadBan).ToList() ?? new();
		u.ReportsAgainst = (json["reports_against"] as JsonArray)?.OfType<JsonObject>().Select(ReadReport).ToList() ?? new();
		return (u, null);
	}

	/// <summary>hours = 0 bans permanently.</summary>
	public static async Task<string> BanUser(string username, string reason, double hours, long reportId, bool closeReports) =>
		(await Send("ban", new JsonObject
		{
			["username"] = username, ["reason"] = reason, ["hours"] = hours,
			["report"] = reportId > 0 ? reportId : null, ["close_reports"] = closeReports,
		})).error;

	public static async Task<string> Unban(string username, string note) =>
		(await Send("unban", new JsonObject { ["username"] = username, ["note"] = note })).error;

	public static async Task<(List<Ban> list, string error)> Bans(bool activeOnly, int page = 0)
	{
		var (json, error) = await Send($"bans?status={(activeOnly ? "active" : "all")}&page={page}");
		return error != null ? (null, error) : (Rows(json, ReadBan), null);
	}

	public static async Task<(List<StaffRow> list, bool canEdit, string error)> Staff()
	{
		var (json, error) = await Send("staff");
		if (error != null) return (null, false, error);
		return (Rows(json, o => new StaffRow
		{
			Username = OnlineApi.Str(o, "username") ?? "", Role = OnlineApi.Str(o, "role") ?? "",
			AddedBy = OnlineApi.Str(o, "added_by") ?? "", Added = OnlineApi.Long(o, "added"),
		}), OnlineApi.Bool(json, "can_edit"), null);
	}

	public static async Task<string> SetStaff(string username, string role) =>
		(await Send("staff/add", new JsonObject { ["username"] = username, ["role"] = role })).error;

	public static async Task<string> RemoveStaff(string username) =>
		(await Send("staff/remove", new JsonObject { ["username"] = username })).error;

	// ---------------------------------------------------------------- admins: home-screen notices (card borders are website-only)

	public class NoticeRow
	{
		public long Id, Position;
		public string Image = "", Text = "", Redirect = "", CreatedBy = "";
		public bool Active;
	}

	public static async Task<(List<NoticeRow> list, string error)> Notices()
	{
		var (json, error) = await Send("notices");
		return error != null ? (null, error) : (Rows(json, o => new NoticeRow
		{
			Id = OnlineApi.Long(o, "id"), Position = OnlineApi.Long(o, "position"), Image = OnlineApi.Str(o, "image") ?? "",
			Text = OnlineApi.Str(o, "text") ?? "", Redirect = OnlineApi.Str(o, "redirect") ?? "",
			CreatedBy = OnlineApi.Str(o, "created_by") ?? "", Active = OnlineApi.Bool(o, "active"),
		}), null);
	}

	/// <summary>imagePath: a file on this computer.</summary>
	public static async Task<string> AddNotice(string imagePath, string text, string redirect)
	{
		var (bytes, name, error) = NoticeImage(imagePath);
		if (error != null) return error;
		return (await Upload("notices/add", new() { ["text"] = text, ["redirect"] = redirect }, bytes, name)).error;
	}

	/// <summary>imagePath null keeps the picture.</summary>
	public static async Task<string> EditNotice(long id, string text, string redirect, string imagePath)
	{
		var fields = new Dictionary<string, string> { ["text"] = text, ["redirect"] = redirect };
		if (imagePath == null) return (await Upload($"notices/{id}/edit", fields, null, null)).error;
		var (bytes, name, error) = NoticeImage(imagePath);
		return error ?? (await Upload($"notices/{id}/edit", fields, bytes, name)).error;
	}

	public static async Task<string> ShowNotice(long id, bool active) =>
		(await Send($"notices/{id}/edit", new JsonObject { ["active"] = active })).error;

	public static async Task<string> MoveNotice(long id, int dir) =>
		(await Send($"notices/{id}/move", new JsonObject { ["dir"] = dir })).error;

	public static async Task<string> DeleteNotice(long id) => (await Send($"notices/{id}/delete", new JsonObject())).error;

	/// <summary>
	/// Notice pictures go up as they are when small; otherwise shrunk to 2048px and re-encoded as WebP,
	/// because the web server refuses uploads over 1 MB.
	/// </summary>
	private static (byte[] bytes, string name, string error) NoticeImage(string path)
	{
		try
		{
			var raw = System.IO.File.ReadAllBytes(path);
			var img = Image.LoadFromFile(path);
			if (img == null || img.IsEmpty()) return (null, null, "That file isn't an image we can read.");
			var big = Math.Max(img.GetWidth(), img.GetHeight());
			if (raw.Length < 900 * 1024 && big <= 2048) return (raw, System.IO.Path.GetFileName(path), null);
			if (big > 2048)
			{
				var f = 2048f / big;
				img.Resize(Math.Max(1, (int)(img.GetWidth() * f)), Math.Max(1, (int)(img.GetHeight() * f)), Image.Interpolation.Lanczos);
			}
			foreach (var q in new[] { 0.9f, 0.8f, 0.65f })
			{
				var webp = img.SaveWebpToBuffer(true, q);
				if (webp.Length < 950 * 1024) return (webp, "notice.webp", null);
			}
			return (null, null, "That image is too big even after shrinking it. Try a smaller one.");
		}
		catch (Exception e)
		{
			return (null, null, $"Couldn't read that file ({e.Message}).");
		}
	}

	private static async Task<(JsonObject json, string error)> Upload(string path, Dictionary<string, string> fields, byte[] image, string fileName)
	{
		var form = new MultipartFormDataContent();
		foreach (var (k, v) in fields) form.Add(new StringContent(v ?? ""), k);
		if (image != null) form.Add(new ByteArrayContent(image), "image", fileName ?? "image.png");
		return await Send(path, null, form);
	}

	// ---------------------------------------------------------------- plumbing

	/// <summary>GET when body is null, else POST JSON. Returns (reply, null) or (reply-or-null, readable error).</summary>
	private static async Task<(JsonObject json, string error)> Send(string path, JsonObject body = null, HttpContent content = null)
	{
		if (!SettingsOperator.SessionConfig.Loggedin || string.IsNullOrEmpty(ApiOperator.PasswordHash))
			return (null, "Log in first.");
		try
		{
			using var req = new HttpRequestMessage(body == null && content == null ? HttpMethod.Get : HttpMethod.Post, PlayerApi.Base + "apiv2/mod/" + path);
			req.Headers.TryAddWithoutValidation("Username", ApiOperator.Username);
			req.Headers.TryAddWithoutValidation("Password", ApiOperator.PasswordHash);
			req.Headers.TryAddWithoutValidation("User-Agent", ApiOperator.AgentString ?? "Qlute");
			if (content != null) req.Content = content;
			else if (body != null) req.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
			using var res = await Http.SendAsync(req);
			JsonObject json = null;
			try { json = JsonNode.Parse(await res.Content.ReadAsStringAsync()) as JsonObject; } catch { }
			if (!res.IsSuccessStatusCode)
				return (json, OnlineApi.Str(json, "error") ?? $"The server said {(int)res.StatusCode}.");
			return json == null ? (null, "The server sent something unexpected.") : (json, null);
		}
		catch (Exception e)
		{
			return (null, $"Couldn't reach the server ({e.Message}).");
		}
	}

	private static List<T> Rows<T>(JsonObject json, Func<JsonObject, T> read) =>
		(json?["results"] as JsonArray)?.OfType<JsonObject>().Select(read).ToList() ?? new();

	private static Report ReadReport(JsonObject o) => new()
	{
		Id = OnlineApi.Long(o, "id"), Reporter = OnlineApi.Str(o, "reporter") ?? "", Target = OnlineApi.Str(o, "target") ?? "",
		Category = OnlineApi.Str(o, "category") ?? "", CategoryText = OnlineApi.Str(o, "category_text") ?? "",
		Details = OnlineApi.Str(o, "details") ?? "", Context = OnlineApi.Str(o, "context") ?? "",
		Source = OnlineApi.Str(o, "source") ?? "", Status = OnlineApi.Str(o, "status") ?? "",
		Created = OnlineApi.Long(o, "created"), HandledBy = OnlineApi.Str(o, "handled_by") ?? "",
		HandledAt = OnlineApi.Long(o, "handled_at"), Note = OnlineApi.Str(o, "note") ?? "",
		TargetOpenReports = OnlineApi.Long(o, "target_open_reports"), TargetRestricted = OnlineApi.Bool(o, "target_restricted"),
	};

	private static Ban ReadBan(JsonObject o) => new()
	{
		Id = OnlineApi.Long(o, "id"), Username = OnlineApi.Str(o, "username") ?? "", Reason = OnlineApi.Str(o, "reason") ?? "",
		IssuedBy = OnlineApi.Str(o, "issued_by") ?? "", Created = OnlineApi.Long(o, "created"), Expires = OnlineApi.Long(o, "expires"),
		Permanent = OnlineApi.Bool(o, "permanent"), Active = OnlineApi.Bool(o, "active"), TimeLeft = OnlineApi.Str(o, "time_left") ?? "",
		LiftedBy = OnlineApi.Str(o, "lifted_by") ?? "", LiftedAt = OnlineApi.Long(o, "lifted_at"), LiftNote = OnlineApi.Str(o, "lift_note") ?? "",
		ReportId = OnlineApi.Long(o, "report"),
	};

	private static UserRow ReadUser(JsonObject o) => new()
	{
		Username = OnlineApi.Str(o, "username") ?? "", Restricted = OnlineApi.Bool(o, "restricted"),
		Rank = OnlineApi.Long(o, "rank"), Points = OnlineApi.Long(o, "points"), Joined = OnlineApi.Long(o, "joined"),
		LastSeen = OnlineApi.Long(o, "last_seen"), Staff = OnlineApi.Str(o, "staff") ?? "",
	};
}
