using Godot;
using System;
using System.Text.Json;
using System.Collections.Generic;

public partial class LeaderboardTest : ColorRect
{
	// Called when the node enters the scene tree for the first time.
	private void _reset()
	{
		_Ready();
		GetNode<PanelContainer>("Leaderboard").QueueFree();
		var x = GD.Load<PackedScene>("res://Panels/GameplayElements/Customizable/LeaderboardPanel.tscn").Instantiate().GetNode<PanelContainer>(".");
		x.Name = "Leaderboard";
		x.Position = new Vector2(373, 54);
		AddChild(x);
	}
	public override void _Ready()
	{
		ApiOperator.LeaderboardList = new List<LeaderboardEntry>([
			new LeaderboardEntry
			{
				username = "Player1",
				points = 1000,
				score = 720837,
				combo = 50,
				MAX = 10,
				GOOD = 5,
				MEH = 3,
				BAD = 2,
				mods = "HDHR",
				time = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
			},
			new LeaderboardEntry
			{
				username = "Player2",
				points = 800,
				score = 750234,
				combo = 40,
				MAX = 8,
				GOOD = 4,
				MEH = 2,
				BAD = 1,
				mods = "HD",
				time = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
			},
			new LeaderboardEntry
			{
				username = "Player3",
				points = 800,
				score = 650234,
				combo = 40,
				MAX = 8,
				GOOD = 4,
				MEH = 2,
				BAD = 1,
				mods = "HD",
				time = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
			},
			new LeaderboardEntry
			{
				username = "Player4",
				points = 800,
				score = 420483,
				combo = 40,
				MAX = 8,
				GOOD = 4,
				MEH = 2,
				BAD = 1,
				mods = "HD",
				time = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
			}
		]);
	}
	private void _changed(float value)
	{
		// This method is called when the value of the slider changes.
		// You can implement functionality here to handle the change.
		SettingsOperator.Gameplaycfg["score"] = value;
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}
}
