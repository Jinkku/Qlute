using Godot;
using System;
using System.Security.Cryptography;
using System.Text;

public partial class AccountPrompt : Control
{
	public static LineEdit Password { get; set; }
	public static LineEdit User { get; set; }
	private VBoxContainer Log {get;set;}
	private VBoxContainer NotLog {get;set;}
	private VBoxContainer Retring {get;set;}
	public Label PlayerName {get;set;}
	private Label Notice { get; set; }
	private SettingsOperator SettingsOperator { get; set; }
	private void _create()
	{
		var Signup = GD.Load<PackedScene>("res://Panels/Screens/Signup.tscn").Instantiate().GetNode<ColorRect>(".");
		GetTree().CurrentScene.AddChild(Signup);
		Hide();
	}
	public override void _Process(double _delta)
	{
		if ((bool)SettingsOperator.Sessioncfg["loggedin"] == true && !SettingsOperator.NoConnectionToGameServer)
		{
			Log.Visible = true;
			NotLog.Visible = false;
			Retring.Visible = false;
		}
		else if (SettingsOperator.NoConnectionToGameServer)
		{
			Log.Visible = false;
			NotLog.Visible = false;
			Retring.Visible = true;
		}
		else
		{
			Log.Visible = false;
			NotLog.Visible = true;
			Retring.Visible = false;
		}
		Notice.Text = ApiOperator.NoticeText;
	}
	public override void _Ready()
	{
		NotLog = GetNode<VBoxContainer>("AccPanel/NotLog");
		Log = GetNode<VBoxContainer>("AccPanel/Log");
		Retring = GetNode<VBoxContainer>("AccPanel/Retring");
		SettingsOperator = GetNode<SettingsOperator>("/root/SettingsOperator");
		Password = NotLog.GetNode<LineEdit>("Password");
		User = NotLog.GetNode<LineEdit>("Username");
		Notice = NotLog.GetNode<Label>("HBoxContainer/Notice");
	}
	private void _on_login_pressed(){
		ApiOperator.PasswordHash = ApiOperator.ComputeSha256Hash(Password.Text);   
		ApiOperator.Username = User.Text;
		ApiOperator.Login(ApiOperator.Username, ApiOperator.PasswordHash);
	}
	private void _logout(){
		Log.Visible = false;
		NotLog.Visible = true;
		Retring.Visible = true;
		SettingsOperator.NoConnectionToGameServer = false;
		SettingsOperator.SetSetting("username",null);
		SettingsOperator.SetSetting("password",null);
		SettingsOperator.Sessioncfg["loggedin"] = false;
		PlayerName.Text = "Guest";
	}
}
