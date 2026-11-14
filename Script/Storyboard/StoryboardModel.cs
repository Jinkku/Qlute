using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace QluteStoryboard;

public enum SbLayer { Background = 0, Fail = 1, Pass = 2, Foreground = 3, Overlay = 4 }

public enum SbOrigin { TopLeft = 0, Centre = 1, CentreLeft = 2, TopRight = 3, BottomCentre = 4, TopCentre = 5, Custom = 6, CentreRight = 7, BottomLeft = 8, BottomRight = 9 }

public enum SbLoopType { LoopForever = 0, LoopOnce = 1 }

/// <summary>Storyboard command kinds, named like the .osu event letters.</summary>
public enum SbCommandType { F, S, V, R, M, MX, MY, C, P }

/// <summary>
/// One storyboard command segment: <c>_TYPE,easing,start,end,values</c>. Shorthand lines with several
/// value sets are split into one segment per step when parsed.
/// </summary>
public class SbCommand
{
	public SbCommandType Type;
	public int Easing;
	public int StartTime;
	public int EndTime;
	/// <summary>Start values (1-3 numbers; for P the parameter letter is in <see cref="Param"/>).</summary>
	public float[] From = new float[3];
	public float[] To = new float[3];
	/// <summary>P only: H (flip horizontal), V (flip vertical) or A (additive blending).</summary>
	public char Param = 'H';

	public SbCommand Clone() => new()
	{
		Type = Type, Easing = Easing, StartTime = StartTime, EndTime = EndTime,
		From = (float[])From.Clone(), To = (float[])To.Clone(), Param = Param,
	};

	public static int Arity(SbCommandType t) => t switch
	{
		SbCommandType.V or SbCommandType.M => 2,
		SbCommandType.C => 3,
		SbCommandType.P => 0,
		_ => 1,
	};
}

/// <summary>A loop (<c>L,start,count</c>): its commands repeat back to back, times relative to the loop start.</summary>
public class SbLoop
{
	public int StartTime;
	public int Count = 1;
	public List<SbCommand> Commands = new();

	public SbLoop Clone() => new() { StartTime = StartTime, Count = Count, Commands = Commands.Select(c => c.Clone()).ToList() };
}

/// <summary>A sprite or animation with its commands.</summary>
public class SbObject
{
	public bool IsAnimation;
	public SbLayer Layer = SbLayer.Background;
	public SbOrigin Origin = SbOrigin.Centre;
	/// <summary>Image path relative to the set folder, forward slashes.</summary>
	public string Path = "";
	public float X = 320, Y = 240;
	public int FrameCount = 1;
	public float FrameDelay = 100;
	public SbLoopType LoopType = SbLoopType.LoopForever;
	public List<SbCommand> Commands = new();
	public List<SbLoop> Loops = new();
	/// <summary>Trigger groups (T,...) are kept verbatim (and not played): hitsound triggers don't map to mania.</summary>
	public List<string> RawTriggerLines = new();
	/// <summary>True for objects that come from the .osb file (shared by every difficulty, read-only in the editor).</summary>
	public bool FromOsb;

	public SbObject Clone() => new()
	{
		IsAnimation = IsAnimation, Layer = Layer, Origin = Origin, Path = Path, X = X, Y = Y,
		FrameCount = FrameCount, FrameDelay = FrameDelay, LoopType = LoopType,
		Commands = Commands.Select(c => c.Clone()).ToList(),
		Loops = Loops.Select(l => l.Clone()).ToList(),
		RawTriggerLines = RawTriggerLines.ToList(),
		FromOsb = FromOsb,
	};

	// ---------- compiled timelines (rebuilt by Compile) ----------
	public SbTimeline Alpha, Scale, VScale, Rotation, Move, MoveX, MoveY, Colour;
	public SbFlagTimeline FlipH, FlipV, Additive;
	public double StartTime, EndTime;
	public bool HasCommands;

