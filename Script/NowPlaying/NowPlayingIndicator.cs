using Godot;
using System;

public partial class NowPlayingIndicator : RichTextLabel
{
	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		var Title = SettingsOperator.SessionConfig.BeatmapTitle?.ToString() ?? "No song selected";
		var Artist = SettingsOperator.SessionConfig.BeatmapArtist?.ToString() ?? "";
		Text = $"{Artist} - {Title}";
	}
}
