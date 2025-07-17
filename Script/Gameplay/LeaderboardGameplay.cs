using Godot;
using System;

public partial class LeaderboardGameplay : PanelContainer
{
	// Called when the node enters the scene tree for the first time.
	private Label Username { get; set; }
	private Label Score { get; set; }
	private Label Combo { get; set; }
	private Label Max { get; set; }
	private Label Great { get; set; }
	private Label Meh { get; set; }
	private Label Bad { get; set; }
	private Label Mods { get; set; }
	private Label pp { get; set; }
	private Label Time { get; set; }
	private Label Rank { get; set; }
	public void _stats()
	{
		SettingsOperator.ResetScore();
		SettingsOperator.Gameplaycfg.Max = (int)GetMeta("max");
		SettingsOperator.Gameplaycfg.Great = (int)GetMeta("good");
		SettingsOperator.Gameplaycfg.Meh = (int)GetMeta("meh");
		SettingsOperator.Gameplaycfg.Bad = (int)GetMeta("bad");
		SettingsOperator.Gameplaycfg.Score = (int)GetMeta("score");
		SettingsOperator.Gameplaycfg.pp = (int)GetMeta("points");
		SettingsOperator.Gameplaycfg.Accuracy = (SettingsOperator.Gameplaycfg.Max + (SettingsOperator.Gameplaycfg.Great / 2) + (SettingsOperator.Gameplaycfg.Meh / 3)) / (SettingsOperator.Gameplaycfg.Max + SettingsOperator.Gameplaycfg.Great + SettingsOperator.Gameplaycfg.Meh + SettingsOperator.Gameplaycfg.Bad);
		GetNode<SceneTransition>("/root/Transition").Switch("res://Panels/Screens/ResultsScreen.tscn");
	}
	public override void _Ready()
	{
		Username = GetNode<Label>("HBoxContainer/UserInfo/Username");
		Score = GetNode<Label>("HBoxContainer/UserInfo/PlayScore/Score");
		Combo = GetNode<Label>("HBoxContainer/UserInfo/PlayScore/Combo");
		Max = GetNode<Label>("HBoxContainer/UserInfo/PlayScore/MAX");
		Great = GetNode<Label>("HBoxContainer/UserInfo/PlayScore/GREAT");
		Meh = GetNode<Label>("HBoxContainer/UserInfo/PlayScore/MEH");
		Bad = GetNode<Label>("HBoxContainer/UserInfo/PlayScore/BAD");
		Rank = GetNode<Label>("HBoxContainer/Rank");

		refresh_info();
	}

	// Refreshes the leaderboard information.

	private void refresh_info()
	{
		if (!HasMeta("username"))
		{
			Username.Text = "Unknown";
		}
		else
		{
			Username.Text = GetMeta("username").ToString();
		}
		if (HasMeta("id"))
		{
			var info = ApiOperator.LeaderboardList[(int)GetMeta("id")];
			Score.Text = string.Format("{0:N0}", info.score);
			Combo.Text = $"{info.combo}x";
			Max.Text = info.MAX.ToString("N0");
			Great.Text = info.GOOD.ToString("N0");
			Meh.Text = info.MEH.ToString("N0");
			Bad.Text = info.BAD.ToString("N0");
		}
		else
		{
			Score.Text = "0";
			Combo.Text = "0";
			Max.Text = "0";
			Great.Text = "0";
			Meh.Text = "0";
			Bad.Text = "0";
		}
		refresh_rank();
	}
	private void refresh_rank()
	{
		if (!HasMeta("rank"))
		{
			Rank.Text = "0";
		}
		else
		{
			Rank.Text = "#" + GetMeta("rank").ToString();
		}
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		if (HasMeta("playing") && (bool)GetMeta("playing"))
		{
			Score.Text = SettingsOperator.Gameplaycfg.Score.ToString("N0");
			Combo.Text = SettingsOperator.Gameplaycfg.Combo + "x";
			Max.Text = SettingsOperator.Gameplaycfg.Max.ToString();
			Great.Text = SettingsOperator.Gameplaycfg.Great.ToString();
			Meh.Text = SettingsOperator.Gameplaycfg.Meh.ToString();
			Bad.Text = SettingsOperator.Gameplaycfg.Bad.ToString();
			refresh_rank();
		}
		else
		{
			refresh_info();
		}
	}
}