	/// <summary>Expand loops and build the per-property timelines used for playback.</summary>
	public void Compile()
	{
		var all = new List<SbCommand>(Commands);
		foreach (var loop in Loops)
		{
			if (loop.Commands.Count == 0) continue;
			int cStart = loop.Commands.Min(c => c.StartTime);
			int cEnd = loop.Commands.Max(c => c.EndTime);
			int duration = Math.Max(0, cEnd - cStart);
			int count = Math.Clamp(loop.Count, 1, 10000);
			for (int i = 0; i < count; i++)
			{
				int shift = loop.StartTime + i * duration;
				foreach (var c in loop.Commands)
				{
					var e = c.Clone();
					e.StartTime += shift;
					e.EndTime += shift;
					all.Add(e);
				}
				if (duration == 0) break;
			}
		}

		Alpha = SbTimeline.From(all, SbCommandType.F);
		Scale = SbTimeline.From(all, SbCommandType.S);
		VScale = SbTimeline.From(all, SbCommandType.V);
		Rotation = SbTimeline.From(all, SbCommandType.R);
		Move = SbTimeline.From(all, SbCommandType.M);
		MoveX = SbTimeline.From(all, SbCommandType.MX);
		MoveY = SbTimeline.From(all, SbCommandType.MY);
		Colour = SbTimeline.From(all, SbCommandType.C);
		FlipH = SbFlagTimeline.From(all, 'H');
		FlipV = SbFlagTimeline.From(all, 'V');
		Additive = SbFlagTimeline.From(all, 'A');

		HasCommands = all.Count > 0;
		// Command times, for StableUntil: merged running spans, plus every start (instant changes included).
		var spans = all.Where(c => c.EndTime > c.StartTime).Select(c => (s: (double)c.StartTime, e: (double)c.EndTime)).OrderBy(x => x.s).ToList();
		var merged = new List<(double s, double e)>();
		foreach (var sp in spans)
		{
			if (merged.Count > 0 && sp.s <= merged[^1].e) merged[^1] = (merged[^1].s, Math.Max(merged[^1].e, sp.e));
			else merged.Add(sp);
		}
		_runStart = merged.Select(m => m.s).ToArray();
		_runEnd = merged.Select(m => m.e).ToArray();
		_starts = all.Select(c => (double)c.StartTime).Distinct().OrderBy(x => x).ToArray();
		StartTime = HasCommands ? all.Min(c => c.StartTime) : 0;
		EndTime = HasCommands ? all.Max(c => c.EndTime) : 0;
		// A sprite whose first fade starts at 0 alpha is invisible until then anyway; osu! starts the
		// lifetime at the earliest command, so do the same.
	}

	private double[] _runStart = Array.Empty<double>(), _runEnd = Array.Empty<double>(), _starts = Array.Empty<double>();

	/// <summary>
	/// Until when the object's state stays exactly what it is at <paramref name="t"/>: t itself while a command
	/// is running (or an animation plays), else the next command start. Lets the player skip idle sprites.
	/// </summary>
	public double StableUntil(double t)
	{
		if (IsAnimation && FrameCount > 1) return t;
		// running span containing t?
		int i = Array.BinarySearch(_runStart, t);
		if (i < 0) i = ~i - 1;
		if (i >= 0 && t < _runEnd[i]) return t;
		int j = Array.BinarySearch(_starts, t);
		j = j < 0 ? ~j : j + 1; // first start strictly after t
		return j < _starts.Length ? _starts[j] : double.PositiveInfinity;
	}

	/// <summary>Everything needed to draw the object at <paramref name="t"/> (ms).</summary>
	public SbState StateAt(double t)
	{
		var s = new SbState
		{
			X = X, Y = Y, ScaleX = 1, ScaleY = 1, Alpha = 1, R = 1, G = 1, B = 1,
		};
		if (Move.Has) { var v = Move.ValueAt(t); s.X = v.a; s.Y = v.b; }
		if (MoveX.Has) s.X = MoveX.ValueAt(t).a;
		if (MoveY.Has) s.Y = MoveY.ValueAt(t).a;
		if (Scale.Has) { var v = Scale.ValueAt(t).a; s.ScaleX = v; s.ScaleY = v; }
		if (VScale.Has) { var v = VScale.ValueAt(t); s.ScaleX *= v.a; s.ScaleY *= v.b; }
		if (Rotation.Has) s.Rotation = Rotation.ValueAt(t).a;
		if (Alpha.Has) s.Alpha = Alpha.ValueAt(t).a;
		if (Colour.Has) { var v = Colour.ValueAt(t); s.R = v.a / 255f; s.G = v.b / 255f; s.B = v.c / 255f; }
		s.FlipH = FlipH.At(t);
		s.FlipV = FlipV.At(t);
		s.Additive = Additive.At(t);
		return s;
	}

