using Godot;
using System;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text;
using System.Collections.Generic;

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
	// Called when the node enters the scene tree for the first time.
	public static HttpRequest LoginApi { get; set; }
	public  HttpRequest InfoApi { get; set; }
	public  HttpRequest SubmitApi { get; set; }
	public static HttpRequest LeaderboardAPI { get; set; }
	public  HttpRequest StatusChecker { get; set; }
	public static string Username = "Guest";
	public static string PasswordHash = null;
	private SettingsOperator SettingsOperator { get; set; }
	public static ApiOperator Instance { get; set; }
	public static string Beatmapapi = "https://catboy.best";
	/// <summary>
	/// Submits a score to the dedicated server.
	/// </summary>
	public void SubmitScore()
	{
		if (!SettingsOperator.SpectatorMode)
		{
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
		if (SettingsOperator.LeaderboardType == 1)
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
		RankApi.Connect("request_completed",  new Callable(this, nameof(RankOutput)));
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
			RankUpdate.Update((int)json["rank"], (int)json["points"]);
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
		SettingsOperator.Sessioncfg["ranknumber"] = n;
		SettingsOperator.ranked_points = json["points"].AsInt32();
	}

	public static int RankedStatus { get; set; }

	public static HttpRequest RankApi { get; set; }

	public static void CheckBeatmapRankStatus()
	{
		RankApi?.CancelRequest();
		if ((int)SettingsOperator.Sessioncfg["SongID"] != -1)
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

	public void Check_Info(string Username)
	{
		GD.Print("Using Profile: " + Username);
		InfoApi.Request(SettingsOperator.GetSetting("api") + "apiv2/getstat/full", new string[] { $"USERNAME: {Username}" });
	}

	private string _on_login_api_request_completed(long result, long responseCode, string[] headers, byte[] body)
	{
		GD.Print("Login API Response: " + Encoding.UTF8.GetString(body));
		try
		{
			Godot.Collections.Dictionary json = Json.ParseString(Encoding.UTF8.GetString(body)).AsGodotDictionary();
			if (json["notification"].ToString() != "")
			{
				Notify.Post(json["notification"].ToString());
			}
			SettingsOperator.Sessioncfg["loggingin"] = false;
			if ((bool)json["success"] && responseCode == 200)
			{
				Check_Info(Username);
				SettingsOperator.SetSetting("username", Username);
				Username = SettingsOperator.GetSetting("username")?.ToString();
				UPlayerName.Instance.Text = Username;
				SettingsOperator.SetSetting("password", PasswordHash);
				PasswordHash = SettingsOperator.GetSetting("password")?.ToString();
				//			}
				SettingsOperator.Sessioncfg["loggedin"] = true;
				return "Logged in!";
			}
			else
			{
				return "Incorrect Credentials";
			}
		}
		catch (Exception e)
		{
			GD.PrintErr("Error parsing JSON: " + e.Message);
			Notify.Post("Error parsing JSON: " + e.Message);
			SettingsOperator.Sessioncfg["loggingin"] = false;
			SettingsOperator.Sessioncfg["loggedin"] = false;
			return "Error parsing JSON";
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

	public static string Login(string username, string password)
	{
		SettingsOperator.Sessioncfg["loggingin"] = true;
		Username = username;
		PasswordHash = password;
		string[] Headers = new string[] {
			$"USERNAME: {username}",
			$"PASSWORD: {password}"
		};
		var msg = LoginApi.Request(SettingsOperator.GetSetting("api") + "apiv2/chkprofile", Headers);
		return msg.ToString();
	}
	
	private long Timer { get; set; }

	private void CheckStatus()
	{
		var Status = "N/A";
		var Title = SettingsOperator.Sessioncfg["beatmaptitle"]?.ToString() ?? "";
		var Artist = SettingsOperator.Sessioncfg["beatmapartist"]?.ToString() ?? "";
		var Difficulty = SettingsOperator.Sessioncfg["beatmapdiff"]?.ToString() ?? "";
		var Mapper = SettingsOperator.Sessioncfg["beatmapmapper"]?.ToString() ?? "";
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
	}
}
