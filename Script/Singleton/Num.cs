using System.Globalization;
using System.Runtime.CompilerServices;

/// <summary>
/// Culture-independent numbers. Replays, beatmaps, settings and request headers are shared between PCs, so a
/// German PC must write "1.5", not "1,5". The whole game runs with the invariant culture (set when the
/// assembly loads, before any node), and file/header parsing goes through these helpers.
/// </summary>
public static class Num
{
	public static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

	[ModuleInitializer]
	internal static void UseInvariantCulture()
	{
		CultureInfo.DefaultThreadCurrentCulture = Inv;
		CultureInfo.DefaultThreadCurrentUICulture = Inv;
		CultureInfo.CurrentCulture = Inv;
		CultureInfo.CurrentUICulture = Inv;
	}

	/// <summary>Invariant parse ("1.5"). With <paramref name="commaFallback"/>, "1,5" (written by older builds on
	/// comma-decimal PCs) also reads as 1.5 instead of failing.</summary>
	public static bool TryDouble(string s, out double value, bool commaFallback = false)
	{
		s = s?.Trim();
		if (double.TryParse(s, NumberStyles.Float, Inv, out value)) return true;
		return commaFallback && s != null && s.Contains(',') && !s.Contains('.')
			&& double.TryParse(s.Replace(',', '.'), NumberStyles.Float, Inv, out value);
	}

	public static double Double(string s, double fallback = 0, bool commaFallback = false) =>
		TryDouble(s, out var v, commaFallback) ? v : fallback;

	public static float Float(string s, float fallback = 0, bool commaFallback = false) =>
		TryDouble(s, out var v, commaFallback) ? (float)v : fallback;

	/// <summary>Invariant text for a number written to a file or header.</summary>
	public static string Str(double v) => v.ToString(Inv);
	public static string Str(float v) => v.ToString(Inv);
}