	/// <summary>Animation frame shown at <paramref name="t"/>.</summary>
	public int FrameAt(double t)
	{
		if (!IsAnimation || FrameCount <= 1) return 0;
		int frame = (int)Math.Floor((t - StartTime) / Math.Max(1, FrameDelay));
		if (frame < 0) frame = 0;
		return LoopType == SbLoopType.LoopOnce ? Math.Min(frame, FrameCount - 1) : frame % FrameCount;
	}

	/// <summary>File of animation frame <paramref name="frame"/> ("sb/x.png" -> "sb/x3.png").</summary>
	public string FramePath(int frame)
	{
		if (!IsAnimation) return Path;
		var ext = System.IO.Path.GetExtension(Path);
		return Path[..^ext.Length] + frame.ToString(CultureInfo.InvariantCulture) + ext;
	}

	public static (float x, float y) OriginFraction(SbOrigin o) => o switch
	{
		SbOrigin.TopLeft or SbOrigin.Custom => (0f, 0f),
		SbOrigin.TopCentre => (0.5f, 0f),
		SbOrigin.TopRight => (1f, 0f),
		SbOrigin.CentreLeft => (0f, 0.5f),
		SbOrigin.Centre => (0.5f, 0.5f),
		SbOrigin.CentreRight => (1f, 0.5f),
		SbOrigin.BottomLeft => (0f, 1f),
		SbOrigin.BottomCentre => (0.5f, 1f),
		SbOrigin.BottomRight => (1f, 1f),
		_ => (0.5f, 0.5f),
	};
}

public struct SbState
{
	public float X, Y, ScaleX, ScaleY, Rotation, Alpha, R, G, B;
	public bool FlipH, FlipV, Additive;
}

/// <summary>Keyframes of one numeric property.</summary>
public class SbTimeline
{
	private struct Seg
	{
		public double S, E;
		public float A0, A1, A2, B0, B1, B2;
		public int Ease;
	}

	private Seg[] _segs = Array.Empty<Seg>();
	public bool Has => _segs.Length > 0;

	public static SbTimeline From(List<SbCommand> all, SbCommandType type)
	{
		var tl = new SbTimeline();
		// Stable sort by start time: for equal starts the later line wins, like osu!.
		tl._segs = all.Where(c => c.Type == type).OrderBy(c => c.StartTime).Select(c => new Seg
		{
			S = c.StartTime, E = Math.Max(c.StartTime, c.EndTime),
			A0 = c.From[0], A1 = c.From[1], A2 = c.From[2],
			B0 = c.To[0], B1 = c.To[1], B2 = c.To[2],
			Ease = c.Easing,
		}).ToArray();
		return tl;
	}

	public (float a, float b, float c) ValueAt(double t)
	{
		var segs = _segs;
		if (t < segs[0].S) return (segs[0].A0, segs[0].A1, segs[0].A2);
		// last segment that has started
		int lo = 0, hi = segs.Length - 1;
		while (lo < hi)
		{
			int mid = (lo + hi + 1) >> 1;
			if (segs[mid].S <= t) lo = mid; else hi = mid - 1;
		}
		var s = segs[lo];
		if (t >= s.E) return (s.B0, s.B1, s.B2);
		float p = (float)SbEasing.Apply(s.Ease, (t - s.S) / (s.E - s.S));
		return (s.A0 + (s.B0 - s.A0) * p, s.A1 + (s.B1 - s.A1) * p, s.A2 + (s.B2 - s.A2) * p);
	}
}

/// <summary>P commands: the flag is on during the command, or from its start onwards when it has no duration.</summary>
public class SbFlagTimeline
{
	private (double s, double e)[] _spans = Array.Empty<(double, double)>();

