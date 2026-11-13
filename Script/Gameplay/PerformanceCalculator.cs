using System;
using System.Collections.Generic;
namespace Game;

/// <summary>
/// pp v3 + competitive score. Exact mirror of the server's Chikara/Chikara/ppv2calc.py
/// (same constants, same operation order, doubles everywhere). The server re-plays every
/// replay and is authoritative; these numbers are the in-game preview. Change both together.
/// Design notes: Chikara/Chikara/PP_SCORE_DESIGN.md
/// </summary>
public static class PerformanceCalculator
{
	public const int PpVersion = 3;

	// Judgement weights in sixths (1, 1/2, 1/3, 0) so accuracy and score stay integer.
	public const int WPerfect = 6, WGreat = 3, WMeh = 2;

	// Score: 900k accuracy + 100k combo on NoMod, times the score multiplier.
	public const long ScoreAccPortion = 900_000;
	public const long ScoreComboPortion = 100_000;
	public const int ComboCap = 400;

	// Difficulty (strain model)
	const double SectionMs = 400.0;
	const double IndividualDecay = 0.125;
	const double OverallDecay = 0.30;
	const double IndividualAdd = 2.0;
	const double OverallAdd = 1.0;
	const double PeakWeight = 0.90;
	const double StarScale = 0.028;

	// Performance
	const double PpScale = 1.13; // calibrated: Hommarju - Slluuddggee [Slliimmee] SS = 124pp (its osu!mania value)
	const double PpStarPower = 2.8;
	const double LengthBonusMax = 0.15;
	const double LengthBonusNotes = 2000;
	const double AccFloor = 0.70;
	const double AccPower = 1.5;
	const double MissBase = 0.98;
	const double MissNormalNotes = 1000;

	// Applied in this order (NF, SL, BT, AM). Rate mods change the star rating instead of pp.
	static readonly string[] ModOrder = { "NF", "SL", "BT", "AM" };
	static readonly double[] PpModFactor = { 0.90, 1.04, 1.08, 0.50 };
	static readonly double[] ScoreModFactor = { 0.50, 1.05, 1.15, 0.50 };

	// ------------------------------------------------------------------ mods

	/// <summary>Playback rate, rounded to 0.01 so the client float and the server's text agree.</summary>
	public static double GetRate(string mods, double speed)
	{
		mods ??= "";
		if (!mods.Contains("DT") && !mods.Contains("HT")) return 1.0;
		if (double.IsNaN(speed) || double.IsInfinity(speed) || speed <= 0) return 1.0;
		return Math.Floor(speed * 100.0 + 0.5) / 100.0;
	}

	/// <summary>Continuous: 1.25x -> 1.15, 1.5x -> 1.30, 0.75x -> 0.65, 0.5x -> 0.30.</summary>
	public static double RateScoreFactor(double rate)
	{
		if (rate > 1.0) return 1.0 + 0.6 * (rate - 1.0);
		if (rate < 1.0) return Math.Max(0.1, 1.0 - 1.4 * (1.0 - rate));
		return 1.0;
	}

	/// <summary>Score multiplier for a mod alias string (e.g. "NFDT") at a playback speed.</summary>
	public static double ScoreMultiplier(string mods, double speed)
	{
		mods ??= "";
		double m = RateScoreFactor(GetRate(mods, speed));
		for (int i = 0; i < ModOrder.Length; i++)
			if (mods.Contains(ModOrder[i])) m *= ScoreModFactor[i];
		return m;
	}

	public static double PpMultiplier(string mods)
	{
		mods ??= "";
		double m = 1.0;
		for (int i = 0; i < ModOrder.Length; i++)
			if (mods.Contains(ModOrder[i])) m *= PpModFactor[i];
		return m;
	}

	// ------------------------------------------------------------------ difficulty

	/// <summary>
	/// Star rating of the map's notes (real columns, not Random-mod ones), times in ms.
	/// Strains decay by the real (rate-adjusted) time between notes; every note adds to its
	/// column (jacks) and to the overall strain (density, chords). The peak of every 400 ms
	/// section is kept and the sorted peaks are summed with weight 0.9^k.
	/// </summary>
	public static double StarRating(List<(int time, int col)> notes, double rate)
	{
		if (notes == null || notes.Count == 0) return 0.0;
		var objs = new List<(int time, int col)>(notes);
		objs.Sort();
		var individual = new double[4];
		double overall = 0.0;
		var peaks = new List<double>();
		double prevT = objs[0].time / rate;
		double sectionEnd = prevT + SectionMs;
		double peak = 0.0;
		foreach (var (timeRaw, col) in objs)
		{
			double t = timeRaw / rate;
			while (t > sectionEnd)
			{
				peaks.Add(peak);
				peak = 0.0;
				sectionEnd += SectionMs;
			}
			double dt = (t - prevT) / 1000.0;
			if (dt > 0)
			{
				double indDecay = Math.Pow(IndividualDecay, dt);
				for (int c = 0; c < 4; c++) individual[c] *= indDecay;
				overall *= Math.Pow(OverallDecay, dt);
			}
			individual[col] += IndividualAdd;
			overall += OverallAdd;
			double strain = individual[col] + overall;
			if (strain > peak) peak = strain;
			prevT = t;
		}
		peaks.Add(peak);
		peaks.Sort((a, b) => b.CompareTo(a));
		double total = 0.0, weight = 1.0;
		foreach (var p in peaks)
		{
			total += p * weight;
			weight *= PeakWeight;
		}
		return total * StarScale;
	}

