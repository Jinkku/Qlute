using Godot;
using System;

public partial class Leaderboard : Button
{
	// Called when the node enters the scene tree for the first time.
	private Label Username { get; set; }
	private Label Score { get; set; }
	private Label Combo { get; set; }
	private Label Max { get; set; }
	private Label Good { get; set; }
	private Label Meh { get; set; }
	private Label Bad { get; set; }
	private Label Mods { get; set; }
	private Label pp { get; set; }
	private Label Time { get; set; }
	public void _stats()
	{
		SettingsOperator.ResetScore();
		SettingsOperator.Gameplaycfg.Max = (int)GetMeta("max");
		SettingsOperator.Gameplaycfg.Great = (int)GetMeta("good");
		SettingsOperator.Gameplaycfg.Meh = (int)GetMeta("meh");
		SettingsOperator.Gameplaycfg.Bad = (int)GetMeta("bad");
		SettingsOperator.Gameplaycfg.Score = (int)GetMeta("score");
		SettingsOperator.Gameplaycfg.MaxCombo = (int)GetMeta("combo");
		SettingsOperator.Gameplaycfg.pp = (float)GetMeta("points");
		Gameplay.ReloadAccuracy();
		GetNode<SceneTransition>("/root/Transition").Switch("res://Panels/Screens/ResultsScreen.tscn");
	}
	public override void _Ready()
	{
		Username = GetNode<Label>("HBoxContainer/UserInfo/Username");
		Score = GetNode<Label>("HBoxContainer/UserInfo/PlayScore/Score");
		Combo = GetNode<Label>("HBoxContainer/UserInfo/PlayScore/Combo");
		Time = GetNode<Label>("created");
		if (!HasMeta("username"))
		{
			Username.Text = "Unknown";
		}
		else
		{
			Username.Text = GetMeta("username").ToString();
		}
		if (!HasMeta("score"))
		{
			Score.Text = "0";
		}
		else
		{
			Score.Text = string.Format("{0:N0}", (int)GetMeta("score"));
		}
		if (!HasMeta("combo"))
		{
			Combo.Text = "0x";
		}
		else
		{
			Combo.Text = GetMeta("combo") + "x";
		}
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	long _lastTime = 0;
	public override void _Process(double _delta)
	{
		if (HasMeta("time"))
		{
			int seconds = (int)(DateTimeOffset.UtcNow.ToUnixTimeSeconds() - (long)GetMeta("time"));
			if (_lastTime != seconds)
			{
				_lastTime = seconds;
				string formattedTime;
				if (seconds < 60)
					formattedTime = $"{seconds}s";
				else if (seconds < 3600)
					formattedTime = $"{seconds / 60}m";
				else if (seconds < 86400)
					formattedTime = $"{seconds / 3600}h";
				else if (seconds < 2592000)
					formattedTime = $"{seconds / 86400}d";
				else if (seconds < 2419200) // 4 weeks
					formattedTime = $"{seconds / 604800}w";
				else if (seconds < 31557600) // 12 months
					formattedTime = $"{seconds / 31557600}m";
				else
					formattedTime = $"{seconds / 31557600}y";
				Time.Text = formattedTime;
			}
		}
	}
}
