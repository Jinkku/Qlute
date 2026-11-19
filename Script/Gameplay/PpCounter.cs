using Godot;
using System;

/// <summary>
/// Live pp in the HUD (PPCounter component and the score overlay). The number glides to the real value instead of
/// jumping, both up (hits) and down (misses, a replay seek), quickly for small changes and big ones alike.
/// </summary>
public partial class PpCounter : Label
{
	/// <summary>How fast it catches up: higher = snappier (about 1 / this seconds to get most of the way).</summary>
	[Export] public float Speed { get; set; } = 8;

	private double _shown = double.NaN;
	private long _lastText = long.MinValue;

	public override void _Process(double delta)
	{
		double target = SettingsOperator.Gameplaycfg.pp;
		if (double.IsNaN(_shown) || double.IsNaN(target) || double.IsInfinity(target)) _shown = target; // first frame: no glide from 0
		else
		{
			_shown += (target - _shown) * (1 - Math.Exp(-delta * Speed));
			if (Math.Abs(target - _shown) < 0.005) _shown = target;
		}
		long rounded = double.IsFinite(_shown) ? (long)Math.Round(_shown) : 0;
		if (rounded == _lastText) return;
		_lastText = rounded;
		Text = rounded.ToString("N0") + "pp";
	}
}