	public static SbFlagTimeline From(List<SbCommand> all, char param) => new()
	{
		_spans = all.Where(c => c.Type == SbCommandType.P && c.Param == param)
			.Select(c => ((double)c.StartTime, c.EndTime > c.StartTime ? (double)c.EndTime : double.MaxValue)).ToArray(),
	};

	public bool At(double t)
	{
		foreach (var (s, e) in _spans)
			if (t >= s && t <= e) return true;
		return false;
	}
}

/// <summary>osu! storyboard easing functions (ids 0-34).</summary>
public static class SbEasing
{
	public static readonly string[] Names =
	{
		"Linear", "Out", "In", "Quad In", "Quad Out", "Quad In/Out", "Cubic In", "Cubic Out", "Cubic In/Out",
		"Quart In", "Quart Out", "Quart In/Out", "Quint In", "Quint Out", "Quint In/Out", "Sine In", "Sine Out",
		"Sine In/Out", "Expo In", "Expo Out", "Expo In/Out", "Circ In", "Circ Out", "Circ In/Out", "Elastic In",
		"Elastic Out", "Elastic Half Out", "Elastic Quarter Out", "Elastic In/Out", "Back In", "Back Out",
		"Back In/Out", "Bounce In", "Bounce Out", "Bounce In/Out",
	};

	private const double ElasticConst = 2 * Math.PI / 0.3;
	private const double ElasticConst2 = 0.3 / 4;
	private const double BackConst = 1.70158;
	private const double BackConst2 = BackConst * 1.525;

	private static double Out(Func<double, double> f, double t) => 1 - f(1 - t);
	private static double InOut(Func<double, double> f, double t) => t < 0.5 ? f(2 * t) / 2 : 1 - f(2 - 2 * t) / 2;

	private static double Quad(double t) => t * t;
	private static double Cubic(double t) => t * t * t;
	private static double Quart(double t) => t * t * t * t;
	private static double Quint(double t) => t * t * t * t * t;
	private static double Sine(double t) => 1 - Math.Cos(t * Math.PI / 2);
	private static double Expo(double t) => t <= 0 ? 0 : Math.Pow(2, 10 * (t - 1));
	private static double Circ(double t) => 1 - Math.Sqrt(Math.Max(0, 1 - t * t));
	private static double Back(double t) => t * t * ((BackConst + 1) * t - BackConst);
	private static double Back2(double t) => t * t * ((BackConst2 + 1) * t - BackConst2);
	private static double BounceOut(double t)
	{
		if (t < 1 / 2.75) return 7.5625 * t * t;
		if (t < 2 / 2.75) { t -= 1.5 / 2.75; return 7.5625 * t * t + 0.75; }
		if (t < 2.5 / 2.75) { t -= 2.25 / 2.75; return 7.5625 * t * t + 0.9375; }
		t -= 2.625 / 2.75;
		return 7.5625 * t * t + 0.984375;
	}
	private static double BounceIn(double t) => 1 - BounceOut(1 - t);

	public static double Apply(int easing, double t)
	{
		t = Math.Clamp(t, 0, 1);
		return easing switch
		{
			1 or 4 => Out(Quad, t),
			2 or 3 => Quad(t),
			5 => InOut(Quad, t),
			6 => Cubic(t),
			7 => Out(Cubic, t),
			8 => InOut(Cubic, t),
			9 => Quart(t),
			10 => Out(Quart, t),
			11 => InOut(Quart, t),
			12 => Quint(t),
			13 => Out(Quint, t),
			14 => InOut(Quint, t),
			15 => Sine(t),
			16 => Out(Sine, t),
			17 => InOut(Sine, t),
			18 => Expo(t),
			19 => Out(Expo, t),
			20 => InOut(Expo, t),
			21 => Circ(t),
			22 => Out(Circ, t),
			23 => InOut(Circ, t),
			24 => -Math.Pow(2, -10 + 10 * t) * Math.Sin((1 - ElasticConst2 - t) * ElasticConst),
			25 => Math.Pow(2, -10 * t) * Math.Sin((t - ElasticConst2) * ElasticConst) + 1,
			26 => Math.Pow(2, -10 * t) * Math.Sin((0.5 * t - ElasticConst2) * ElasticConst) + 1,
			27 => Math.Pow(2, -10 * t) * Math.Sin((0.25 * t - ElasticConst2) * ElasticConst) + 1,
			28 => t < 0.5
				? -0.5 * Math.Pow(2, -10 + 20 * t) * Math.Sin((1 - ElasticConst2 * 1.5 - 2 * t) * ElasticConst / 1.5)
				: 0.5 * Math.Pow(2, -20 * t + 10) * Math.Sin((2 * t - ElasticConst2 * 1.5 - 1) * ElasticConst / 1.5) + 1,
			29 => Back(t),
			30 => Out(Back, t),
			31 => InOut(Back2, t),
			32 => BounceIn(t),
			33 => BounceOut(t),
			34 => InOut(BounceIn, t),
			_ => t,
		};
	}
}

