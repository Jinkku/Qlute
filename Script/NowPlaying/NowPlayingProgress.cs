using Godot;
using System;

public partial class NowPlayingProgress : HSlider
{
	private double timeTotal {get;set;}
	private double timeCurrent { get; set; }
	private float ProgressChange { get; set; }
	private bool Changing { get; set; }

	private void Changed(float value)
	{
		ProgressChange = value;
		Changing = true;
	}
	
	private void Changeto(bool changed) 
	{
		if (changed && !SettingsOperator.inGameplay)
		{
			Changing = false;
			AudioPlayer.Instance.Seek(ProgressChange);
		}
	}
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
		if (!Changing)
		{
			timeCurrent = SettingsOperator.Gameplaycfg.Time;
			Value = timeCurrent;
		}
	}
}
