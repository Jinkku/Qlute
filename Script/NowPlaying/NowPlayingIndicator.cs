using Godot;
using System;

public partial class NowPlayingIndicator : RichTextLabel
{

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		var Title = SettingsOperator.Sessioncfg["beatmaptitle"]?.ToString() ?? "No song selected";
		var Artist = SettingsOperator.Sessioncfg["beatmapartist"]?.ToString() ?? "";
		Text = $"{Artist} - {Title}";
	}
}