/// <summary>A storyboard sound (<c>Sample,time,layer,"file",volume</c>).</summary>
public class SbSample
{
	public int Time;
	public SbLayer Layer;
	public string Path = "";
	public int Volume = 100;
}

/// <summary>
/// A parsed storyboard: objects in draw order plus samples and the video. <see cref="OtherLines"/> keeps
/// every [Events] line that isn't storyboard content (breaks, comments...) so the editor can write it back.
/// </summary>
public class StoryboardDoc
{
	private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

	public List<SbObject> Objects = new();
	public List<SbSample> Samples = new();
	public string VideoPath;
	public int VideoOffset;
	/// <summary>Background image from the "0,0,\"file\"" event, if the parsed lines had one.</summary>
	public string BackgroundPath;
	/// <summary>[Events] lines kept verbatim (breaks, the video line, samples, anything unknown).</summary>
	public List<string> OtherLines = new();
	/// <summary>Storyboards are 854 wide (16:9) when true, else 640 (4:3).</summary>
	public bool Widescreen = true;

	public bool IsEmpty => Objects.Count == 0 && VideoPath == null && Samples.Count == 0;

	public void CompileAll()
	{
		foreach (var o in Objects) o.Compile();
	}

	/// <summary>Every image file the storyboard uses (animation frames expanded).</summary>
	public IEnumerable<string> ImagePaths()
	{
		foreach (var o in Objects)
		{
			if (!o.IsAnimation) { yield return o.Path; continue; }
			for (int i = 0; i < Math.Clamp(o.FrameCount, 1, 1000); i++) yield return o.FramePath(i);
		}
	}

	// =====================================================================
	// Loading
	// =====================================================================

	/// <summary>
	/// Storyboard of a difficulty: its own [Events] plus the set's .osb (if any), like osu! plays it.
	/// </summary>
	public static StoryboardDoc LoadForBeatmap(string osuPath)
	{
		var doc = new StoryboardDoc();
		if (string.IsNullOrEmpty(osuPath) || !File.Exists(osuPath)) return doc;
		var osuLines = File.ReadAllLines(osuPath);
		doc.Widescreen = ReadWidescreen(osuLines);
		doc.ParseFile(osuLines, fromOsb: false);
		var osb = FindOsb(osuPath);
		if (osb != null)
		{
			try { doc.ParseFile(File.ReadAllLines(osb), fromOsb: true); }
			catch (Exception e) { Godot.GD.PrintErr("[Storyboard] couldn't read " + osb + ": " + e.Message); }
		}
		return doc;
	}

	/// <summary>The set's .osb: osu! names it "Artist - Title (Creator).osb"; fall back to the only .osb in the folder.</summary>
	public static string FindOsb(string osuPath)
	{
		var dir = System.IO.Path.GetDirectoryName(osuPath);
		if (dir == null || !Directory.Exists(dir)) return null;
		var osbs = Directory.GetFiles(dir, "*.osb");
		if (osbs.Length == 0) return null;
		if (osbs.Length == 1) return osbs[0];
		// "Artist - Title (Creator) [Diff].osu" -> "Artist - Title (Creator).osb"
		var name = System.IO.Path.GetFileNameWithoutExtension(osuPath);
		int bracket = name.LastIndexOf(" [", StringComparison.Ordinal);
		if (bracket > 0) name = name[..bracket];
		return osbs.FirstOrDefault(o => string.Equals(System.IO.Path.GetFileNameWithoutExtension(o), name, StringComparison.OrdinalIgnoreCase)) ?? osbs[0];
	}

