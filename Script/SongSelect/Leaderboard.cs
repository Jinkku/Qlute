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
		SettingsOperator.ResetScore(0);

		SettingsOperator.GameplayInfo[0].Max = Info.MAX;
		SettingsOperator.GameplayInfo[0].Great = Info.GOOD;
		SettingsOperator.GameplayInfo[0].Meh = Info.MEH;
		SettingsOperator.GameplayInfo[0].Bad = Info.BAD;
		SettingsOperator.GameplayInfo[0].Score = Info.score;
		SettingsOperator.GameplayInfo[0].MaxCombo = Info.combo;
		SettingsOperator.GameplayInfo[0].pp = Info.points;
		SettingsOperator.GameplayInfo[0].Accuracy = Gameplay.ReloadAccuracy(SettingsOperator.GameplayInfo[0].Max, SettingsOperator.GameplayInfo[0].Great, SettingsOperator.GameplayInfo[0].Meh, SettingsOperator.GameplayInfo[0].Bad);
		if (Info.FilePath != "")
		{
			Replay.FilePath = Info.FilePath;
		}
		ModsOperator.SetMods(Info.mods);
		GetNode<SceneTransition>("/root/Transition").Switch("res://Panels/Screens/ResultsScreen.tscn");
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
