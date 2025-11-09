using Godot;
using System;

public partial class RatingCard : Label
{
	private int SongID { get; set; }
    public override void _Ready()
    {
		SongID = (int)GetNode<MusicCard>("../../../../../").SongID;
    }
	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		Text = "Lv. " + (SettingsOperator.Beatmaps[SongID].Levelrating * ModsMulti.multiplier).ToString("N0");
	}
}