	private static bool ReadWidescreen(IEnumerable<string> lines)
	{
		foreach (var raw in lines)
		{
			var l = raw.Trim();
			if (l.StartsWith("WidescreenStoryboard", StringComparison.OrdinalIgnoreCase))
				return l.EndsWith("1");
			if (l == "[Events]") break;
		}
		return true;
	}

	/// <summary>Parse the [Variables] and [Events] sections of a .osu or .osb.</summary>
	public void ParseFile(IEnumerable<string> lines, bool fromOsb)
	{
		var vars = new List<(string name, string value)>();
		var events = new List<string>();
		string section = "";
		foreach (var raw in lines)
		{
			var trimmed = raw.Trim();
			if (trimmed.StartsWith("[") && trimmed.EndsWith("]")) { section = trimmed; continue; }
			if (section == "[Variables]")
			{
				int eq = trimmed.IndexOf('=');
				if (trimmed.StartsWith("$") && eq > 1) vars.Add((trimmed[..eq], trimmed[(eq + 1)..]));
			}
			else if (section == "[Events]") events.Add(raw.TrimEnd('\r', '\n'));
		}
		// Longest names first so $ab doesn't eat part of $abc.
		vars.Sort((a, b) => b.name.Length.CompareTo(a.name.Length));
		if (vars.Count > 0)
			for (int i = 0; i < events.Count; i++)
				if (events[i].Contains('$'))
					foreach (var (n, v) in vars) events[i] = events[i].Replace(n, v);
		ParseEvents(events, fromOsb);
	}

	/// <summary>Parse [Events] lines (variables already substituted).</summary>
	public void ParseEvents(IEnumerable<string> lines, bool fromOsb)
	{
		SbObject current = null;
		SbLoop loop = null;
		bool inTrigger = false;
		foreach (var raw in lines)
		{
			if (string.IsNullOrWhiteSpace(raw) || raw.TrimStart().StartsWith("//"))
			{
				if (!fromOsb && raw.TrimStart().StartsWith("//") && !IsSectionComment(raw)) OtherLines.Add(raw.Trim());
				continue;
			}
			int depth = 0;
			while (depth < raw.Length && (raw[depth] == ' ' || raw[depth] == '_')) depth++;
			var line = raw[depth..].Trim();
			var p = SplitCsv(line);
			if (p.Count == 0) continue;

			if (depth == 0)
			{
				current = null;
				loop = null;
				inTrigger = false;
				switch (p[0])
				{
					case "Sprite" or "4":
						if (p.Count < 6) break;
						current = new SbObject
						{
							Layer = ParseLayer(p[1]), Origin = ParseOrigin(p[2]), Path = CleanPath(p[3]),
							X = F(p[4], 320), Y = F(p[5], 240), FromOsb = fromOsb,
						};
						Objects.Add(current);
						break;
					case "Animation" or "6":
						if (p.Count < 8) break;
						current = new SbObject
						{
							IsAnimation = true,
							Layer = ParseLayer(p[1]), Origin = ParseOrigin(p[2]), Path = CleanPath(p[3]),
							X = F(p[4], 320), Y = F(p[5], 240),
							FrameCount = Math.Max(1, (int)F(p[6], 1)), FrameDelay = Math.Max(1, F(p[7], 100)),
							LoopType = p.Count > 8 && (p[8] == "LoopOnce" || p[8] == "1") ? SbLoopType.LoopOnce : SbLoopType.LoopForever,
							FromOsb = fromOsb,
						};
						Objects.Add(current);
						break;
					case "Video" or "1":
						if (p.Count >= 3)
						{
							VideoOffset = (int)F(p[1], 0);
							VideoPath = CleanPath(p[2]);
						}
						if (!fromOsb) OtherLines.Add(line);
						break;
					case "Sample" or "5":
						if (p.Count >= 4)
							Samples.Add(new SbSample
							{
								Time = (int)F(p[1], 0), Layer = ParseLayer(p[2]), Path = CleanPath(p[3]),
								Volume = p.Count > 4 ? (int)F(p[4], 100) : 100,
							});
						if (!fromOsb) OtherLines.Add(line);
						break;
					case "0":
						if (p.Count >= 3 && BackgroundPath == null) BackgroundPath = CleanPath(p[2]);
						if (!fromOsb) OtherLines.Add(line);
						break;
					default:
						// breaks (2,...), colour transformations, unknown
						if (!fromOsb) OtherLines.Add(line);
						break;
				}
				continue;
			}

			if (current == null) continue;
			if (depth == 1)
			{
				loop = null;
				inTrigger = false;
				if (p[0] == "L")
				{
					loop = new SbLoop { StartTime = (int)F(p.ElementAtOrDefault(1), 0), Count = Math.Max(1, (int)F(p.ElementAtOrDefault(2), 1)) };
					current.Loops.Add(loop);
				}
				else if (p[0] == "T")
				{
					inTrigger = true;
					current.RawTriggerLines.Add(" " + line);
				}
				else ParseCommand(p, current.Commands);
			}
			else
			{
				if (inTrigger) current.RawTriggerLines.Add("  " + line);
				else if (loop != null) ParseCommand(p, loop.Commands);
			}
		}
	}

