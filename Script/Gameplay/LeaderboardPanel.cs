using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;

public class LeaderboardStatus
{
	public int Rank { get; set; }
	public string Username { get; set; }
	public int Score { get; set; }
	public bool Playing { get; set; }
	public PanelContainer Node { get; set; }
	public Tween tween { get; set; }
}
public partial class LeaderboardPanel : PanelContainer
{
	// Called when the node enters the scene tree for the first time.
	private Control LeaderboardContainer;
	public List<LeaderboardStatus> LeaderboardEntries = new List<LeaderboardStatus>();
	private int Count = 0;
	public override void _Ready()
	{
		LeaderboardContainer = GetNode<Control>("ControlLead");
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
			leaderboardEntry.Position = new Vector2(0, (ranknum - 1) * leaderboardEntry.Size.Y + (ranknum * 5)); // Adjust position based on rank
			
			LeaderboardEntries.Add(new LeaderboardStatus
			{
				Rank = ranknum,
				Username = entry.username,
				Score = entry.score,
				Playing = false,
				Node = leaderboardEntry,
				tween = leaderboardEntry.CreateTween()
			});
			ranknum++;
		}
		var playerEntry = GD.Load<PackedScene>("res://Panels/GameplayElements/Static/Leaderboard.tscn").Instantiate().GetNode<PanelContainer>(".");
		playerEntry.SelfModulate = new Color(56 / 255f, 82 / 255f, 138 / 255f); // Default color for other ranks
		playerEntry.SetMeta("rank", ranknum);
		playerEntry.SetMeta("username", ApiOperator.Username);
		playerEntry.SetMeta("playing", true);
		playerEntry.Position = new Vector2(0, (ranknum - 1) * playerEntry.Size.Y + (ranknum * 5)); // Adjust position based on rank
		LeaderboardContainer.AddChild(playerEntry);
		LeaderboardEntries.Add(new LeaderboardStatus
		{
			Rank = ranknum,
			Username = ApiOperator.Username,
			Score = (int)SettingsOperator.Gameplaycfg["score"],
			Playing = true,
			Node = playerEntry,
			tween = playerEntry.CreateTween()
		});
		Count = ranknum;
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		var oldscore = (int)SettingsOperator.Gameplaycfg["score"];
		if (LeaderboardEntries.Count > 1)
		{
			// Sort the leaderboard by score in descending order
			LeaderboardEntries.Sort((a, b) => b.Score.CompareTo(a.Score));

			// Update ranks and UI
			for (int i = 0; i < LeaderboardEntries.Count; i++)
			{
				var entry = LeaderboardEntries[i];
				if (entry.Username == ApiOperator.Username) entry.Score = (int)SettingsOperator.Gameplaycfg["score"];
				var oldrank = entry.Rank;
				entry.Rank = i + 1;
				entry.Node.SetMeta("rank", entry.Rank);
				if (entry.Rank != oldrank)
				{
					entry.tween?.Kill();
					entry.tween = entry.Node.CreateTween();
					entry.tween.TweenProperty(entry.Node, "position", new Vector2(0, i * entry.Node.Size.Y + ((i + 1) * 5)), 0.2).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic); // Adjust position based on new rank
				}
			}
		}
	}
}
