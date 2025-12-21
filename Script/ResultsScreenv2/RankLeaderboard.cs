using Godot;
using System;
using System.Collections.Generic;

public partial class RankLeaderboard : PanelContainer
{
	[Export]
	public string PlayerName { get; set; }
	[Export]
	public long Time { get; set; }
	[Export]
	public int Score { get; set; }
	[Export]
	public float Accuracy { get; set; }
	[Export]
	public int Perfect { get; set; }
	[Export]
	public int Great { get; set; }
	[Export]
	public int Meh { get; set; }
	[Export]
	public int Miss { get; set; }
	[Export]
	public int pp { get; set; }
	[Export]
	public int Rank { get; set; }
	[Export]
	public int Combo { get; set; }
	[Export]
	public string Mods { get; set; }
	
	private Label PlayerNameLabel { get; set; }
	private Label ScoreLabel { get; set; }
	private Label AccuracyLabel { get; set; }
	private Label PerfectLabel { get; set; }
	private Label GreatLabel { get; set; }
	private Label MehLabel { get; set; }
	private Label MissLabel { get; set; }
	private Label ppLabel { get; set; }
	private Label RankLabel { get; set; }
	private Label TimeLabel { get; set; }
	private Label ComboLabel { get; set; }
	private ModsResultsScreenShowcase ModsList { get; set; }
	
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		PlayerNameLabel = GetNode<Label>("Columns/Row1/Username");
		TimeLabel = GetNode<Label>("Columns/Row1/Achieved");
		ScoreLabel = GetNode<Label>("Columns/Score");
		AccuracyLabel = GetNode<Label>("Columns/Accuracy");
		PerfectLabel = GetNode<Label>("Columns/Perfect");
		GreatLabel = GetNode<Label>("Columns/Great");
		MehLabel = GetNode<Label>("Columns/Meh");
		MissLabel = GetNode<Label>("Columns/Miss");
		ppLabel = GetNode<Label>("Columns/pp");
		ComboLabel = GetNode<Label>("Columns/Combo");
		RankLabel = GetNode<Label>("Columns/Rank");
		ModsList = GetNode<ModsResultsScreenShowcase>("Columns/Mods");
		PlayerNameLabel.Text = PlayerName;
		ScoreLabel.Text = Score.ToString("N0");
		AccuracyLabel.Text = Accuracy.ToString("P2");
		PerfectLabel.Text = Perfect.ToString("N0");
		GreatLabel.Text = Great.ToString("N0");
		MehLabel.Text = Meh.ToString("N0");
		MissLabel.Text = Miss.ToString("N0");
		ppLabel.Text = $"{pp:N0}pp";
		RankLabel.Text = $"#{Rank:N0}";
		TimeLabel.Text = SettingsOperator.ParseTimeEpoch(Time);
		ComboLabel.Text = $"{Combo:N0}x";
		ModsList.ExternalMods = Mods;

		
	}
}
