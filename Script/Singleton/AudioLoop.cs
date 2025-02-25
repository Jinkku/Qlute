using Godot;
using System;

public partial class AudioLoop : Node
{
	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		if (SettingsOperator.Gameplaycfg["timetotal"]-SettingsOperator.Gameplaycfg["time"] < -1000 && SettingsOperator.loopaudio)
		{
			AudioPlayer.Instance.Play();
		}
	}
}
