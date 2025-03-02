using Godot;
using System;

public partial class Bpmtick : Timer
{
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{_Process(0);}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{WaitTime = 60000 / ((int)SettingsOperator.Sessioncfg["beatmapbpm"]*AudioPlayer.Instance.PitchScale) * 0.001;}
}