	// ------------------------------------------------------------------ performance

	/// <summary>pp of an SS.</summary>
	public static double MaxPerformance(double stars, int noteCount, string mods)
	{
		if (noteCount <= 0) return 0.0;
		double lengthBonus = 1.0 + LengthBonusMax * Math.Min(1.0, noteCount / LengthBonusNotes);
		return PpScale * Math.Pow(Math.Max(stars - 0.15, 0.05), PpStarPower) * lengthBonus * PpMultiplier(mods);
	}

	/// <summary>pp of a play. Notes that were never judged count as misses.</summary>
	public static double Performance(double maxPp, int noteCount, int perfect, int great, int meh, int bad)
	{
		if (noteCount <= 0) return 0.0;
		bad = Math.Max(bad, noteCount - perfect - great - meh);
		double acc = (double)(WPerfect * perfect + WGreat * great + WMeh * meh) / (WPerfect * noteCount);
		double accFactor = Math.Pow(Math.Max(0.0, (acc - AccFloor) / (1.0 - AccFloor)), AccPower);
		double effMisses = bad * Math.Min(1.0, MissNormalNotes / noteCount);
		double missFactor = Math.Pow(MissBase, effMisses);
		return maxPp * accFactor * missFactor;
	}

	/// <summary>
	/// Live pp for the HUD: the pp the current accuracy/misses would be worth on the whole map,
	/// scaled by how much of the map has been judged, so it climbs to the final value.
	/// </summary>
	public static double LivePerformance(double maxPp, int noteCount, int perfect, int great, int meh, int bad)
	{
		int judged = perfect + great + meh + bad;
		if (noteCount <= 0 || judged <= 0) return 0.0;
		if (judged >= noteCount) return Performance(maxPp, noteCount, perfect, great, meh, bad);
		double scale = (double)noteCount / judged;
		double projected = Performance(maxPp, noteCount,
			(int)Math.Round(perfect * scale), (int)Math.Round(great * scale), (int)Math.Round(meh * scale), (int)Math.Round(bad * scale));
		return projected * judged / noteCount;
	}

	// ------------------------------------------------------------------ level

	const double LevelMinPp = 5.0;     // Lv 1 = 0..5 pp
	const double LevelMaxPp = 1500.0;  // Lv 100 = 1500 pp and up
	const double LevelCurve = 0.8;     // < 1: levels rise faster at low pp (Lv 7 = 50-60pp)

	/// <summary>
	/// Map level 1..100 from NoMod SS pp: Lv 1 &lt;= 5pp, Lv 100 &gt;= 1500pp,
	/// 1 + 99 * ((pp - 5) / 1495)^0.8 in between (floored, 2..99). Same as ppv2calc.level_from_pp.
	/// </summary>
	public static int LevelFromPp(double pp)
	{
		if (!(pp > LevelMinPp)) return 1;
		if (pp >= LevelMaxPp) return 100;
		double x = (pp - LevelMinPp) / (LevelMaxPp - LevelMinPp);
		return Math.Clamp((int)Math.Floor(1.0 + 99.0 * Math.Pow(x, LevelCurve)), 2, 99);
	}

	// ------------------------------------------------------------------ score

	/// <summary>Combo weight a hit adds: judgement weight * min(combo after the hit, ComboCap).</summary>
	public static long ComboWeight(int judgementWeight, int comboAfterHit) =>
		(long)judgementWeight * Math.Min(comboAfterHit, ComboCap);

	/// <summary>WPerfect * sum(min(i, ComboCap) for i in 1..noteCount).</summary>
	public static long ComboPortionMax(int noteCount)
	{
		long n = noteCount, s;
		if (n <= ComboCap) s = n * (n + 1) / 2;
		else s = (long)ComboCap * (ComboCap + 1) / 2 + (n - ComboCap) * ComboCap;
		return WPerfect * s;
	}

	/// <summary>
	/// Live score for the HUD: the score the play so far is on track for (accuracy and combo
	/// measured over the notes judged so far). Starts at the SS value on the first perfect, drops on
	/// every miss/great/meh, and equals <see cref="ComputeScore"/> once every note is judged.
	/// </summary>
	public static int LiveScore(int perfect, int great, int meh, int bad, long comboPortion, double multiplier)
	{
		int judged = perfect + great + meh + bad;
		return judged <= 0 ? 0 : ComputeScore(judged, perfect, great, meh, comboPortion, multiplier);
	}

	/// <summary>0..1,000,000 on NoMod (SS = exactly 1,000,000), times the score multiplier.</summary>
	public static int ComputeScore(int noteCount, int perfect, int great, int meh, long comboPortion, double multiplier)
	{
		if (noteCount <= 0) return 0;
		long accNum = WPerfect * (long)perfect + WGreat * (long)great + WMeh * (long)meh;
		long accDen = WPerfect * (long)noteCount;
		long comboDen = ComboPortionMax(noteCount);
		// Integers below 2^53: each division is one correctly rounded double op, same as Python.
		double baseScore = (double)(ScoreAccPortion * accNum) / accDen + (double)(ScoreComboPortion * comboPortion) / comboDen;
		return (int)Math.Floor(baseScore * multiplier + 0.5);
	}
}
