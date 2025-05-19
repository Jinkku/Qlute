using Godot;
using System;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text;
using System.Collections.Generic;

public partial class ApiOperator : Node
{
	// Called when the node enters the scene tree for the first time.
	public static HttpRequest LoginApi { get; set; }
	public static HttpRequest InfoApi { get; set; }
	public static HttpRequest SubmitApi { get; set; }
	public static string Username = "Guest";
	public static string PasswordHash = null;
	public static SettingsOperator SettingsOperator { get; set; }
	public static ApiOperator Instance { get; set; }
	public static string Beatmapapi = "https://catboy.best";
	public void SubmitScore(){
		int BeatmapID = (int)SettingsOperator.Sessioncfg["osubeatid"];
		int BeatmapSetID = (int)SettingsOperator.Sessioncfg["osubeatidset"];
		double MAX = SettingsOperator.Gameplaycfg["max"];
		double GREAT = SettingsOperator.Gameplaycfg["great"];
		double MEH = SettingsOperator.Gameplaycfg["meh"];
		double BAD = SettingsOperator.Gameplaycfg["bad"];
		double COMBO = SettingsOperator.Gameplaycfg["maxcombo"];
		double timetotal = SettingsOperator.Gameplaycfg["timetotal"] * 0.001;
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
			$"Mods: {ModsOperator.GetModAlias()}"
		};
		Notify.Post("Submitting...");
		SubmitApi.Request(SettingsOperator.GetSetting("api")+"apiv2/submitscore",Headers);
	}
	public override void _Ready()
	{	
		SettingsOperator = GetNode<SettingsOperator>("/root/SettingsOperator");
		Instance = this;
		Username = SettingsOperator.GetSetting("username")?.ToString();
		PasswordHash = SettingsOperator.GetSetting("password")?.ToString();
		LoginApi = new HttpRequest();
		InfoApi = new HttpRequest();
		SubmitApi = new HttpRequest();
		LoginApi.Timeout = 3;
		InfoApi.Timeout = 3;
		AddChild(InfoApi);
		AddChild(LoginApi);
		AddChild(SubmitApi);
		LoginApi.Connect("request_completed", new Callable(this, nameof(_on_login_api_request_completed)));
		InfoApi.Connect("request_completed", new Callable(this, nameof(_on_info_request_completed)));
		SubmitApi.Connect("request_completed",new Callable(this,nameof(_Submitrequest)));
		if ((Username != null || PasswordHash != null) && (bool)SettingsOperator.Sessioncfg["loggedin"] == false){
			UPlayerName.Instance.Text = Username;
			ApiOperator.Login(Username,PasswordHash);
		}
	}

	private void _Submitrequest(long result, long responseCode, string[] headers, byte[] body){
		Godot.Collections.Dictionary json = Json.ParseString(Encoding.UTF8.GetString(body)).AsGodotDictionary();
		if ((int)json["rankedmap"] >0 && (int)json["error"] == 0){
		RankUpdate.Update((int)json["rank"],(int)json["points"]);}
	}
	private void _on_info_request_completed(long result, long responseCode, string[] headers, byte[] body){
		Godot.Collections.Dictionary json = Json.ParseString(Encoding.UTF8.GetString(body)).AsGodotDictionary();
		
		Ranking.Instance.Text = "#" + json["rank"].AsInt32().ToString("N0");
		Ranking.Instance.Visible = true;
		string Ranknum = json["rank"].AsInt32().ToString("N0");
		if (int.TryParse(Ranknum, out int n)){
			if (n == 0){
				Ranking.Instance.Visible = false;
			}else{
			Ranking.Instance.Text = "#" + n.ToString("N0");}
		}
		SettingsOperator.Sessioncfg["ranknumber"] = n;
		SettingsOperator.ranked_points = json["points"].AsInt32();
		

	
	}
	public void Check_Info(string Username){
		GD.Print("Using Profile: "+ Username);
		InfoApi.Request(SettingsOperator.GetSetting("api") + "apiv2/getstat/full",new string[]{$"USERNAME: {Username}"});}
	
	private string _on_login_api_request_completed(long result, long responseCode, string[] headers, byte[] body){
		GD.Print("Login API Response: " + body.ToString());
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
		using (SHA256 sha256Hash = SHA256.Create())
		{
			byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(rawData));
			StringBuilder builder = new StringBuilder();
			foreach (byte b in bytes)
			{
				builder.Append(b.ToString("x2")); // Convert byte to hexadecimal
			}
			return builder.ToString();
		}
	}

	public static string Login(string username, string password){
		SettingsOperator.Sessioncfg["loggingin"] = true;
		Username = username;
		PasswordHash = password;
		string[] Headers = new string[] {
			$"USERNAME: {username}",
			$"PASSWORD: {password}"
		};
		var msg = LoginApi.Request(SettingsOperator.GetSetting("api") + "apiv2/chkprofile",Headers);
		return msg.ToString();
		}
}
