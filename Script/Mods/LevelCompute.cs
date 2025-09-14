using Godot;
using System;

public partial class LevelCompute : Label
{
	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		if ((int)SettingsOperator.Sessioncfg["SongID"] != -1) Text = $"Lv. {(SettingsOperator.LevelRating * ModsMulti.multiplier).ToString("N0")}";
	}
}
