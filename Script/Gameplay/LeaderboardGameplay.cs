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
	private void refresh_rank()
	{
		if (!HasMeta("rank"))
		{
			Rank.Text = "0";
		}
		else
		{
			Rank.Text = "#" + GetMeta("rank").ToString();
		}
	}
	private LeaderboardEntry info { get; set; }
	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		if (HasMeta("playing") && (bool)GetMeta("playing"))
		{
			Score.Text = SettingsOperator.Gameplaycfg.Score.ToString("N0");
			Combo.Text = SettingsOperator.Gameplaycfg.Combo + "x";
			Accuracy.Text = SettingsOperator.Gameplaycfg.Accuracy.ToString("P2");
			refresh_rank();
		}
		else
		{
			info = ApiOperator.LeaderboardList[(int)GetMeta("id")];
			if (Picture.Texture != info.ProfilePicture && info.ProfilePicture != null)
			{
				Picture.Texture = info.ProfilePicture;
			}
			else if (Picture.Texture != GuestPicture && info.ProfilePicture == null)
			{
				Picture.Texture = GuestPicture;
			}
			refresh_info();
		}
	}
}
