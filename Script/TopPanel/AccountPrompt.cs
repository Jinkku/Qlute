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
	public VBoxContainer Log {get;set;}
	public VBoxContainer NotLog {get;set;}
	public Label Ranking {get;set;}
	public Label PerformanceNumber {get;set;}
	public Label PlayerName {get;set;}
	private SettingsOperator SettingsOperator { get; set; }
	private void _create()
	{
		var Signup = GD.Load<PackedScene>("res://Panels/Screens/Signup.tscn").Instantiate().GetNode<ColorRect>(".");
		GetTree().CurrentScene.AddChild(Signup);
		Hide();
	}
	public override void _Process(double _delta)
	{
		if ((bool)SettingsOperator.Sessioncfg["loggedin"] == true)
		{
			Log.Visible = true;
			NotLog.Visible = false;
		}
		else
		{
			Log.Visible = false;
			NotLog.Visible = true;
		}
	}
	public override void _Ready()
	{
		NotLog = GetNode<VBoxContainer>("AccPanel/NotLog");
		Log = GetNode<VBoxContainer>("AccPanel/Log");
		SettingsOperator = GetNode<SettingsOperator>("/root/SettingsOperator");
		Password = NotLog.GetNode<LineEdit>("Password");
		User = NotLog.GetNode<LineEdit>("Username");
	}
	private void _on_login_pressed(){
		PasswordHash = ApiOperator.ComputeSha256Hash(Password.Text);   
		Label Notice = NotLog.GetNode<Label>("HBoxContainer/Notice");
		Notice.Text = "Connecting...";
		Username = User.Text;
		Notice.Text = ApiOperator.Login(Username,PasswordHash);
	}
	private void _logout(){
		Log.Visible = false;
		NotLog.Visible = true;
		SettingsOperator.SetSetting("username",null);
		SettingsOperator.SetSetting("password",null);
		SettingsOperator.Sessioncfg["loggedin"] = false;
		Ranking.Visible = false;
		PlayerName.Text = "Guest";
	}
}
