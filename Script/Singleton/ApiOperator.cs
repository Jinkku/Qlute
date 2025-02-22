using Godot;
using System;
using System.Security.Cryptography;
using System.Text;

public partial class ApiOperator : Node
{
	// Called when the node enters the scene tree for the first time.
	public static HttpRequest LoginApi { get; set; }
	public static HttpRequest RankingApi { get; set; }
	public static string Username = "Guest";
	public static string PasswordHash = null;
	public static SettingsOperator SettingsOperator { get; set; }
	public static ApiOperator Instance { get; set; }
	public override void _Ready()
	{	
		SettingsOperator = GetNode<SettingsOperator>("/root/SettingsOperator");
		Instance = this;
		Username = SettingsOperator.GetSetting("username")?.ToString();
		PasswordHash = SettingsOperator.GetSetting("password")?.ToString();
		LoginApi = new HttpRequest();
		RankingApi = new HttpRequest();
		LoginApi.Timeout = 3;
		RankingApi.Timeout = 3;
		AddChild(RankingApi);
		AddChild(LoginApi);

		LoginApi.Connect("request_completed", new Callable(this, nameof(_on_login_api_request_completed)));
		RankingApi.Connect("request_completed", new Callable(this, nameof(_on_Ranking_request_completed)));
		if ((Username != null || PasswordHash != null) && (bool)SettingsOperator.Sessioncfg["loggedin"] == false){
			UPlayerName.Instance.Text = Username;
			ApiOperator.Login(Username,PasswordHash);
		}
	}
	private void _on_Ranking_request_completed(long result, long responseCode, string[] headers, byte[] body){
		Ranking.Instance.Text = "#" + (string)Encoding.UTF8.GetString(body);
		Ranking.Instance.Visible = true;
		GD.Print("rankup!");
		string Ranknum = (string)Encoding.UTF8.GetString(body);
		GD.Print(Ranknum);
		if (int.TryParse(Ranknum, out int n)){
			if (n == 0){
				Ranking.Instance.Visible = false;
			}else{
			Ranking.Instance.Text = "#" + n.ToString("N0");}
		}
		SettingsOperator.Sessioncfg["ranknumber"] = n;
		

	
	}
	public void Check_Rank(string Username){
		GD.Print("Using Profile: "+ Username);
		RankingApi.Request(SettingsOperator.GetSetting("api") + "api/getstat?"+ Username +"?rank");}
	
	private string _on_login_api_request_completed(long result, long responseCode, string[] headers, byte[] body){
		Godot.Collections.Dictionary json = Json.ParseString(Encoding.UTF8.GetString(body)).AsGodotDictionary();
		SettingsOperator.Sessioncfg["loggingin"] = false;
		if ((bool)json["success"]) {
			Check_Rank(Username);
			SettingsOperator.SetSetting("username",Username);
			Username = SettingsOperator.GetSetting("username")?.ToString();
			UPlayerName.Instance.Text = Username;
			SettingsOperator.SetSetting("password",PasswordHash);
			PasswordHash = SettingsOperator.GetSetting("password")?.ToString();
			if ((bool)SettingsOperator.Sessioncfg["showaccountpro"] == true){
			//AniPlayer.Play("Drop out");
			//AniPlayer.Play("Drop in_Profile");
			SettingsOperator.Sessioncfg["showaccountpro"] = !(bool)SettingsOperator.Sessioncfg["showaccountpro"];}
			SettingsOperator.Sessioncfg["loggedin"] = true;
			return "Logged in!";
		} else {
			return "Incorrect Credentials";
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
		var msg = LoginApi.Request(SettingsOperator.GetSetting("api") + "api/chkprofile?"+ username +"?"+ password);
		return msg.ToString();
		}
	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}
}
