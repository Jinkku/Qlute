using Godot;
using System;

public partial class TimeLeft : Label
{
	private long _lastSecond = long.MinValue;

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		var etho = (SettingsOperator.Gameplaycfg.TimeTotalGame / 0.001f) - (SettingsOperator.Gameplaycfg.Time / 0.001f);
		if (etho < 0)
		{
			etho = 0;
		}
		long second = (long)Math.Floor(etho / 1000.0);
		if (second == _lastSecond) return; // only changes once a second
		_lastSecond = second;
		Text = "-" + TimeSpan.FromMilliseconds(etho).ToString(@"mm\:ss");
	}
}
