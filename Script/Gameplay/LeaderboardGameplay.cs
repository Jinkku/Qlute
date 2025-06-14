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
		SettingsOperator.Gameplaycfg["max"] = (int)GetMeta("max");
		SettingsOperator.Gameplaycfg["great"] = (int)GetMeta("good");
		SettingsOperator.Gameplaycfg["meh"] = (int)GetMeta("meh");
		SettingsOperator.Gameplaycfg["bad"] = (int)GetMeta("bad");
		SettingsOperator.Gameplaycfg["score"] = (int)GetMeta("score");
		SettingsOperator.Gameplaycfg["pp"] = (double)GetMeta("points");
		SettingsOperator.Gameplaycfg["accuracy"] = (SettingsOperator.Gameplaycfg["max"] + (SettingsOperator.Gameplaycfg["great"] / 2) + (SettingsOperator.Gameplaycfg["meh"] / 3)) / (SettingsOperator.Gameplaycfg["max"] + SettingsOperator.Gameplaycfg["great"] + SettingsOperator.Gameplaycfg["meh"] + SettingsOperator.Gameplaycfg["bad"]);
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

		if (!HasMeta("max"))
		{
			Max.Text = "0";
		}
		else
		{
			Max.Text = GetMeta("max").ToString();
		}

		if (!HasMeta("good"))
		{
			Great.Text = "0";
		}
		else
		{
			Great.Text = GetMeta("good").ToString();
		}

		if (!HasMeta("meh"))
		{
			Meh.Text = "0";
		}
		else
		{
			Meh.Text = GetMeta("meh").ToString();
		}

		if (!HasMeta("bad"))
		{
			Bad.Text = "0";
		}
		else
		{
			Bad.Text = GetMeta("bad").ToString();
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
			Score.Text = SettingsOperator.Gameplaycfg["score"].ToString("N0");
			Combo.Text = SettingsOperator.Gameplaycfg["combo"] + "x";
			Max.Text = SettingsOperator.Gameplaycfg["max"].ToString();
			Great.Text = SettingsOperator.Gameplaycfg["great"].ToString();
			Meh.Text = SettingsOperator.Gameplaycfg["meh"].ToString();
			Bad.Text = SettingsOperator.Gameplaycfg["bad"].ToString();
			refresh_rank();
		}
		else
		{
			refresh_info();

		}
	}
}
