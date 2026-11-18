using Godot;
using System;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Game;
using DiscordRPC;
public class BeatmapDownloader
{
	public HttpRequest Request { get; set; }
	public string Url { get; set; }
	public string Info { get; set; }
	public int BeatmapID { get; set; }
	public int DownloadedBytes { get; set; }
	/// <summary>The set it came from, so a failed download can be retried from its notification.</summary>
	public BrowseCatalogLegend Source { get; set; }
	/// <summary>The progress card; completed or failed when the download ends.</summary>
	public NotificationLegend Notification { get; set; }
	/// <summary>
	/// Set for an update of a set the player already has (<see cref="ApiOperator.UpdateBeatmapSet"/>): that set's folder.
	/// Its old difficulties are removed when the download is done and the new version is unpacked into the same folder.
	/// </summary>
	public string ReplaceFolder { get; set; }
	/// <summary>Seconds since the last byte arrived (a stalled download is given up after a while).</summary>
	public double Idle { get; set; }
	public bool Done { get; set; }
}
public class LeaderboardEntry
{
	public string username { get; set; }
	public double points { get; set; }
	public int score { get; set; }
	public int combo { get; set; }
	public int rcombo { get; set; }
	public float Accuracy { get; set; }
	public int MAX { get; set; }
	public int GOOD { get; set; }
	public int MEH { get; set; }
	public int BAD { get; set; }
	public string mods { get; set; }
	public long time { get; set; }
	public float speed_multi { get; set; } = 1;
	public string FilePath { get; set; }
	/// <summary>Online score id (0 for local replays).</summary>
	public int id { get; set; }
	/// <summary>True if the server has this score's replay (apiv2/getreplay/{id}).</summary>
	public bool replay { get; set; }
	public bool Active { get; set; }
	/// <summary>Profile picture URL, sent with each getleaderboard row.</summary>
	public string pfp { get; set; }
	public Texture2D ProfilePicture { get; set; }
}
public partial class ApiOperator : Node
{
	public static HttpRequest LoginApi { get; set; }
	public  HttpRequest InfoApi { get; set; }
	public  HttpRequest SubmitApi { get; set; }
	public static HttpRequest LeaderboardAPI { get; set; }
	public  HttpRequest StatusChecker { get; set; }
	public static string Username = "Guest";
	public static string PasswordHash = null;
	private SettingsOperator SettingsOperator { get; set; }
	public static string NoticeText { get; set; }
	public static ApiOperator Instance { get; set; }
	public static bool Submitted = false;
	public static string Beatmapapi = "https://catboy.best";
	public static Texture2D PictureData { get; set; }
	public static Texture2D CardBorderData { get; set; }
	public static DiscordRpcClient Client { get; private set; }
	public static string AgentString { get; set; }
	public static string[] AgentHeaders { get; set; }
	[Signal]
	public delegate void DiscordEventHandler(bool enabled);

	/// <summary>
	/// Submits a score to the dedicated server. (New Version)
	/// </summary>
	public void SS()
	{
		if (!SettingsOperator.SpectatorMode)
		{
			Submitted = false;
			SettingsOperator.JustPlayedScore = false;
			GD.Print("Submitting score....");
			// Secret medals for how the play was finished (player's own clock / volume)
			if (DateTime.Now.Hour is >= 2 and < 5) MedalPopup.Claim("nightowl");
			if (AudioPlayer.MasterVol <= 0 || AudioServer.IsBusMute(0) || AudioServer.GetBusVolumeDb(0) <= -60)
				MedalPopup.Claim("muted");
			int BeatmapID = SettingsOperator.SessionConfig.BeatmapID;
			int BeatmapSetID = SettingsOperator.SessionConfig.BeatmapSetID;
			double MAX = SettingsOperator.Gameplaycfg.Max;
			double GREAT = SettingsOperator.Gameplaycfg.Great;
			double MEH = SettingsOperator.Gameplaycfg.Meh;
			double BAD = SettingsOperator.Gameplaycfg.Bad;
			double COMBO = SettingsOperator.Gameplaycfg.MaxCombo;
			double timetotal = SettingsOperator.Gameplaycfg.TimeTotal;
			string[] Headers = new string[] {
				$"BeatmapID: {BeatmapID}",
				$"BeatmapSetID: {BeatmapSetID}",
				// The server recognises the map by this (an edited or outdated copy can't submit); the ids are a hint.
				$"BeatmapHash: {BeatmapHash.Of(SettingsOperator.SessionConfig.BeatmapURL)}",
				$"USERNAME: {SettingsOperator.GetSetting("username")}",
				$"PASSWORD: {SettingsOperator.GetSetting("password")}",
				$"MAX: {MAX}",
				$"TAKEN: {timetotal}",
				$"GREAT: {GREAT}",
				$"MEH: {MEH}",
				$"BAD: {BAD}",
				$"COMBO: {COMBO}",
				$"Version: {ProjectSettings.GetSetting("application/config/version")}",
				$"Branch: {ProjectSettings.GetSetting("application/config/branch")}",
				$"MULTIPLIER: {Num.Str(AudioPlayer.Instance.PitchScale)}",
				$"Mods: {ModsOperator.GetModAlias()}",
				$"User-Agent: {AgentString}"
			};
			SubmitApi.Request(SettingsOperator.GetSetting("api") + "apiv2/ss", Headers,method: HttpClient.Method.Post, requestData: Replay.FileCache);
		}
	}
	/// <summary>
	/// Submits a score to the dedicated server. (Old Version, use SS than SubmitScore)
	/// </summary>
	public void SubmitScore()
	{
		if (!SettingsOperator.SpectatorMode)
		{
			Submitted = false;
			SettingsOperator.JustPlayedScore = false;
			GD.Print("Submitting score....");
			int BeatmapID = SettingsOperator.SessionConfig.BeatmapID;
			int BeatmapSetID = SettingsOperator.SessionConfig.BeatmapSetID;
			double MAX = SettingsOperator.Gameplaycfg.Max;
			double GREAT = SettingsOperator.Gameplaycfg.Great;
			double MEH = SettingsOperator.Gameplaycfg.Meh;
			double BAD = SettingsOperator.Gameplaycfg.Bad;
			double COMBO = SettingsOperator.Gameplaycfg.MaxCombo;
			double timetotal = SettingsOperator.Gameplaycfg.TimeTotal;
			string[] Headers = new string[] {
			$"BeatmapID: {BeatmapID}",
			$"BeatmapSetID: {BeatmapSetID}",
			$"USERNAME: {SettingsOperator.GetSetting("username")}",
			$"PASSWORD: {SettingsOperator.GetSetting("password")}",
			$"MAX: {MAX}",
			$"TAKEN: {timetotal}",
			$"GREAT: {GREAT}",
			$"MEH: {MEH}",
			$"BAD: {BAD}",
			$"COMBO: {COMBO}",
			$"Version: {ProjectSettings.GetSetting("application/config/version")}",
			$"Branch: {ProjectSettings.GetSetting("application/config/branch")}",
			$"Mods: {ModsOperator.GetModAlias()}",
			$"User-Agent: {AgentString}"
		};
			SubmitApi.Request(SettingsOperator.GetSetting("api") + "apiv2/submitscore", Headers);
		}
	}
	public static List<LeaderboardEntry> LeaderboardList = new List<LeaderboardEntry>();
	/// <summary>
	/// Why the online leaderboard is what it is (the server's Beatmap-Status): "ok", "modified" (our copy of the map
	/// differs from the server's: edited or outdated), "unknown" (not a map the server has), or "" (not known / older server).
	/// </summary>
	public static string LeaderboardMapStatus = "";
	/// <summary>For an "outdated" / "modified" copy: the server's set id and where to download its version ("" if none).</summary>
	public static int LeaderboardMapSetId;
	public static string LeaderboardMapDownload = "";
	private static int _leaderboardTicket;

