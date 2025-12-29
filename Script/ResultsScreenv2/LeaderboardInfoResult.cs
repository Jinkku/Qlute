using Godot;
using System;
using System.Collections.Generic;

public partial class LeaderboardInfoResult : ScrollContainer
{
	private VBoxContainer Leaderboards { get; set; }
	private Label NoBeatmap { get; set; }
	private List<LeaderboardEntry> oldLeaderboardList { get; set; } = new List<LeaderboardEntry>();
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		Leaderboards = GetNode<VBoxContainer>("Rows");
		refreshl();
	}
	private void refreshl(){
		oldLeaderboardList = ApiOperator.LeaderboardList;
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
			Leaderboards.Visible = false;
		}
		else
		{
			Leaderboards.Visible = true;
			var ranknum = 1;
			foreach (var entry in ApiOperator.LeaderboardList)
			{
				var leaderboardEntry = GD.Load<PackedScene>("res://Panels/Overlays/RankLeaderboard.tscn").Instantiate().GetNode<RankLeaderboard>(".");
				leaderboardEntry.PlayerName = entry.username;
				leaderboardEntry.Rank = ranknum;
				leaderboardEntry.Perfect = entry.MAX;
				leaderboardEntry.Great = entry.GOOD;
				leaderboardEntry.Meh = entry.MEH;
				leaderboardEntry.Miss = entry.BAD;
				leaderboardEntry.Accuracy = entry.Accuracy;
				leaderboardEntry.pp = (int)entry.points;
				leaderboardEntry.Time = entry.time;
				leaderboardEntry.Combo = entry.combo;
				leaderboardEntry.Mods = entry.mods;
				leaderboardEntry.Score = entry.score;
				
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
