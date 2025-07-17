using Godot;
using System;

public partial class SongIndication : Label
{
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		ReloadSongText();
	}

	private void ReloadSongText()
	{
		var Title = SettingsOperator.Sessioncfg["beatmaptitle"]?.ToString() ?? "No song selected";
		var Artist = SettingsOperator.Sessioncfg["beatmapartist"]?.ToString() ?? "";
		if (Artist == "")
		{
			Text = Title;
		} else {
			Text = $"{Artist} - {Title}";
		}
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		ReloadSongText();
	}
}
