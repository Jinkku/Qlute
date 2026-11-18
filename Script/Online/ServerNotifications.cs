using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

/// <summary>
/// The website's notifications (the bell: apiv2/notifications) shown in the game too, so a warning from the team,
/// a comment on your beatmap or a supporter gift isn't only seen on the website.
///
/// Polled while logged in (the live ws/v1/notify/ socket is website-only). Only notifications newer than the last one
/// this game showed for this account pop up; the first time an account is seen it gets one "N unread" summary instead
/// of a flood. Kinds the game already shows live are skipped: medals (MedalPopup) always, chat messages and invites
/// while the online chat is connected. Clicking a card opens its page on the website and marks it read there.
/// </summary>
public static class ServerNotifications
{
	private const double PollSeconds = 30;
	private const double FirstPollSeconds = 4;
	private const int MaxCards = 3;
	/// <summary>Setting holding "username:last shown id".</summary>
	public const string SeenSetting = "notifseen";

	private static readonly System.Net.Http.HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };
	private static string _user;
	private static double _wait;
	private static bool _busy;

	/// <summary>Called every frame from ApiOperator.</summary>
	public static void Tick(double delta)
	{
		if (!OnlineApi.Available)
		{
			_user = null;
			return;
		}
		if (_user != ApiOperator.Username)
		{
			_user = ApiOperator.Username; // just logged in (or switched account)
			_wait = FirstPollSeconds;
		}
		if (_busy || (_wait -= delta) > 0) return;
		_wait = PollSeconds;
		_ = Poll(_user);
	}

	private static async Task Poll(string user)
	{
		_busy = true;
		try
		{
			var res = await Http.SendAsync(PlayerApi.Authed(HttpMethod.Get, "apiv2/notifications"));
			if (!res.IsSuccessStatusCode) return; // logged out / server down: try again next time
			var json = JsonNode.Parse(await res.Content.ReadAsStringAsync()) as JsonObject;
			if (json == null || user != _user) return;
			var rows = (json["notifications"] as JsonArray)?.OfType<JsonObject>().ToList() ?? new();
			Show(user, rows, (int)OnlineApi.Long(json, "unread"));
		}
		catch (Exception e)
		{
			GD.Print($"[Notifications] Couldn't check the website's notifications: {e.Message}");
		}
		finally
		{
			_busy = false;
		}
	}

	private static void Show(string user, List<JsonObject> rows, int unread)
	{
		long seen = LastSeen(user);
		long newest = rows.Count == 0 ? seen : Math.Max(seen, rows.Max(r => OnlineApi.Long(r, "id")));
		if (seen < 0)
		{
			// First time this account plays here: one summary, not 30 old notifications.
			if (unread > 0)
				Notify.Post(unread == 1 ? "1 unread notification" : $"{unread} unread notifications",
					uri: PlayerApi.Base.TrimEnd('/') + "/", Description: "Click to see them on the website.").FromServer = true;
			SaveSeen(user, newest);
			return;
		}

		bool chatLive = OnlineChat.Instance?.Connected == true;
		var fresh = rows
			.Where(r => OnlineApi.Long(r, "id") > seen && !OnlineApi.Bool(r, "read"))
			.Where(r => !ShownLive(OnlineApi.Str(r, "kind") ?? "", chatLive))
			.OrderBy(r => OnlineApi.Long(r, "id"))
			.ToList();
		foreach (var r in fresh.Take(MaxCards)) Post(r);
		if (fresh.Count > MaxCards)
			Notify.Post($"{fresh.Count - MaxCards} more notifications", uri: PlayerApi.Base.TrimEnd('/') + "/",
				Description: "Click to see them all on the website.").FromServer = true;
		if (newest != seen) SaveSeen(user, newest);
	}

	/// <summary>Already shown by the game the moment it happens.</summary>
	private static bool ShownLive(string kind, bool chatLive) => kind switch
	{
		"medal" => true,                 // MedalPopup
		"dm" or "invite" or "friend" => chatLive, // OnlineChat
		_ => false
	};

	private static void Post(JsonObject r)
	{
		long id = OnlineApi.Long(r, "id");
		string kind = OnlineApi.Str(r, "kind") ?? "";
		string title = OnlineApi.Str(r, "title");
		if (string.IsNullOrWhiteSpace(title)) title = TitleFor(kind, OnlineApi.Str(r, "actor") ?? "");
		string link = OnlineApi.Str(r, "link") ?? "";
		string uri = link.StartsWith("/") ? PlayerApi.Base.TrimEnd('/') + link : link;
		if (!SafeLink.IsHttps(uri)) uri = ""; // only https pages from the server
		var n = Notify.Post(title, uri: uri, Type: IconFor(kind), Description: OnlineApi.Str(r, "text") ?? "");
		n.FromServer = true;
		n.OnClick = () => _ = MarkRead(id);
	}

	/// <summary>Same headings as the server's title_for (online/notifications.py), for servers that don't send one.</summary>
	public static string TitleFor(string kind, string actor) => kind switch
	{
		"dm" => string.IsNullOrEmpty(actor) ? "New message" : $"Message from {actor}",
		"invite" => "Chat room invite",
		"friend" => "New friend",
		"comment" => "New comment",
		"supporter" => "Qlute supporter",
		"medal" => "Medal unlocked!",
		"moderation" => "From the Qlute team",
		_ => "Notification"
	};

	private static NotificationIcons.NotificationType IconFor(string kind) => kind switch
	{
		"moderation" => NotificationIcons.NotificationType.Warning,
		"supporter" or "medal" => NotificationIcons.NotificationType.Gift,
		"dm" or "comment" => NotificationIcons.NotificationType.Send,
		"invite" or "friend" => NotificationIcons.NotificationType.UserOnline,
		_ => NotificationIcons.NotificationType.Info
	};

	private static async Task MarkRead(long id)
	{
		if (!OnlineApi.Available) return;
		try { await Http.SendAsync(PlayerApi.Authed(HttpMethod.Post, $"apiv2/notifications/read/{id}")); }
		catch (Exception e) { GD.Print($"[Notifications] Couldn't mark {id} read: {e.Message}"); }
	}

	/// <summary>Last notification id shown for <paramref name="user"/>, or -1 if this account was never checked here.</summary>
	private static long LastSeen(string user)
	{
		var value = SettingsOperator.GetSetting(SeenSetting)?.ToString() ?? "";
		int colon = value.LastIndexOf(':');
		if (colon <= 0 || value[..colon] != user) return -1;
		return long.TryParse(value[(colon + 1)..], out var id) ? id : -1;
	}

	private static void SaveSeen(string user, long id) => SettingsOperator.SetSetting(SeenSetting, $"{user}:{Math.Max(0, id)}");
}
