using Godot;
using System;
using System.Collections.Generic;

public partial class LeaderboardInfo : ScrollContainer
{
	// Called when the node enters the scene tree for the first time.
	private VBoxContainer Leaderboards { get; set; }
	private Label NoBeatmap { get; set; }
	private List<LeaderboardEntry> oldLeaderboardList { get; set; } = new List<LeaderboardEntry>();

	public override void _Ready()
	{
		Leaderboards = GetNode<VBoxContainer>("Leaderboard");
		NoBeatmap = GetNode<Label>("NoBeatmap");
		refreshl();
	}
	private void refreshl(){
		oldLeaderboardList = ApiOperator.LeaderboardList;
		Random randomizer = new Random();
		for (int i = 0; i < 101; i++)
		{
			ApiOperator.LeaderboardList.Add(new LeaderboardEntry
			{
				username = $"Player{i.ToString("N0")}",
				points = randomizer.Next(1, 1000),
				score = randomizer.Next(1, 1823476),
				combo = randomizer.Next(1, 3265),
				MAX = randomizer.Next(1, 1000),
				GOOD = randomizer.Next(1, 1000),
				MEH = randomizer.Next(1, 1000),
				BAD = randomizer.Next(1, 1000),
				mods = "HD",
				time = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
			});
		}
		foreach (var child in Leaderboards.GetChildren())
		{
			if (child is Button button)
			{
				Leaderboards.RemoveChild(button);
				button.QueueFree();
			}
		}
		if (ApiOperator.LeaderboardList.Count < 1 && ApiOperator.LeaderboardStatus == 2)
		{
			NoBeatmap.Visible = true;
			Leaderboards.Visible = false;
		}
		else
		{
			NoBeatmap.Visible = false;
			Leaderboards.Visible = true;
			var ranknum = 1;
			foreach (var entry in ApiOperator.LeaderboardList)
			{
				var leaderboardEntry = GD.Load<PackedScene>("res://Panels/SongSelectButtons/Leaderboard.tscn").Instantiate().GetNode<Button>(".");
				leaderboardEntry.SetMeta("ID", ranknum-1);
				leaderboardEntry.SetMeta("rank", ranknum);
				Leaderboards.AddChild(leaderboardEntry);
				ranknum++;
			}
		}
	}
	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		if (oldLeaderboardList != ApiOperator.LeaderboardList)
		{
			refreshl();
		}
	}
}