	private static bool IsSectionComment(string raw)
	{
		var t = raw.Trim();
		return t.StartsWith("//Background and Video events") || t.StartsWith("//Break Periods") ||
		       t.StartsWith("//Storyboard Layer") || t.StartsWith("//Storyboard Sound Samples");
	}

	private static void ParseCommand(List<string> p, List<SbCommand> into)
	{
		if (p.Count < 4) return;
		if (!Enum.TryParse<SbCommandType>(p[0], out var type)) return;
		int easing = (int)F(p[1], 0);
		int start = (int)Math.Round(F(p[2], 0));
		int end = string.IsNullOrWhiteSpace(p[3]) ? start : (int)Math.Round(F(p[3], start));
		if (end < start) end = start;

		if (type == SbCommandType.P)
		{
			if (p.Count < 5 || p[4].Length == 0) return;
			into.Add(new SbCommand { Type = type, Easing = easing, StartTime = start, EndTime = end, Param = char.ToUpperInvariant(p[4][0]) });
			return;
		}

		int n = SbCommand.Arity(type);
		var values = p.Skip(4).Select(v => F(v, 0)).ToList();
		if (values.Count < n) return;
		int duration = end - start;
		if (values.Count < 2 * n)
		{
			// single value set: constant
			var c = new SbCommand { Type = type, Easing = easing, StartTime = start, EndTime = end };
			for (int k = 0; k < n; k++) { c.From[k] = values[k]; c.To[k] = values[k]; }
			into.Add(c);
			return;
		}
		// shorthand: a chain of segments, each as long as the first
		int sets = values.Count / n;
		for (int i = 0; i + 1 < sets; i++)
		{
			var c = new SbCommand
			{
				Type = type, Easing = easing,
				StartTime = start + i * duration, EndTime = end + i * duration,
			};
			for (int k = 0; k < n; k++)
			{
				c.From[k] = values[i * n + k];
				c.To[k] = values[(i + 1) * n + k];
			}
			into.Add(c);
		}
	}

	public static SbLayer ParseLayer(string s) => s switch
	{
		"Background" or "0" => SbLayer.Background,
		"Fail" or "1" => SbLayer.Fail,
		"Pass" or "2" => SbLayer.Pass,
		"Foreground" or "3" => SbLayer.Foreground,
		"Overlay" or "4" => SbLayer.Overlay,
		_ => SbLayer.Background,
	};

	public static SbOrigin ParseOrigin(string s)
	{
		if (int.TryParse(s, out var n) && n is >= 0 and <= 9) return (SbOrigin)n;
		return Enum.TryParse<SbOrigin>(s, true, out var o) ? o : SbOrigin.Centre;
	}

	private static string CleanPath(string s) => s.Trim().Trim('"').Replace('\\', '/');

	private static float F(string s, float fallback) =>
		float.TryParse(s, NumberStyles.Float, Inv, out var v) && float.IsFinite(v) ? v : fallback;

