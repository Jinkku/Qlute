using Godot;
using System;

public partial class SongProgress : ProgressBar
{
	private double timeTotal {get;set;}
	private double timeCurrent { get; set; }
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		timeTotal = 1; // Avoid division by zero
		timeCurrent = 0;
		_Process(0);
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		timeTotal = SettingsOperator.Gameplaycfg.TimeTotal;
		MaxValue = timeTotal;
		timeCurrent = SettingsOperator.Gameplaycfg.Time;
		Value = timeCurrent;
	}
}
