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
	public bool Active { get; set; }
}
public partial class LeaderboardPanel : PanelContainer
{
	// Called when the node enters the scene tree for the first time.
	private Control LeaderboardContainer;
	public List<LeaderboardStatus> LeaderboardEntries = new List<LeaderboardStatus>();
	private int Count = 0;
	private int LeadSize = 0;
	private ScrollContainer Scroll { get; set; }
	private PanelContainer PlayerNode { get; set; }
	private int PlayerLeaderboardEntry { get; set; }
	private string User { get; set; }
	public override void _Ready()
	{
		LeaderboardContainer = GetNode<Control>("S/ControlLead");
		Scroll = GetNode<ScrollContainer>("S");
		var ranknum = 1;
		var id = 0;
		foreach (var entry in ApiOperator.LeaderboardList)
		{
			var leaderboardEntry = GD.Load<PackedScene>("res://Panels/GameplayElements/Static/Leaderboard.tscn").Instantiate().GetNode<PanelContainer>(".");
			Size = new Vector2(leaderboardEntry.Size.X, Size.Y);
			leaderboardEntry.SetMeta("username", entry.username);
			leaderboardEntry.SetMeta("id", id);
			leaderboardEntry.SetMeta("rank", ranknum);
			leaderboardEntry.SetMeta("playing", false);
			leaderboardEntry.SelfModulate = new Color(0.56f, 0.56f, 0.56f); // Default color for other ranks
			LeaderboardContainer.AddChild(leaderboardEntry);
			leaderboardEntry.Position = new Vector2(0, ((ranknum - 1) * leaderboardEntry.Size.Y) + (ranknum * 5)); // Adjust position based on rank
			LeadSize += (int)leaderboardEntry.Size.Y + 5;
			leaderboardEntry.ProcessMode = ProcessModeEnum.Pausable;
			leaderboardEntry.Visible = false;
			LeaderboardEntries.Add(new LeaderboardStatus
			{
					Rank = ranknum,
					Username = entry.username,
					Score = entry.score,
					Playing = false,
					Node = leaderboardEntry,
					tween = null
			});
			ranknum++;
			id++;
		}
		var playerEntry = GD.Load<PackedScene>("res://Panels/GameplayElements/Static/Leaderboard.tscn").Instantiate().GetNode<PanelContainer>(".");
		User = ApiOperator.Username;
		if (ModsOperator.Mods["auto"]) User = "Qlutina";
		playerEntry.SelfModulate = new Color(56 / 255f, 82 / 255f, 138 / 255f); // Default color for other ranks
		playerEntry.SetMeta("rank", ranknum);
		playerEntry.SetMeta("username", User);
		playerEntry.SetMeta("playing", true);
		playerEntry.Position = new Vector2(0, ((ranknum - 1) * playerEntry.Size.Y) + (ranknum * 5)); // Adjust position based on rank
		LeaderboardContainer.AddChild(playerEntry);
		LeaderboardEntries.Add(new LeaderboardStatus
		{
			Rank = ranknum,
			Username = User,
			Score = (int)SettingsOperator.Gameplaycfg.Score,
			Playing = true,
			Node = playerEntry,
			tween = null,
			Active = true
		});
		PlayerNode = playerEntry;
		PlayerLeaderboardEntry = id;
		Count = ranknum;
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	private Vector2 Convert_Position(PanelContainer Node, int ID)
	{
		float Y = -5;
		Y += ID * Node.Size.Y; // Grabs Node ID then times it to the Node's Size
		Y += (ID + 1) * 5; // Then Adds a offset of 5
		return new Vector2(0, Y); // returns value
	}
	private int oldscore { get; set; }
	public override void _Process(double delta)
	{
		if (LeaderboardEntries.Count > 1)
		{
			Scroll.ScrollVertical = (int)PlayerNode.Position.Y;
			LeaderboardContainer.CustomMinimumSize = new Vector2(0, PlayerNode.Position.Y + PlayerNode.Size.Y);


			// Sort the leaderboard by score in descending order
			LeaderboardEntries = LeaderboardEntries.OrderByDescending(entry => entry.Score).ToList();

			// Update ranks and UI
			for (int i = 0; i < LeaderboardEntries.Count; i++)
			{
				var entry = LeaderboardEntries[i];
				if (entry.Active && entry.Username == User)
				{
					entry.Score = SettingsOperator.Gameplaycfg.Score;
					PlayerLeaderboardEntry = i;
					SettingsOperator.Gameplaycfg.Rank = PlayerLeaderboardEntry;
				}
				else if (ModsOperator.Mods["npc"] && i > LeaderboardEntries.Count - 1)
				{
					entry.Score = ApiOperator.LeaderboardList[i].score;
				}
				if (entry.Rank > LeaderboardEntries[PlayerLeaderboardEntry].Rank - 6 && entry.Rank < LeaderboardEntries[PlayerLeaderboardEntry].Rank + 6 && !entry.Playing)
				{
					entry.Node.Visible = true;
					entry.Node.ProcessMode = ProcessModeEnum.Pausable;
				}
				else if (!entry.Playing)
				{
					entry.Node.Visible = false;
					entry.Node.ProcessMode = ProcessModeEnum.Disabled;
				}
				var oldrank = entry.Rank;
				entry.Rank = i + 1;
				entry.Node.SetMeta("rank", entry.Rank);
				if (entry.Rank != oldrank)
				{
					entry.tween?.Kill();
					entry.tween = entry.Node.CreateTween();
					entry.tween.Parallel().TweenProperty(entry.Node, "modulate", new Color(2f, 2f, 2f, 0.5f), 0.3).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
					entry.tween.Parallel().TweenProperty(entry.Node, "position", Convert_Position(entry.Node, i), 0.5).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic); // Adjust position based on new rank
					entry.tween.TweenProperty(entry.Node, "modulate", new Color(1f, 1f, 1f, 1f), 0.5).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
				}
			}
		}
	}
}
