using Godot;
using System;

public partial class SongSelectMode : Label
{
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		if (SettingsOperator.CreateSelectingBeatmap || SettingsOperator.MultiSelectingBeatmap)
		{
			Text = "Select a beatmap to use";
		}
		else
		{
			Text = "";
		}
	}
}
