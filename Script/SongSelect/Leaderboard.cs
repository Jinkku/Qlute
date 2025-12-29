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
	private LeaderboardEntry Info { get; set; }
	public void _stats()
	{
		SettingsOperator.ResetScore();

		SettingsOperator.Gameplaycfg.Max = Info.MAX;
		SettingsOperator.Gameplaycfg.Great = Info.GOOD;
		SettingsOperator.Gameplaycfg.Meh = Info.MEH;
		SettingsOperator.Gameplaycfg.Bad = Info.BAD;
		SettingsOperator.Gameplaycfg.Score = Info.score;
		SettingsOperator.Gameplaycfg.MaxCombo = Info.combo;
		SettingsOperator.Gameplaycfg.pp = Info.points;
		SettingsOperator.Gameplaycfg.Username = Info.username;
		SettingsOperator.Gameplaycfg.EpochTime = Info.time;
		if (HasMeta("rank"))
			SettingsOperator.Gameplaycfg.Rank = (int)GetMeta("rank");
		else
		{
			SettingsOperator.Gameplaycfg.Rank = 1;
		}
		SettingsOperator.Gameplaycfg.Accuracy = Gameplay.ReloadAccuracy(SettingsOperator.Gameplaycfg.Max, SettingsOperator.Gameplaycfg.Great, SettingsOperator.Gameplaycfg.Meh, SettingsOperator.Gameplaycfg.Bad);
		if (Info.FilePath != "")
		{
			Replay.FilePath = Info.FilePath;
		}
		ModsOperator.SetMods(Info.mods);
		GetNode<SceneTransition>("/root/Transition").Switch("res://Panels/Screens/ResultScreenv2.tscn");
	}
	public override void _Ready()
	{
		Info = ApiOperator.LeaderboardList[(int)GetMeta("ID")];
		Username = GetNode<Label>("HBoxContainer/UserInfo/Username");
		Score = GetNode<Label>("HBoxContainer/UserInfo/PlayScore/Score");
		Combo = GetNode<Label>("HBoxContainer/UserInfo/PlayScore/Combo");
		Mods = GetNode<Label>("HBoxContainer/Col2/Mods");
		Time = GetNode<Label>("HBoxContainer/Col2/created");
		Username.Text = Info.username;
		Score.Text = Info.score.ToString("N0");
		Combo.Text = Info.combo.ToString("N0") + "x";
		if (Info.mods != "")
		{
			Mods.Text = Info.mods;
		}
		else
		{
			Mods.Visible = false;
		}
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	long _lastTime = 0;
	public override void _Process(double _delta)
	{
		int seconds = (int)(DateTimeOffset.Now.ToUnixTimeSeconds() - Info.time);
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
