using Godot;
using System;

public partial class NowPlayingIndicator : RichTextLabel
{
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		SongName();
	}

	private string SongName()
	{
		var Title = SettingsOperator.Sessioncfg["beatmaptitle"]?.ToString() ?? "No song selected";
		var Artist = SettingsOperator.Sessioncfg["beatmapartist"]?.ToString() ?? "";
		return $"{Artist} - {Title}";
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		SongName();
	}
}
