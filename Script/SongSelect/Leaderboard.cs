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
	public override void _Process(double delta)
	{
	}
}