	public async void _LeaderboardAPIDone(long result, long responseCode, string[] headers, byte[] body)
	{
		if (responseCode != 200)
		{
			GD.PrintErr("Leaderboard API request failed with response code: " + responseCode);
			Notify.Post("Failed to load leaderboard. Please try again later.",Type: NotificationIcons.NotificationType.Warning);
			LeaderboardStatus = 0; // Set status to not loaded
			return;
		}
		else
		{
			List<LeaderboardEntry> entries;
			try { entries = JsonSerializer.Deserialize<List<LeaderboardEntry>>(Encoding.UTF8.GetString(body)) ?? new(); }
			catch (Exception e)
			{
				GD.PrintErr("Leaderboard JSON was invalid: " + e.Message);
				LeaderboardStatus = 0;
				return;
			}
			// The server sends hit counts but no accuracy: work it out the same way the game does.
			foreach (var e in entries)
				if (e.Accuracy <= 0 && e.MAX + e.GOOD + e.MEH + e.BAD > 0)
					e.Accuracy = Gameplay.ReloadAccuracy(e.MAX, e.GOOD, e.MEH, e.BAD);
			int beatmapId = SettingsOperator.SessionConfig.BeatmapID;
			LeaderboardMapStatus = "";
			LeaderboardMapSetId = 0;
			LeaderboardMapDownload = "";
			foreach (var h in headers)
			{
				int colon = h.IndexOf(':');
				if (colon < 0) continue;
				string name = h[..colon].Trim(), value = h[(colon + 1)..].Trim();
				if (name.Equals("Beatmap-Status", StringComparison.OrdinalIgnoreCase)) LeaderboardMapStatus = value.ToLowerInvariant();
				else if (name.Equals("Beatmap-Setid", StringComparison.OrdinalIgnoreCase)) int.TryParse(value, out LeaderboardMapSetId);
				else if (name.Equals("Beatmap-Download", StringComparison.OrdinalIgnoreCase)) LeaderboardMapDownload = value;
			}
			LeaderboardList = entries;
			LeaderboardStatus = 2; // Set status to loaded
			// Profile pictures load in parallel. Picking another song meanwhile clears or replaces
			// LeaderboardList, so only apply a picture while our list is still the current one.
			bool Current() => SettingsOperator.SessionConfig.BeatmapID == beatmapId && ReferenceEquals(LeaderboardList, entries);
			var loads = new List<Task>(entries.Count);
			foreach (var leaderboardEntry in entries)
				loads.Add(LoadLeaderboardPicture(leaderboardEntry, Current));
			await Task.WhenAll(loads);
			GD.Print("Leaderboard loaded successfully.");
		}
	}
	private static async Task LoadLeaderboardPicture(LeaderboardEntry entry, Func<bool> current)
	{
		try
		{
			string url = entry.pfp;
			if (string.IsNullOrEmpty(url)) // older servers don't send it with the leaderboard
			{
				using var req = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Get, SettingsOperator.GetSetting("api") + "apiv2/getstat/pfp_path");
				req.Headers.Add("USERNAME", entry.username);
				using var res = await Http.SendAsync(req);
				url = JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement.GetProperty("url").GetString();
			}
			if (!current()) return;
			if (AvatarCache.TryGetValue(url, out var cached))
			{
				entry.ProfilePicture = cached;
				return;
			}
			await DownloadImage(url, texture =>
			{
				if (AvatarCache.Count >= AvatarCacheMax) AvatarCache.Clear();
				AvatarCache[url] = texture;
				if (current()) entry.ProfilePicture = texture;
			});
		}
		catch (Exception e)
		{
			GD.PrintErr($"Couldn't load a leaderboard picture: {e.Message}");
		}
	}

	// Avatars by URL, so switching back and forth between songs doesn't download the same pictures again.
	private const int AvatarCacheMax = 256;
	private static readonly Dictionary<string, ImageTexture> AvatarCache = new();

	/// <summary>One HttpClient for the whole game (a new one per request leaks sockets and redoes TLS).</summary>
	public static readonly System.Net.Http.HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };

	public static void ReloadLeaderboard(int BeatmapID)
	{
		if (SettingsOperator.NoConnectionToGameServer)
		{
			GD.Print("Skipping because connection to Game server is unverified");
			return;
		}
		else if (BeatmapID == -1 && SettingsOperator.LeaderboardType != 1) // online, the map is found by its hash
		{
			GD.Print("Skipping because BeatmapID is -1. Meaning there is not BeatmapID set.");
			return;
		}
		else if (SettingsOperator.LeaderboardType == 1)
		{
			// The server recognises the map by the hash of our .osu (BeatmapHash), so an edited or outdated copy gets no
			// leaderboard; the ids go along as a hint. Hashing a big map takes a moment, so it runs off the main thread.
			LeaderboardStatus = 1; // Set status to loading
			LeaderboardMapStatus = "";
			LeaderboardMapSetId = 0;
			LeaderboardMapDownload = "";
			string path = SettingsOperator.SessionConfig.BeatmapURL;
			int setId = SettingsOperator.SessionConfig.BeatmapSetID;
			int ticket = ++_leaderboardTicket;
			Task.Run(() => BeatmapHash.Of(path)).ContinueWith(t => Callable.From(() =>
			{
				if (ticket != _leaderboardTicket) return; // another map was picked meanwhile
				var headers = new List<string> { $"BEATMAPID: {BeatmapID}", $"BEATMAPSETID: {setId}", $"User-Agent: {AgentString}" };
				if (t.IsCompletedSuccessfully && !string.IsNullOrEmpty(t.Result)) headers.Add($"BEATMAPHASH: {t.Result}");
				LeaderboardAPI.CancelRequest(); // a request for the previous map may still be running
				LeaderboardAPI.Request(SettingsOperator.GetSetting("api") + "apiv2/getleaderboard", headers.ToArray());
			}).CallDeferred());
		}
		else
		{
			LeaderboardList.Clear();
			LeaderboardList = Replay.Search(BeatmapID);
			LeaderboardStatus = 2; // Set status to Loaded
		}
	}

	public static void InitDiscord()
	{
        
		if (Client != null)
		{
			KillDiscord();
		}

		Client = new DiscordRpcClient("1484981423684452684");
        

		Client.Initialize();    
	}

	public static void KillDiscord()
	{
        
		if (Client != null)
		{
			try
			{
				Client.ClearPresence();
				Client.Dispose(); // Kills client, background pipe threads, and closes the socket connections
			}
			catch (Exception e)
			{
				GD.PrintErr($"Error closing Discord client: {e.Message}");
			}
			finally
			{
				Client = null; // Zero out reference so _Process stops executing Invoke loops
			}
		}
	}

	
	private static string _appName;

	/// <summary>Rebuilds the User-Agent; called again once the background exe hash is done.</summary>
	public static void RefreshAgentString()
	{
		_appName ??= ProjectSettings.GetSetting("application/config/name").ToString();
		string agent = $"{_appName}-{SettingsOperator.GameChecksum}";
		AgentHeaders = new string[] { $"User-Agent: {agent}" };
		AgentString = agent;
	}

	public static int LeaderboardStatus = 0; // 0 = Not loaded, 1 = Loading, 2 = Loaded
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		RefreshAgentString();
		SettingsOperator = GetNode<SettingsOperator>("/root/SettingsOperator");
		Instance = this;
		var savedName = SettingsOperator.GetSetting("username")?.ToString();
		Username = string.IsNullOrWhiteSpace(savedName) ? "Guest" : savedName; // nothing saved: stay "Guest", not null
		PasswordHash = SettingsOperator.GetSetting("password")?.ToString();
		LoginApi = new HttpRequest();
		InfoApi = new HttpRequest();
		SubmitApi = new HttpRequest();
		LeaderboardAPI = new HttpRequest();
		StatusChecker = new HttpRequest();
		RankApi = new HttpRequest();
		LoginApi.Timeout = 3;
		InfoApi.Timeout = 3;
		LeaderboardAPI.Timeout = 3;
		AddChild(InfoApi);
		AddChild(LoginApi);
		AddChild(SubmitApi);
		AddChild(LeaderboardAPI);
		AddChild(StatusChecker);
		AddChild(RankApi);
		RankApi.Connect("request_completed", new Callable(this, nameof(RankOutput)));
		LoginApi.Connect("request_completed", new Callable(this, nameof(_on_login_api_request_completed)));
		LeaderboardAPI.Connect("request_completed", new Callable(this, nameof(_LeaderboardAPIDone)));
		InfoApi.Connect("request_completed", new Callable(this, nameof(_on_info_request_completed)));
		SubmitApi.Connect("request_completed", new Callable(this, nameof(_Submitrequest)));
		if (Check.CheckBoolValue(SettingsOperator.GetSetting("discord-rpc").ToString()))
		{
			InitDiscord();
		}
		if (Username != "Guest" && !string.IsNullOrEmpty(PasswordHash) && SettingsOperator.SessionConfig.Loggedin == false && Check.CheckBoolValue(SettingsOperator.GetSetting("stayloggedin").ToString()))
		{
			GD.Print("Attempting to log in with the saved account...");
			UPlayerName.Instance.Text = Username;

			ApiOperator.Login(Username, PasswordHash);
		}
		else
		{
			GD.Print("No username or password found or stay logged in is not ticked, skipping login.");
		}

		GetNotices();
	}

	public static List<EventLegend> EventData = new List<EventLegend>();
	private async void GetNotices()
	{
		// Notices are optional: a down or erroring server (500, timeout, bad JSON) just means no banners.
		for (int attempt = 0; attempt < 3; attempt++)
		{
			try
			{
				string result = await FetchData($"{SettingsOperator.GetSetting("api")}apiv2/getnotice");
				EventData = JsonSerializer.Deserialize<List<EventLegend>>(result) ?? new List<EventLegend>();
				return;
			}
			catch (Exception e)
			{
				GD.Print($"Couldn't load notices (attempt {attempt + 1}/3): {e.Message}");
			}
			await Task.Delay(10000);
		}
	}
	private void _Submitrequest(long result, long responseCode, string[] headers, byte[] body)
	{
		if (result != (long)HttpRequest.Result.Success || responseCode != 200)
		{
			GD.PrintErr($"Score submission failed (result {result}, HTTP {responseCode})");
			Notify.Post("Couldn't submit your score. The server didn't answer properly.", Type: NotificationIcons.NotificationType.Warning);
			return;
		}
		var text = Encoding.UTF8.GetString(body);
		var parser = new Json();
		if (parser.Parse(text) != Error.Ok || parser.Data.VariantType != Variant.Type.Dictionary)
		{
			GD.PrintErr("Score submission returned invalid JSON: " + parser.GetErrorMessage());
			Notify.Post("Couldn't submit your score. The server sent an unexpected answer.", Type: NotificationIcons.NotificationType.Warning);
			return;
		}
		Godot.Collections.Dictionary json = parser.Data.AsGodotDictionary();
		GD.Print(text);
		MedalPopup.ShowFromReply(text); // medals this play unlocked
		if (json.ContainsKey("rankedmap") && json.ContainsKey("error") && (int)json["rankedmap"] > 0 && (int)json["error"] == 0)
		{
			Submitted = true;
			RankUpdate.Update((int)json["rank"], (int)json["points"]);
			SettingsOperator.OldLevel = SettingsOperator.Level;
			SettingsOperator.OldScore = SettingsOperator.RankScore;
			SettingsOperator.OldAccuracy = SettingsOperator.OAccuracy;
			SettingsOperator.OldCombo = SettingsOperator.OCombo;
			SettingsOperator.Level = (int)json["level"];
			SettingsOperator.RankScore = (int)json["score"];
			SettingsOperator.OAccuracy = (float)json["accuracy"];
			SettingsOperator.OCombo = json["max_combo"].AsInt32();
			if (SettingsOperator.LeaderboardType == 1)
				ReloadLeaderboard(SettingsOperator.SessionConfig.BeatmapID);
		}
		if (json.ContainsKey("msg") && json["msg"].ToString() != "")
		{
			Notify.Post(json["msg"].ToString(), Type: NotificationIcons.NotificationType.Warning);
		}
	}
	private void _on_info_request_completed(long result, long responseCode, string[] headers, byte[] body)
	{
		Godot.Collections.Dictionary json = Json.ParseString(Encoding.UTF8.GetString(body)).AsGodotDictionary();

		Ranking.Instance.Text = "#" + json["rank"].AsInt32().ToString("N0");
		Ranking.Instance.Visible = true;
		string Ranknum = json["rank"].AsInt32().ToString("");
		if (int.TryParse(Ranknum, out int n))
		{
			if (n == 0)
			{
				Ranking.Instance.Visible = false;
			}
			else
			{
				Ranking.Instance.Text = "#" + n.ToString("N0");
			}
		}
		SettingsOperator.Rank = n;
		SettingsOperator.OldRank = n;
		SettingsOperator.RankScore = json["score"].AsInt32();
		SettingsOperator.OldScore = SettingsOperator.RankScore;
		SettingsOperator.ranked_points = json["points"].AsInt32();
		SettingsOperator.Oldpp = SettingsOperator.ranked_points;
		SettingsOperator.Level = json["level"].AsInt32();
		SettingsOperator.OldLevel = SettingsOperator.Level;
		SettingsOperator.OAccuracy = (float)json["accuracy"];
		SettingsOperator.OldAccuracy = SettingsOperator.OAccuracy;
		SettingsOperator.OCombo = json["max_combo"].AsInt32();
		SettingsOperator.OldCombo = SettingsOperator.OCombo;
		SettingsOperator.ProfilePictureURL = json["pfp_path"].AsString();
		SettingsOperator.CardBorderURL = json["cardborder"].AsString();
	}

	public static int RankedStatus { get; set; } = RankStatusLegend.Unknown;

	public static HttpRequest RankApi { get; set; }

	/// <summary>Online beatmap ID the pending RankApi request is for.</summary>
	private static int _rankRequestBeatmapId;

	/// <summary>
	/// Called when a map is selected: shows its saved rank status straight away, then asks the
	/// server. RankOutput only saves/updates if the server's answer is different.
	/// </summary>
	public static void CheckBeatmapRankStatus()
	{
		RankApi?.CancelRequest();
		int song = SettingsOperator.SessionConfig.SongID;
		if (song < 0 || song >= SettingsOperator.Beatmaps.Count) return;
		var legend = SettingsOperator.Beatmaps[song];
		RankedStatus = legend.RankStatus;
		_rankRequestBeatmapId = legend.BeatmapID;
		if (legend.BeatmapID > 0 && !SettingsOperator.NoConnectionToGameServer)
		{
			RankApi.Request($"{SettingsOperator.GetSetting("api")}apiv2/s/{legend.BeatmapID}",customHeaders: AgentHeaders);
		}
	}
	/// <summary>
	/// Server answer for the selected map. Saved (and shown) only if it differs from the saved
	/// status; a failed request keeps the saved status.
	/// </summary>
	public void RankOutput(long result, long responseCode, string[] headers, byte[] body)
	{
		if (result != (long)HttpRequest.Result.Success || responseCode != 200) return;
		try
		{
			var parsed = Json.ParseString(Encoding.UTF8.GetString(body));
			if (parsed.VariantType != Variant.Type.Dictionary) return;
			var dict = (Godot.Collections.Dictionary)parsed;
			if (!dict.TryGetValue("RankedStatus", out var value)) return;
			int status = RankStatusStore.Normalize((int)(long)value);
			RankStatusStore.Update(_rankRequestBeatmapId, status); // no-op if unchanged
		}
		catch (Exception e)
		{
			GD.PrintErr($"[RankStatus] Bad answer from the server: {e.Message}");
		}
	}

	public static void DownloadBeatmap(int id, int index) => DownloadBeatmap(Browse.BrowseCatalog[index]);

	/// <summary>Queues a set from apiv2/search (or apiv2/favorites) for download.</summary>
	public static void DownloadBeatmap(BrowseCatalogLegend BeatmapInfo)
	{
		var id = BeatmapInfo.id;
		var Info = $"{BeatmapInfo.artist} - {BeatmapInfo.title} from {BeatmapInfo.creator}";
		if (string.IsNullOrEmpty(BeatmapInfo.download_url))
		{
			Notify.Post("Can't download this beatmap right now", Description: Info, Type: NotificationIcons.NotificationType.Warning);
			return;
		}
		var url = BeatmapInfo.download_url.Replace(" ", "%20");
		if (DownloadList.Exists(d => !d.Done && (d.Url == url || d.BeatmapID == id)))
		{
			Notify.Post("Already downloading", Description: Info);
			return;
		}

		DownloadList.Add(new BeatmapDownloader
		{
			Url = url,
			Info = Info,
			BeatmapID = id,
			Source = BeatmapInfo
		});
	}

	public static List<BeatmapDownloader> DownloadList = new List<BeatmapDownloader>();

	/// <summary>
	/// Downloads the server's version of the selected map's set (its leaderboard said our copy is outdated or modified)
	/// and puts it in place of the old one: same folder, old difficulties removed, the same map selected afterwards.
	/// </summary>
	public static bool UpdateBeatmapSet(BeatmapLegend map)
	{
		if (map == null || string.IsNullOrEmpty(LeaderboardMapDownload)) return false;
		string folder = !string.IsNullOrEmpty(map.Path) && Directory.Exists(map.Path) ? map.Path : Path.GetDirectoryName(map.Rawurl ?? "");
		if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return false;
		string url = LeaderboardMapDownload.Replace(" ", "%20");
		string info = $"{map.Artist} - {map.Title} from {map.Mapper}";
		if (DownloadList.Exists(d => !d.Done && (d.Url == url || d.ReplaceFolder == folder)))
		{
			Notify.Post("Already updating", Description: info);
			return true;
		}
		DownloadList.Add(new BeatmapDownloader
		{
			Url = url,
			Info = info,
			BeatmapID = LeaderboardMapSetId > 0 ? LeaderboardMapSetId : Math.Abs(folder.GetHashCode()),
			ReplaceFolder = folder,
		});
		BeatmapListener.SelectAfterImport = (folder, map.BeatmapID, map.Version);
		return true;
	}

	/// <summary>Removes the old difficulties of a set being updated (their .osu files and library entries).</summary>
	private static void RemoveOldDifficulties(string folder)
	{
		string norm(string p) => Path.GetFullPath(p).TrimEnd('/', '\\');
		string target = norm(folder);
		foreach (var osu in Directory.GetFiles(folder, "*.osu"))
			File.Delete(osu);
		int removed = global::SettingsOperator.Beatmaps.RemoveAll(b => !string.IsNullOrEmpty(b.Rawurl) && norm(Path.GetDirectoryName(b.Rawurl)) == target);
		if (removed == 0) return;
		// The list shifted: renumber and select a map that still exists (the updated one is selected after import).
		for (int i = 0; i < global::SettingsOperator.Beatmaps.Count; i++) global::SettingsOperator.Beatmaps[i].ID = i;
		int keep = Math.Clamp(global::SettingsOperator.SessionConfig.SongID, 0, Math.Max(0, global::SettingsOperator.Beatmaps.Count - 1));
		global::SettingsOperator.SessionConfig.SongID = -1;
		global::SettingsOperator.SessionConfig.ReloadDB = true;
		if (global::SettingsOperator.Beatmaps.Count > 0) ((SceneTree)Engine.GetMainLoop()).Root.GetNode<global::SettingsOperator>("SettingsOperator").SelectSongID(keep);
	}

	/// <summary>A download with no new bytes for this long is given up (the server or connection hung).</summary>
	private const double DownloadStallSeconds = 60;

	private void _on_download_completed(long result, long responseCode, string[] headers, byte[] body, BeatmapDownloader downloader)
	{
		if (downloader.Done) return;
		string tempFilePath = Path.Combine(SettingsOperator.downloadsdir, $"{downloader.BeatmapID}.osz.tmp");
		string finalFilePath = Path.Combine(SettingsOperator.downloadsdir, $"{downloader.BeatmapID}.osz");
		// An update is unpacked into the set's own folder (the importer unpacks <name>.osz into beatmaps/<name>).
		if (downloader.ReplaceFolder != null)
			finalFilePath = Path.Combine(SettingsOperator.downloadsdir, Path.GetFileName(downloader.ReplaceFolder.TrimEnd('/', '\\')) + ".osz");
		string error = DownloadError(result, responseCode);
		if (error == null && !LooksLikeZip(tempFilePath))
			error = "The server sent something that isn't a beatmap file. Try again in a bit.";
		if (error == null)
		{
			try
			{
				if (downloader.ReplaceFolder != null) RemoveOldDifficulties(downloader.ReplaceFolder);
				if (File.Exists(finalFilePath)) File.Delete(finalFilePath);
				File.Move(tempFilePath, finalFilePath); // BeatmapListener picks it up and imports it
				GD.Print($"Beatmap downloaded successfully to: {finalFilePath}");
			}
			catch (Exception ex)
			{
				error = $"Couldn't save it: {ex.Message}";
			}
		}
		FinishDownload(downloader, error);
	}

	/// <summary>Ends a download either way: the notification always closes or shows the error, never stays stuck.</summary>
	private void FinishDownload(BeatmapDownloader downloader, string error)
	{
		downloader.Done = true;
		DownloadList.Remove(downloader);
		if (error == null)
		{
			downloader.Notification?.Complete("Downloaded", downloader.Info);
		}
		else
		{
			GD.PrintErr($"[Download] {downloader.Info}: {error}");
			try { File.Delete(Path.Combine(SettingsOperator.downloadsdir, $"{downloader.BeatmapID}.osz.tmp")); } catch { }
			var source = downloader.Source;
			string retry = source != null ? "\nClick to try again." : "";
			if (downloader.Notification != null)
			{
				downloader.Notification.Fail("Download failed", $"{downloader.Info}\n{error}{retry}");
				if (source != null) downloader.Notification.OnClick = () => DownloadBeatmap(source);
			}
			else
			{
				var n = Notify.Post("Download failed", Description: $"{downloader.Info}\n{error}{retry}", Type: NotificationIcons.NotificationType.Warning);
				if (source != null) n.OnClick = () => DownloadBeatmap(source);
			}
		}
		var request = downloader.Request;
		downloader.Request = null;
		if (request != null && IsInstanceValid(request)) request.QueueFree();
	}

	/// <summary>Why a download failed, in words a player understands, or null if it worked.</summary>
	public static string DownloadError(long result, long responseCode)
	{
		switch ((HttpRequest.Result)result)
		{
			case HttpRequest.Result.Success: break;
			case HttpRequest.Result.CantResolve:
			case HttpRequest.Result.CantConnect:
			case HttpRequest.Result.ConnectionError:
			case HttpRequest.Result.NoResponse:
				return "Couldn't reach the server. Check your internet connection.";
			case HttpRequest.Result.TlsHandshakeError:
				return "A secure connection to the server couldn't be made.";
			case HttpRequest.Result.Timeout:
				return "The server took too long to answer.";
			case HttpRequest.Result.DownloadFileCantOpen:
			case HttpRequest.Result.DownloadFileWriteError:
				return "Couldn't write the file. Is your disk full?";
			default:
				return $"The download broke off ({(HttpRequest.Result)result}).";
		}
		if (responseCode == 200) return null;
		return responseCode switch
		{
			404 => "This beatmap isn't on the server anymore.",
			403 => "The server won't let you download this beatmap.",
			429 => "Too many downloads at once. Wait a minute and try again.",
			>= 500 => $"The server had a problem (HTTP {responseCode}). Try again later.",
			_ => $"The server said HTTP {responseCode}."
		};
	}

	/// <summary>.osz files are zips ("PK"); an error page saved as the download isn't.</summary>
	private static bool LooksLikeZip(string path)
	{
		try
		{
			using var f = File.OpenRead(path);
			return f.Length > 4 && f.ReadByte() == 'P' && f.ReadByte() == 'K';
		}
		catch
		{
			return false;
		}
	}

	public void Check_Info(string Username)
	{
		InfoApi.Request(SettingsOperator.GetSetting("api") + "apiv2/getstat/full", new string[] { $"USERNAME: {Username}", $"User-Agent: {AgentString}" });
	}

	private void _on_login_api_request_completed(long result, long responseCode, string[] headers, byte[] body)
	{
		try
		{
			Godot.Collections.Dictionary json = new Godot.Collections.Dictionary();
			json.Add("notification", "");
			json.Add("success", 0);
			if (responseCode == 200) json = Json.ParseString(Encoding.UTF8.GetString(body)).AsGodotDictionary();
			if (responseCode == 200 && json["notification"].ToString() != "")
			{
				Notify.Post(json["notification"].ToString(), Type: NotificationIcons.NotificationType.Warning);
			}
			SettingsOperator.SessionConfig.Loggingin = false;
			if ((bool)json["success"] && responseCode == 200)
			{
				GetNotices();
				SettingsOperator.NoConnectionToGameServer = false;
				Check_Info(Username);
				SettingsOperator.SetSetting("username", Username);
				Username = SettingsOperator.GetSetting("username")?.ToString();
				UPlayerName.Instance.Text = Username;
				if (Check.CheckBoolValue(SettingsOperator.GetSetting("stayloggedin").ToString()))
					SettingsOperator.SetSetting("password", PasswordHash);
				else
					SettingsOperator.SetSetting("password", "");
				SettingsOperator.SessionConfig.Loggedin = true;
				NoticeText = "";
				ModerationApi.RefreshMe(); // team members get the admin panel button
				MedalPopup.CheckSoon(); // medals earned since last time (website, rank changes...)
				// The server now stores passwords salted; accounts still on the old unsalted hash are asked to pick a new one.
				// Logging in on the website sends them straight to the change-password page.
				if (json.ContainsKey("change_password") && (bool)json["change_password"])
					Notify.Post("We've upgraded how passwords are stored. Click here to change your password on the website.",
						uri: SettingsOperator.GetSetting("api") + "changepassword", Type: NotificationIcons.NotificationType.Warning);
			}
			else if (Username != "Guest" && PasswordHash != null && responseCode != 200)
			{
				// Couldn't reach the server (a wrong password still comes back as 200). Retry with backoff.
				SettingsOperator.NoConnectionToGameServer = true;
				ScheduleLoginRetry();
			}
			else
			{
				SettingsOperator.NoConnectionToGameServer = false;
				NoticeText = "Incorrect Credentials";
			}
		}
		catch (Exception e)
		{
			GD.PrintErr("Error parsing JSON: " + e.Message);
			Notify.Post("Error parsing JSON: " + e.Message);
			SettingsOperator.SessionConfig.Loggingin = false;
			SettingsOperator.SessionConfig.Loggedin = false;
		}
	}
	public static string ComputeSha256Hash(string rawData)
	{
		byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawData));
		StringBuilder builder = new StringBuilder();
		foreach (byte b in bytes)
		{
			builder.Append(b.ToString("x2")); // Convert byte to hexadecimal
		}
		return builder.ToString();
	}
	private const int MaxLoginRetries = 8;   // 2+4+8+16+32+60+60+60 s, about 4 minutes
	private const double MaxLoginRetryDelay = 60;
	private static int _loginRetries;
	private static double _loginRetryDelay = 2;
	private static int _loginGeneration; // a new manual login cancels pending retries

	private void ScheduleLoginRetry()
	{
		if (_loginRetries >= MaxLoginRetries)
		{
			NoticeText = "Couldn't reach the server. Try logging in again later.";
			return;
		}
		_loginRetries++;
		double delay = _loginRetryDelay;
		_loginRetryDelay = Math.Min(_loginRetryDelay * 2, MaxLoginRetryDelay);
		NoticeText = $"Couldn't reach the server, retrying in {delay:0} s...";
		int generation = _loginGeneration;
		string user = Username, pass = PasswordHash;
		GetTree().CreateTimer(delay, processAlways: true).Timeout += () =>
		{
			if (generation != _loginGeneration || SettingsOperator.SessionConfig.Loggedin
			    || SettingsOperator.SessionConfig.Loggingin || Username != user) return;
			SendLogin(user, pass);
		};
	}

	public static void Login(string username, string password)
	{
		// A login the player asked for starts the retry count over.
		_loginGeneration++;
		_loginRetries = 0;
		_loginRetryDelay = 2;
		SendLogin(username, password);
	}

	private static async void SendLogin(string username, string password)
	{
		SettingsOperator.SessionConfig.Loggingin = true;
		Username = username;
		PasswordHash = password;
		NoticeText = "Connecting...";
		// The server checks the exe hash in the User-Agent; on the first launch after an update it's still
		// being computed in the background, and logging in without it shows "version not supported".
		if (!ChecksumUtil.GameChecksumReady.IsCompleted)
			await Task.WhenAny(ChecksumUtil.GameChecksumReady, Task.Delay(TimeSpan.FromSeconds(15)));
		string[] Headers = new string[] {
			$"USERNAME: {username}",
			$"PASSWORD: {password}",
			$"User-Agent: {AgentString}"
		};
		NoticeText = "Connecting...";
		LoginApi.Request(SettingsOperator.GetSetting("api") + "apiv2/chkprofile", Headers);
	}
	
	private long Timer { get; set; }
	private string Status = "N/A";
	/// <summary>What the server last got (account + text) and when, so the status is only sent when it changes.</summary>
	private string _sentStatus;
	private long _sentStatusAt;
	/// <summary>The server shows players as online for 60 s after their last status, so refresh well before that.</summary>
	private const long StatusKeepAliveSeconds = 30;

	private void CheckStatus()
	{
		var Title = SettingsOperator.SessionConfig.BeatmapTitle?.ToString() ?? "";
		var Artist = SettingsOperator.SessionConfig.BeatmapArtist?.ToString() ?? "";
		var Difficulty = SettingsOperator.SessionConfig.BeatmapDifficultyName?.ToString() ?? "";
		var Mapper = SettingsOperator.SessionConfig.BeatmapMapper?.ToString() ?? "";
		if (GetTree().CurrentScene == null || SettingsOperator.NoConnectionToGameServer) return; // Skips everything if these were triggered.
		
		if (GetTree().CurrentScene.Name == "Gameplay")
		{
			Status = $"Playing {Artist} - {Title} by {Mapper} [{Difficulty}]";
		}
		else if (GetTree().CurrentScene.Name == "BrowseCatalog")
		{
			Status = "Browsing for new songs...";
		}
		else
		{
			Status = $"Listening to {Artist} - {Title}";
		}
		CheckStatusDC(); // local Discord pipe, not the network
		long now = DateTimeOffset.Now.ToUnixTimeSeconds();
		string key = Username + "\n" + Status;
		if (key == _sentStatus && now - _sentStatusAt < StatusKeepAliveSeconds) return;
		_sentStatus = key;
		_sentStatusAt = now;
		string[] Headers = new string[] {
		$"NOWPLAYING: {Status}",
		$"USERNAME: {Username}",
		$"PASSWORD: {PasswordHash}",
		$"User-Agent: {AgentString}"
		};
		StatusChecker.CancelRequest();
		StatusChecker.Request(SettingsOperator.GetSetting("api") + "apiv2/setstatus", Headers);
	}

	public void CheckStatusDC()
	{
		// Discord RPC Part
		try
		{
			if (Check.CheckBoolValue(SettingsOperator.GetSetting("discord-rpc").ToString()) && Client != null)
				Client.SetPresence(new RichPresence()
				{
					State = Status,
					Assets = new Assets()
					{
						LargeImageKey = "main-logo",
						LargeImageText = $"{Username} (#{SettingsOperator.Rank:N0})",
					}
				});
		}
		catch (Exception e)
		{
			GD.Print(e.Message);
		}
	}
	
	static bool IsJpeg(byte[] data)
	{
		return data != null &&
		       data.Length >= 3 &&
		       data[0] == 0xFF &&
		       data[1] == 0xD8 &&
		       data[2] == 0xFF;
	}

	static bool IsPng(byte[] data)
	{
		return data != null &&
		       data.Length >= 8 &&
		       data[0] == 0x89 &&
		       data[1] == 0x50 &&
		       data[2] == 0x4E &&
		       data[3] == 0x47 &&
		       data[4] == 0x0D &&
		       data[5] == 0x0A &&
		       data[6] == 0x1A &&
		       data[7] == 0x0A;
	}
	
	/// <summary>
	/// Fetch data from url in a async way.
	/// </summary>
	/// <param name="url"></param>
	/// <returns></returns>
	/// <summary>
	/// Downloads an online score's replay into the replays folder (so it also shows up in the
	/// local leaderboard). Returns the file path, or null on failure. Reuses an earlier download.
	/// </summary>
	public static async Task<string> DownloadReplay(LeaderboardEntry entry)
	{
		if (entry == null || entry.id <= 0 || !entry.replay) return null;
		static string Clean(string s) => Replay.CleanName(s);
		string path = $"{SettingsOperator.replaydir}/[{Clean(entry.username)}] [{Clean(SettingsOperator.SessionConfig.BeatmapMapper)}] {Clean(SettingsOperator.SessionConfig.BeatmapArtist)} - {Clean(SettingsOperator.SessionConfig.BeatmapTitle)} [{Clean(SettingsOperator.SessionConfig.BeatmapDifficultyName)}]-{entry.time}.qrf";
		if (System.IO.File.Exists(path)) return path;
		try
		{
			using var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(15) };
			client.DefaultRequestHeaders.Add("User-Agent", AgentString);
			var response = await client.GetAsync(SettingsOperator.GetSetting("api") + $"apiv2/getreplay/{entry.id}");
			if (!response.IsSuccessStatusCode) return null;
			string data = await response.Content.ReadAsStringAsync();
			if (!data.Contains("#BeatmapID: ")) return null; // not a replay
			System.IO.Directory.CreateDirectory(SettingsOperator.replaydir);
			await System.IO.File.WriteAllTextAsync(path, data);
			Replay.Parse(path);
			return path;
		}
		catch (Exception e)
		{
			GD.PrintErr($"[Qlute] Replay download failed: {e.Message}");
			return null;
		}
	}

	public static async Task<string> FetchData(string url)
	{
		return await Http.GetStringAsync(url);
	}
	
	public static async Task
		DownloadImage(string path, Action<ImageTexture> callback)
	{
		try
		{
			// 1. Download the image
			{
				byte[] imageBytes = await Http.GetByteArrayAsync(path);

				// 2. Create a Godot Image object
				Godot.Image image = new Godot.Image();
				using (var stream = new MemoryStream(imageBytes))
				{
					if (IsJpeg(imageBytes))
					{
						image.LoadJpgFromBuffer(imageBytes);
					}
					else if (IsPng(imageBytes))
					{
						image.LoadPngFromBuffer(imageBytes);
					}
					else
					{
						GD.PushError("Unknown or unsupported image format");
					}
				}

				// 3. Create a texture and display the image
				ImageTexture texture = new ImageTexture();
				texture.SetImage(image);

				// Call the callback function with the texture
				callback(texture);
			}
		}
		catch (Exception e)
		{
			GD.PrintErr("Error downloading or processing image: " + e.Message);
		}
	}
	public override void _Process(double delta)
	{
		Client?.Invoke();
		if (SettingsOperator.SessionConfig.Loggedin && DateTimeOffset.Now.ToUnixTimeSeconds() - Timer > 3)
		{
			Timer = DateTimeOffset.Now.ToUnixTimeSeconds();
			CheckStatus();
		}
		ServerNotifications.Tick(delta);

		foreach (var downloader in DownloadList.ToArray()) // FinishDownload removes from the list
		{
			if (downloader.Request == null)
			{
				Directory.CreateDirectory(SettingsOperator.downloadsdir);
				var downloadRequest = new HttpRequest();
				AddChild(downloadRequest);
				downloadRequest.Timeout = 0; // big sets take a while: the stall check below gives up on hung ones
				var d = downloader;
				downloadRequest.RequestCompleted += (long result, long responseCode, string[] headers, byte[] body) => _on_download_completed(result, responseCode, headers, body, d);
				downloadRequest.DownloadFile = Path.Combine(SettingsOperator.downloadsdir, $"{downloader.BeatmapID}.osz.tmp");
				downloader.Request = downloadRequest;
				downloader.Notification = Notify.Post("Downloading", Description: downloader.Info,
					ProgressGetter: () => IsInstanceValid(downloadRequest) ? downloadRequest.GetDownloadedBytes() : 0,
					Max: () => IsInstanceValid(downloadRequest) ? downloadRequest.GetBodySize() : -1,
					Type: NotificationIcons.NotificationType.Download);
				downloader.Notification.AutoComplete = false; // FinishDownload ends it
				var err = downloadRequest.Request(downloader.Url, method: HttpClient.Method.Get, customHeaders: AgentHeaders);
				if (err != Error.Ok) FinishDownload(downloader, $"The download couldn't start ({err}).");
			}
			else if (IsInstanceValid(downloader.Request))
			{
				int bytes = downloader.Request.GetDownloadedBytes();
				if (bytes != downloader.DownloadedBytes)
				{
					downloader.DownloadedBytes = bytes;
					downloader.Idle = 0;
				}
				else if ((downloader.Idle += delta) > DownloadStallSeconds)
				{
					downloader.Request.CancelRequest(); // doesn't emit RequestCompleted
					FinishDownload(downloader, "The download stopped moving. Check your connection and try again.");
				}
			}
		}
	}
}
