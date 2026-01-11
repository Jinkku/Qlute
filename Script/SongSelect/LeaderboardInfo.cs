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
				entry.Accuracy = Gameplay.ReloadAccuracy(entry.MAX, entry.GOOD,entry.MEH, entry.BAD);
				var leaderboardEntry = GD.Load<PackedScene>("res://Panels/SongSelectButtons/Leaderboard.tscn").Instantiate().GetNode<Leaderboard>(".");
				leaderboardEntry.Info = entry;
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
