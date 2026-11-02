using Godot;
using System;

public partial class CurrentTime : Label
{
	private long _lastSecond = long.MinValue;

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		double ms = SettingsOperator.Gameplaycfg.Time / 0.001f;
		long second = (long)Math.Floor(ms / 1000.0);
		if (second == _lastSecond) return; // only changes once a second
		_lastSecond = second;
		Text = TimeSpan.FromMilliseconds(ms).ToString(@"mm\:ss");
	}
}
