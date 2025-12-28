using Godot;
using System;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text;
using System.Collections.Generic;
using System.IO;


public class BeatmapDownloader
{
	public HttpRequest Request { get; set; }
	public string Url { get; set; }
	public string Info { get; set; }
	public int BeatmapID { get; set; }
	public int DownloadedBytes { get; set; }
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
	public string FilePath { get; set; }
	public bool Active { get; set; }
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
	/// <summary>
	/// Submits a score to the dedicated server.
	/// </summary>
	public void SubmitScore()
	{
		if (!SettingsOperator.SpectatorMode)
		{
			Submitted = false;
			SettingsOperator.JustPlayedScore = false;
			GD.Print("Submitting score....");
			int BeatmapID = (int)SettingsOperator.Sessioncfg["osubeatid"];
			int BeatmapSetID = (int)SettingsOperator.Sessioncfg["osubeatidset"];
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
			$"Mods: {ModsOperator.GetModAlias()}"
		};
			SubmitApi.Request(SettingsOperator.GetSetting("api") + "apiv2/submitscore", Headers);
		}
	}
	public static List<LeaderboardEntry> LeaderboardList = new List<LeaderboardEntry>();
	public void _LeaderboardAPIDone(long result, long responseCode, string[] headers, byte[] body)
	{
		if (responseCode != 200)
		{
			GD.PrintErr("Leaderboard API request failed with response code: " + responseCode);
			Notify.Post("Failed to load leaderboard. Please try again later.");
			LeaderboardStatus = 0; // Set status to not loaded
			return;
		}
		else
		{
			LeaderboardList.Clear();
			LeaderboardStatus = 2; // Set status to loaded
			LeaderboardList = JsonSerializer.Deserialize<List<LeaderboardEntry>>((string)Encoding.UTF8.GetString(body));
			GD.Print("Leaderboard loaded successfully.");
		}

		for (int i = 0; i < LeaderboardList.Count; i++)
		{
			var entry = LeaderboardList[i];
			entry.Accuracy = Gameplay.ReloadAccuracy(entry.MAX, entry.GOOD, entry.MEH, entry.BAD);
		}
	}
	public static void ReloadLeaderboard(int BeatmapID)
	{
		if (SettingsOperator.NoConnectionToGameServer)
		{
			GD.Print("Skipping because connection to Game server is unverified");
			return;
		}
		else if (SettingsOperator.LeaderboardType == 1)
		{
			LeaderboardAPI.Request(SettingsOperator.GetSetting("api") + "apiv2/getleaderboard", new string[] { $"BEATMAPID: {BeatmapID}" });
			LeaderboardStatus = 1; // Set status to loading
		}
		else
		{
			LeaderboardList.Clear();
			LeaderboardList = Replay.Search(BeatmapID);
			LeaderboardStatus = 2; // Set status to Loaded
		}
	}
	public static int LeaderboardStatus = 0; // 0 = Not loaded, 1 = Loading, 2 = Loaded
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		SettingsOperator = GetNode<SettingsOperator>("/root/SettingsOperator");
		Instance = this;
		Username = SettingsOperator.GetSetting("username")?.ToString();
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
		if ((Username != null || PasswordHash != null) && (bool)SettingsOperator.Sessioncfg["loggedin"] == false)
		{
			GD.Print("Attempting to login with username: " + Username);
			UPlayerName.Instance.Text = Username;

			ApiOperator.Login(Username, PasswordHash);
		}
		else
		{
			GD.Print("No username or password found, skipping login.");
		}
	}
	private void _Submitrequest(long result, long responseCode, string[] headers, byte[] body)
	{
		Godot.Collections.Dictionary json = Json.ParseString(Encoding.UTF8.GetString(body)).AsGodotDictionary();
		if ((int)json["rankedmap"] > 0 && (int)json["error"] == 0)
		{
			Submitted = true;
			RankUpdate.Update((int)json["rank"], (int)json["points"]);
			SettingsOperator.OldLevel = SettingsOperator.Level;
			SettingsOperator.OldScore = SettingsOperator.RankScore;
			SettingsOperator.OldAccuracy = SettingsOperator.OAccuracy;
			SettingsOperator.OldCombo = SettingsOperator.OCombo;
			SettingsOperator.Level = (int)json["level"];
			SettingsOperator.RankScore = (int)json["score"];
			SettingsOperator.OAccuracy = json["accuracy"].AsInt32();
			SettingsOperator.OCombo = json["max_combo"].AsInt32();

		}
		if (json["msg"].ToString() != "")
		{
			Notify.Post(json["msg"].ToString());
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
		SettingsOperator.OAccuracy = json["accuracy"].AsInt32();
		SettingsOperator.OldAccuracy = SettingsOperator.OAccuracy;
		SettingsOperator.OCombo = json["max_combo"].AsInt32();
		SettingsOperator.OldCombo = SettingsOperator.OCombo;

	}

	public static int RankedStatus { get; set; }

	public static HttpRequest RankApi { get; set; }

	public static void CheckBeatmapRankStatus()
	{
		RankApi?.CancelRequest();
		if (SettingsOperator.SongID != -1 && !SettingsOperator.NoConnectionToGameServer)
		{
			RankApi.Request($"{Beatmapapi}/api/s/{SettingsOperator.Sessioncfg["osubeatidset"]}");
		}
	}
	/// <summary>
	/// Produces an output of the RankedStatus (ex. 1 == Ranked, etc.)
	/// </summary>
	/// <param name="result"></param>
	/// <param name="responseCode"></param>
	/// <param name="headers"></param>
	/// <param name="body"></param>
	public void RankOutput(long result, long responseCode, string[] headers, byte[] body)
	{
		// Convert byte[] → string
		string jsonString = Encoding.UTF8.GetString(body);

		// Parse the JSON (returns Variant)
		Variant parsed = Json.ParseString(jsonString);

		// Cast Variant to Dictionary
		var dict = (Godot.Collections.Dictionary)parsed;

		// Get RankedStatus (Variant → long → int)
		RankedStatus = (int)(long)dict["RankedStatus"];
		if (RankedStatus == -1)
		{
			RankedStatus = 0;
		}
		else if (RankedStatus != 1)
		{
			RankedStatus = 2;
		}
	}

	public static void DownloadBeatmap(int id, int index)
	{
		BrowseCatalogLegend BeatmapInfo = Browse.BrowseCatalog[index];
		var Artist = BeatmapInfo.artist;
		var Title = BeatmapInfo.title;
		var Creator = BeatmapInfo.creator;
		var Info = $"{Artist} - {Title} from {Creator}";
		string url = Beatmapapi + "/d/" + id;
		if (DownloadList.Exists(d => d.Url == url))
		{
			Notify.Post("This beatmap is already being downloaded.");
			return;
		}
		DownloadList.Add(new BeatmapDownloader
		{
			Url = url,
			Info = Info,
			BeatmapID = id
		});
	}

	public static List<BeatmapDownloader> DownloadList = new List<BeatmapDownloader>();

	private void _on_download_completed(long result, long responseCode, string[] headers, byte[] body, int beatmap = 0)
	{
		if (responseCode == 200)
		{
			try
			{
				int id = beatmap; // Assuming ID is passed in headers
				string tempFilePath = Path.Combine(SettingsOperator.downloadsdir, $"{id}.osz.tmp");
				string finalFilePath = Path.Combine(SettingsOperator.downloadsdir, $"{id}.osz");

				if (File.Exists(tempFilePath))
				{
					File.Move(tempFilePath, finalFilePath);
				}

				GD.Print($"Beatmap downloaded successfully to: {finalFilePath}");
			}
			catch (Exception ex)
			{
				Notify.Post($"Error saving beatmap: {ex.Message}");
			}
		}
		else
		{
			Notify.Post($"Failed to download beatmap. HTTP Status: {responseCode}. You might be throttled ;w;");
		}
	}



	public void Check_Info(string Username)
	{
		GD.Print("Using Profile: " + Username);
		InfoApi.Request(SettingsOperator.GetSetting("api") + "apiv2/getstat/full", new string[] { $"USERNAME: {Username}" });
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
				Notify.Post(json["notification"].ToString());
			}
			SettingsOperator.Sessioncfg["loggingin"] = false;
			if ((bool)json["success"] && responseCode == 200)
			{
				SettingsOperator.NoConnectionToGameServer = false;
				Check_Info(Username);
				SettingsOperator.SetSetting("username", Username);
				Username = SettingsOperator.GetSetting("username")?.ToString();
				UPlayerName.Instance.Text = Username;
				SettingsOperator.SetSetting("password", PasswordHash);
				PasswordHash = SettingsOperator.GetSetting("password")?.ToString();
				SettingsOperator.Sessioncfg["loggedin"] = true;
				NoticeText = "";
			}
			else if (Username != "Guest" && PasswordHash != null && responseCode != 200)
			{
				NoticeText = "Retring...";
				SettingsOperator.NoConnectionToGameServer = true;
				ApiOperator.Login(Username, PasswordHash);
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
			SettingsOperator.Sessioncfg["loggingin"] = false;
			SettingsOperator.Sessioncfg["loggedin"] = false;
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
	public static void Login(string username, string password)
	{
		SettingsOperator.Sessioncfg["loggingin"] = true;
		Username = username;
		PasswordHash = password;
		string[] Headers = new string[] {
			$"USERNAME: {username}",
			$"PASSWORD: {password}"
		};
		NoticeText = "Connecting...";
		var msg = LoginApi.Request(SettingsOperator.GetSetting("api") + "apiv2/chkprofile", Headers);
	}
	
	private long Timer { get; set; }

	private void CheckStatus()
	{
		var Status = "N/A";
		var Title = SettingsOperator.Sessioncfg["beatmaptitle"]?.ToString() ?? "";
		var Artist = SettingsOperator.Sessioncfg["beatmapartist"]?.ToString() ?? "";
		var Difficulty = SettingsOperator.Sessioncfg["beatmapdiff"]?.ToString() ?? "";
		var Mapper = SettingsOperator.Sessioncfg["beatmapmapper"]?.ToString() ?? "";
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
		string[] Headers = new string[] {
		$"NOWPLAYING: {Status}",
		$"USERNAME: {SettingsOperator.GetSetting("username")}",
		$"PASSWORD: {SettingsOperator.GetSetting("password")}",
		};
		StatusChecker.Request(SettingsOperator.GetSetting("api") + "apiv2/setstatus", Headers);
	}
	public override void _Process(double delta)
	{
		if ((bool)SettingsOperator.Sessioncfg["loggedin"] && DateTimeOffset.Now.ToUnixTimeSeconds() - Timer > 3)
		{
			Timer = DateTimeOffset.Now.ToUnixTimeSeconds();
			CheckStatus();
		}

		foreach (var downloader in DownloadList)
		{
			if (downloader.Request == null)
			{
				int id = int.Parse(downloader.Url.Substring(downloader.Url.LastIndexOf('/') + 1));
				string url = downloader.Url;
				GD.Print(url);
				string Info = downloader.Info;

				var downloadRequest = new HttpRequest();
				AddChild(downloadRequest);
				downloadRequest.Timeout = 0;
				downloadRequest.RequestCompleted += (long result, long responseCode, string[] headers, byte[] body) => _on_download_completed(result, responseCode, headers, body, id);
				downloadRequest.DownloadFile = Path.Combine(SettingsOperator.downloadsdir, $"{id}.osz.tmp");
				downloadRequest.Request(url, null, Godot.HttpClient.Method.Get);

				downloader.Request = downloadRequest;

				Notify.Post($"Downloading {Info}", ProgressGetter: () => downloadRequest.GetDownloadedBytes(), Max: () => downloadRequest.GetBodySize());
			}
			else
			{
				if (downloader.DownloadedBytes != downloader.Request.GetDownloadedBytes())
				{
					downloader.DownloadedBytes = downloader.Request.GetDownloadedBytes();
					downloader.Request.Timeout++;
				}
			}
		}
	}
}
