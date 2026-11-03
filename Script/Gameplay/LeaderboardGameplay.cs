using Godot;
using System;

public partial class LeaderboardGameplay : PanelContainer
{
	// Called when the node enters the scene tree for the first time.
	private Label Username { get; set; }
	private Label Score { get; set; }
	private Label Combo { get; set; }
	private Label Accuracy { get; set; }
	private Label Mods { get; set; }
	private Label pp { get; set; }
	private Label Time { get; set; }
	private Label Rank { get; set; }
	private TextureRect Picture { get; set; }
	private Texture2D GuestPicture { get; set; }
	public override void _Ready()
	{
		Picture = GetNode<TextureRect>("LeaderboardColumns/Picture");
		GuestPicture = GD.Load<CompressedTexture2D>("res://Resources/System/guest.png");
		Rank = GetNode<Label>("LeaderboardColumns/Rank");
		Username = GetNode<Label>("LeaderboardColumns/Col1/Username");
		Score = GetNode<Label>("LeaderboardColumns/Col1/Score");
		Combo = GetNode<Label>("LeaderboardColumns/Col2/Combo");
		Accuracy = GetNode<Label>("LeaderboardColumns/Col2/Accuracy");
		if (!HasMeta("username"))
		{
			Username.Text = "Unknown";
		}
		else
		{
			Username.Text = GetMeta("username").ToString();
		}

		if (HasMeta("playing") && (bool)GetMeta("playing") && HasMeta("username") &&
		    GetMeta("username").ToString() == ApiOperator.Username)
		{
			Picture.Texture = ApiOperator.PictureData;
		}
		refresh_info();
	}

	// Refreshes the leaderboard information.

	private void refresh_info()
	{
		if (HasMeta("id"))
		{
			info ??= ApiOperator.LeaderboardList[(int)GetMeta("id")];
			Score.Text = string.Format("{0:N0}", info.score);
			Combo.Text = $"{info.combo}x";
			Accuracy.Text = info.Accuracy.ToString("P2");
		}
		else
		{
			Score.Text = "0";
			Combo.Text = "0x";
			Accuracy.Text = "100.00%";
		}
		refresh_rank();
	}
	private string _lastRank;
	private void refresh_rank()
	{
		if (!HasMeta("rank"))
		{
			Rank.Text = "0";
			_lastRank = null;
		}
		else
		{
			var rank = GetMeta("rank").ToString();
			if (rank != _lastRank)
			{
				_lastRank = rank;
				Rank.Text = "#" + rank;
			}
		}
	}
	private LeaderboardEntry info { get; set; }
	private (int, int, float) _lastInfoStats;
	private (int Score, int Combo, float Accuracy) _lastPlaying = (int.MinValue, int.MinValue, float.NaN);
	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		if (HasMeta("playing") && (bool)GetMeta("playing"))
		{
			// Only re-format strings when the values actually change.
			var cur = (SettingsOperator.Gameplaycfg.Score, SettingsOperator.Gameplaycfg.Combo, SettingsOperator.Gameplaycfg.Accuracy);
			if (cur != _lastPlaying)
			{
				_lastPlaying = cur;
				Score.Text = cur.Score.ToString("N0");
				Combo.Text = cur.Combo + "x";
				Accuracy.Text = cur.Accuracy.ToString("P2");
			}
			refresh_rank();
		}
		else
		{
			var newInfo = ApiOperator.LeaderboardList[(int)GetMeta("id")];
			// LAN opponents' entries update live, so also refresh when their numbers move.
			var stats = (newInfo.score, newInfo.combo, newInfo.Accuracy);
			bool changed = !ReferenceEquals(newInfo, info) || stats != _lastInfoStats;
			_lastInfoStats = stats;
			info = newInfo;
			if (Picture.Texture != info.ProfilePicture && info.ProfilePicture != null)
			{
				Picture.Texture = info.ProfilePicture;
			}
			else if (Picture.Texture != GuestPicture && info.ProfilePicture == null)
			{
				Picture.Texture = GuestPicture;
			}
			if (changed) refresh_info();
		}
	}
}
