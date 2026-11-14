using System;

/// <summary>
/// Unstable Rate (std-dev of hit offsets * 10) kept as a running mean/variance (Welford's method),
/// so adding a hit is O(1) with no allocations or LINQ during gameplay.
/// </summary>
public struct RunningUnstableRate
{
	private int _count;
	private double _mean;
	private double _m2;

	public int Count => _count;

	public void Reset()
	{
		_count = 0;
		_mean = 0;
		_m2 = 0;
	}

	public void Add(double offsetMs)
	{
		_count++;
		double delta = offsetMs - _mean;
		_mean += delta / _count;
		_m2 += delta * (offsetMs - _mean);
	}

	/// <summary>Population std-dev * 10; 0 until there are two hits.</summary>
	public float Value => _count < 2 ? 0f : (float)(Math.Sqrt(_m2 / _count) * 10.0);
}