	/// <summary>Split on commas, keeping quoted strings together.</summary>
	public static List<string> SplitCsv(string line)
	{
		var result = new List<string>();
		var sb = new StringBuilder();
		bool quoted = false;
		foreach (var ch in line)
		{
			if (ch == '"') { quoted = !quoted; sb.Append(ch); continue; }
			if (ch == ',' && !quoted) { result.Add(sb.ToString().Trim()); sb.Clear(); continue; }
			sb.Append(ch);
		}
		result.Add(sb.ToString().Trim());
		return result;
	}

	// =====================================================================
	// Writing (the editor stores the difficulty's storyboard in its [Events])
	// =====================================================================

	private static string N(float v) => v.ToString("0.###", Inv);

	/// <summary>[Events] lines for this difficulty: kept lines, then every non-.osb object.</summary>
	public List<string> ToEventLines()
	{
		var lines = new List<string>(OtherLines);
		var mine = Objects.Where(o => !o.FromOsb).ToList();
		foreach (var layer in new[] { SbLayer.Background, SbLayer.Fail, SbLayer.Pass, SbLayer.Foreground, SbLayer.Overlay })
		{
			var group = mine.Where(o => o.Layer == layer).ToList();
			if (group.Count == 0) continue;
			lines.Add($"//Storyboard Layer {(int)layer} ({layer})");
			foreach (var o in group) WriteObject(o, lines);
		}
		return lines;
	}

	public static void WriteObject(SbObject o, List<string> lines)
	{
		var path = o.Path.Replace('"', '\'');
		lines.Add(o.IsAnimation
			? $"Animation,{o.Layer},{o.Origin},\"{path}\",{N(o.X)},{N(o.Y)},{o.FrameCount},{N(o.FrameDelay)},{o.LoopType}"
			: $"Sprite,{o.Layer},{o.Origin},\"{path}\",{N(o.X)},{N(o.Y)}");
		foreach (var c in o.Commands) lines.Add(" " + CommandLine(c));
		foreach (var l in o.Loops)
		{
			lines.Add($" L,{l.StartTime},{l.Count}");
			foreach (var c in l.Commands) lines.Add("  " + CommandLine(c));
		}
		lines.AddRange(o.RawTriggerLines);
	}

	public static string CommandLine(SbCommand c)
	{
		var head = $"{c.Type},{c.Easing},{c.StartTime},{(c.EndTime == c.StartTime ? "" : c.EndTime.ToString(Inv))}";
		if (c.Type == SbCommandType.P) return $"{head},{c.Param}";
		int n = SbCommand.Arity(c.Type);
		var from = string.Join(",", c.From.Take(n).Select(N));
		bool same = c.From.Take(n).SequenceEqual(c.To.Take(n));
		return same ? $"{head},{from}" : $"{head},{from},{string.Join(",", c.To.Take(n).Select(N))}";
	}

	/// <summary>Short human description of a command for lists.</summary>
	public static string Describe(SbCommand c)
	{
		string ease = c.Easing > 0 && c.Easing < SbEasing.Names.Length ? $" ({SbEasing.Names[c.Easing]})" : "";
		string time = c.EndTime > c.StartTime ? $"{c.StartTime}-{c.EndTime}" : $"{c.StartTime}";
		string label = c.Type switch
		{
			SbCommandType.F => "Fade", SbCommandType.S => "Scale", SbCommandType.V => "Stretch",
			SbCommandType.R => "Rotate", SbCommandType.M => "Move", SbCommandType.MX => "Move X",
			SbCommandType.MY => "Move Y", SbCommandType.C => "Colour", _ => "Param",
		};
		if (c.Type == SbCommandType.P)
			return $"{time}  {label} {(c.Param == 'H' ? "flip H" : c.Param == 'V' ? "flip V" : "additive")}";
		int n = SbCommand.Arity(c.Type);
		string v(float[] a) => string.Join(" ", a.Take(n).Select(N));
		return c.From.Take(n).SequenceEqual(c.To.Take(n))
			? $"{time}  {label} {v(c.From)}{ease}"
			: $"{time}  {label} {v(c.From)} -> {v(c.To)}{ease}";
	}
}
