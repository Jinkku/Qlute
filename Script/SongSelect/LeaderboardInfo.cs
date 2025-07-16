using Godot;
using System;
using System.Collections.Generic;

public partial class LeaderboardInfo : ScrollContainer
{
	// Called when the node enters the scene tree for the first time.
	private VBoxContainer Leaderboards { get; set; }
	private Label NoBeatmap { get; set; }
	public List<LeaderboardEntry> oldLeaderboardList { get; set; } = new List<LeaderboardEntry>();

	public override void _Ready()
	{
		Leaderboards = GetNode<VBoxContainer>("Leaderboard");
		NoBeatmap = GetNode<Label>("NoBeatmap");
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
				leaderboardEntry.SetMeta("username", entry.username);
				leaderboardEntry.SetMeta("points", entry.points);
				leaderboardEntry.SetMeta("score", entry.score);
				leaderboardEntry.SetMeta("combo", entry.combo);
				leaderboardEntry.SetMeta("max", entry.MAX);
				leaderboardEntry.SetMeta("good", entry.GOOD);
				leaderboardEntry.SetMeta("meh", entry.MEH);
				leaderboardEntry.SetMeta("bad", entry.BAD);
				leaderboardEntry.SetMeta("mods", entry.mods);
				leaderboardEntry.SetMeta("time", entry.time);
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
