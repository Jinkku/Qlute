using Godot;
using System;

public partial class LeaderboardPanel : PanelContainer
{
	// Called when the node enters the scene tree for the first time.
	private VBoxContainer LeaderboardContainer;
	public override void _Ready()
	{
		LeaderboardContainer = GetNode<VBoxContainer>("LeaderboardScroll/Legend");
		var ranknum = 1;
		foreach (var entry in ApiOperator.LeaderboardList)
		{
			var leaderboardEntry = GD.Load<PackedScene>("res://Panels/GameplayElements/Static/Leaderboard.tscn").Instantiate().GetNode<PanelContainer>(".");
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
			leaderboardEntry.SetMeta("playing", false);
			leaderboardEntry.SelfModulate = new Color(0.56f, 0.56f, 0.56f); // Default color for other ranks
			LeaderboardContainer.AddChild(leaderboardEntry);
			ranknum++;
		}
		var playerEntry = GD.Load<PackedScene>("res://Panels/GameplayElements/Static/Leaderboard.tscn").Instantiate().GetNode<PanelContainer>(".");
		playerEntry.SelfModulate = new Color(56 / 255f, 82 / 255f, 138 / 255f); // Default color for other ranks
		playerEntry.SetMeta("rank", ranknum);
		playerEntry.SetMeta("username", ApiOperator.Username);
		playerEntry.SetMeta("playing", true);
		LeaderboardContainer.AddChild(playerEntry);
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}
}
