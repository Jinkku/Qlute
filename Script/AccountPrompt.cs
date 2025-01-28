using Godot;
using System;
using System.Security.Cryptography;
using System.Text;

public partial class AccountPrompt : Control
{
	public static LineEdit Password { get; set; }
	public static LineEdit User { get; set; }
	public static HttpRequest LoginApi { get; set; }
	public static HttpRequest RankingApi { get; set; }
	public string Username = "Guest";
	public string PasswordHash = null;
	public static SettingsOperator SettingsOperator { get; set; }
	public override void _Ready()
	{
		SettingsOperator = GetNode<SettingsOperator>("/root/SettingsOperator");
		RankingApi = GetNode<HttpRequest>("RankingApi");
		LoginApi = GetNode<HttpRequest>("LoginApi");
		Password = GetNode<LineEdit>("Password");
		User = GetNode<LineEdit>("Username");
		Username = SettingsOperator.GetSetting("username")?.ToString();
		PasswordHash = SettingsOperator.GetSetting("password")?.ToString();
		if ((Username != null || PasswordHash != null) && (bool)SettingsOperator.Sessioncfg["loggedin"] == false){
			User.Text = Username;
			Login();
		}
	}
	public void Login(){LoginApi.Request(SettingsOperator.GetSetting("api") + "api/chkprofile?"+ User.Text +"?"+ PasswordHash);}
	public void Check_Rank(){RankingApi.Request(SettingsOperator.GetSetting("api") + "api/getstat?"+ Username +"?rank");}
	
	private void _on_login_pressed(){
		PasswordHash = ComputeSha256Hash(Password.Text);   
		Label Notice = GetNode<Label>("Notice");
		Notice.Text = "Connecting...";
		Login();
		Username = User.Text;
	}
	private void _on_Ranking_request_completed(long result, long responseCode, string[] headers, byte[] body){
		Label Ranking = GetNode<Label>("%Ranking");
		Ranking.Visible = true;
		string Ranknum = (string)Encoding.UTF8.GetString(body);
		if (int.TryParse(Ranknum, out int n)){
			if (n == 0){
				Ranking.Visible = false;
			}else{
			Ranking.Text = "#"+n.ToString();}
		}
		SettingsOperator.Sessioncfg["ranknumber"] = n;
		

	
	}
	private void _on_login_api_request_completed(long result, long responseCode, string[] headers, byte[] body){
		Label Notice = GetNode<Label>("Notice");
		Godot.Collections.Dictionary json = Json.ParseString(Encoding.UTF8.GetString(body)).AsGodotDictionary();
		Label PlayerName = GetNode<Label>("%UPlayerName");
		if ((bool)json["success"]) {
			Notice.Text = "Logged in!";
			PlayerName.Text = Username;
			Check_Rank();
			SettingsOperator.SetSetting("username",Username);
			SettingsOperator.SetSetting("password",PasswordHash);
			AnimationPlayer AniPlayer = GetNode<AnimationPlayer>("%AccountPanelAnimation");
			if ((bool)SettingsOperator.Sessioncfg["showaccountpro"] == true){
			AniPlayer.Play("Drop out");
			AniPlayer.Play("Drop in_Profile");
			SettingsOperator.Sessioncfg["showaccountpro"] = !(bool)SettingsOperator.Sessioncfg["showaccountpro"];}
			SettingsOperator.Sessioncfg["loggedin"] = true;
		} else {
			Notice.Text = "Incorrect Credentials";
		}
	}
	static string ComputeSha256Hash(string rawData)
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
}
