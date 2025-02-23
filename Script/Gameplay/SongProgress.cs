using Godot;
using System;

public partial class SongProgress : ProgressBar
{
	// Called when the node enters the scene tree for the first time.
	public static SettingsOperator SettingsOperator { get; set; }
	public override void _Ready()
	{
		SettingsOperator = GetNode<SettingsOperator>("/root/SettingsOperator");
		float timeTotal = 1; // Avoid division by zero

		if (SettingsOperator.Gameplaycfg.TryGetValue("timetotal", out var total))
		{
			timeTotal = Convert.ToSingle(total);
		}
		MaxValue = timeTotal;
		_Process(0);
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		float timeCurrent = 0;
		

		if (SettingsOperator.Gameplaycfg.TryGetValue("time", out var current))
		{
			timeCurrent = Convert.ToSingle(current);
		}
		Value = timeCurrent;
	}
}
