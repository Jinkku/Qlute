using Godot;
using System;
using System.Collections;
using System.Text;

public partial class Signup : ColorRect
{
	private HttpRequest SignupHTTP { get; set; }
	private LineEdit UserLine { get; set; }
	private LineEdit PassLine { get; set; }
	private Button SignupButton { get; set; }
	private Label Warning { get; set; }
	private Label Warning2 { get; set; }
	private string username;
	private string password;
	private Tween tween;
	private SettingsOperator SettingsOperator { get; set; }
	private void _back()
	{
		animation(0);
	}
	private void _signup()
	{
		username = UserLine.Text;
		password = ApiOperator.ComputeSha256Hash(PassLine.Text);
		SettingsOperator.Sessioncfg["loggingin"] = true;
		ApiOperator.Username = username;
		ApiOperator.PasswordHash = password;
		string[] Headers = new string[] {
			$"username: {username}",
			$"password: {password}"
		};
		var msg = SignupHTTP.Request(SettingsOperator.GetSetting("api") + "apiv2/signup/", Headers,HttpClient.Method.Post);
	}
	private void complete(long result, long responseCode, string[] headers, byte[] body)
	{
		Modulate = new Color(1f, 1f, 1f, 1f);
		SettingsOperator.Sessioncfg["loggingin"] = false;
		if (responseCode == 200)
		{
			Notify.Post("Account created, welcome to Qlute!");
			ApiOperator.Login(username, password);
			animation(0);
		}
		else
		{
			Notify.Post("Account username is taken, try another one o-o");
		}

		GD.Print("Login API Response: " + Encoding.UTF8.GetString(body));
	}


	private void animation(int process)
	{
		tween?.Kill();
		tween = CreateTween();
		if (process == 0)
		{
			tween.TweenProperty(this, "modulate", new Color(1f, 1f, 1f, 0f), 0.2f);
			tween.TweenCallback(Callable.From(QueueFree));
		}
		else
		{
			tween.TweenProperty(this, "modulate", new Color(1f, 1f, 1f, 1f), 0.2f);
			tween.Play();
		}
	}

	// Called when the node enters the scene tree for the first time.

	public override void _Ready()
	{
		SettingsOperator = GetNode<SettingsOperator>("/root/SettingsOperator");
		Warning = GetNode<Label>("PanelContainer/VBoxContainer/Warning");
		Warning2 = GetNode<Label>("PanelContainer/VBoxContainer/Warning2");
		SignupButton = GetNode<Button>("PanelContainer/VBoxContainer/HBoxContainer/SignupButton");
		UserLine = GetNode<LineEdit>("PanelContainer/VBoxContainer/Username");
		PassLine = GetNode<LineEdit>("PanelContainer/VBoxContainer/Password");
		SignupHTTP = GetNode<HttpRequest>("PanelContainer/Signup");
		Modulate = new Color(1f, 1f, 1f, 0f);
		animation(1);
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		if (UserLine.Text.Length > 0 && PassLine.Text.Length > 7)
			SignupButton.Disabled = false;
		else
			SignupButton.Disabled = true;

		if (UserLine.Text.Length < 1)
		{
			Warning.Visible = true;
			Warning.Text = "Username needs at least a character.";
		}else Warning.Hide();


		if (PassLine.Text.Length < 8)
		{
			Warning2.Visible = true;
			Warning2.Text = "Password needs at least 8 characters";
		}
		else Warning2.Hide();
	}
}
