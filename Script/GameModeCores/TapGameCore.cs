using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

public enum TapObjectType
{
	Circle,
	Slider,
	Spinner
}

/// <summary>
/// A single hit object for the Tap! gamemode. All positions are in osu! playfield units (512x384).
/// </summary>
public class TapObject
{
	public TapObjectType Type;
	public int Time;
	public int EndTime;
	public Vector2 Pos;
	public Vector2 EndPos;
	public int ComboNumber;
	public int ComboColour;
	public double ppv2xp;

	// Slider path
	public List<Vector2> Path;
	public List<float> PathLen;
	public int Slides = 1;
	public Vector2[] ScreenPath;
	public float ScreenPathScale = -1f;
	// Slider body visual: rendered into a CanvasGroup so it fades as one flat image
	public CanvasGroup BodyGroup;
	public Line2D BodyBorder;
	public Line2D BodyFill;
	public int BodyShownCount = -1;

	// Runtime state
	public bool HeadDone;
	public int HeadResult = -1;
	public float HeadOffset;
	public bool Judged;
	public int Result = -1;
	public float JudgedAt;
	public float ShakeAt = float.MinValue;
	public bool Tracking;
	public float TrackedMs;
	public float SpinTotal;
	public float SpinLastAngle = float.NaN;
	public float SpinRequired = 1f;

	/// <summary>Position in TapGameCore.Objects (judgements are sent to LAN spectators by index).</summary>
	public int Index;

	public float Duration => Math.Max(1, EndTime - Time);
	public float TotalLength => PathLen == null || PathLen.Count == 0 ? 0f : PathLen[^1];

	public Vector2 PathAt(float dist)
	{
		if (Path == null || Path.Count == 0) return Pos;
		if (dist <= 0) return Path[0];
		if (dist >= TotalLength) return Path[^1];
		int lo = 0, hi = PathLen.Count - 1;
		while (lo < hi)
		{
			int mid = (lo + hi) / 2;
			if (PathLen[mid] < dist) lo = mid + 1; else hi = mid;
		}
		if (lo == 0) return Path[0];
		float seg = PathLen[lo] - PathLen[lo - 1];
		float t = seg <= 0 ? 0 : (dist - PathLen[lo - 1]) / seg;
		return Path[lo - 1].Lerp(Path[lo], t);
	}

	/// <summary>Ball position on a slider at song time 'now' (ms).</summary>
	public Vector2 SliderBallAt(float now)
	{
		float e = Mathf.Clamp(now - Time, 0, Duration);
		float slideDur = Duration / Slides;
		int span = Math.Min(Slides - 1, (int)(e / slideDur));
		float p = (e - span * slideDur) / slideDur;
		if (span % 2 == 1) p = 1f - p;
		return PathAt(p * TotalLength);
	}
}

/// <summary>
/// Tap! - an aim-and-click gamemode inspired by osu!standard. Circles, sliders and spinners
/// </summary>
public partial class TapGameCore : Control
{
	public static readonly Vector2 FieldSize = new Vector2(512, 384);
	private static readonly Vector2 SpinnerCenter = new Vector2(256, 192);

	private string oldtitle = "";
	private SettingsOperator SettingsOperator { get; set; }
	public static ApiOperator ApiOperator { get; set; }
	public static int score { get; set; }
	public static List<DanceCounter> dance { get; set; }
	public static bool Dead { get; set; }
	public static int seed = 0;

	public List<TapObject> Objects = new List<TapObject>();
	private List<Color> ComboColours = new List<Color>();

	// Difficulty
	private float CS = 4, AR = 9, OD = 8, SliderMultiplier = 1.4f;
	public float Radius { get; private set; } = 36f;
	private float Preempt = 600, FadeIn = 400;
	private float W300 = 32, W100 = 76, W50 = 120;

	// Runtime
	private TapPlayfield Field { get; set; }
	private TextureRect Beatmap_Background { get; set; }
	private Timer WaitClock { get; set; }
	private Control HUD { get; set; }
	private Label ComboCounter { get; set; }
	private Control SpectatorPanel { get; set; }
	private Node PauseMenu { get; set; }
	private bool HideHUD { get; set; }
	private bool songstarted = false;
	private bool Finished { get; set; }
	private bool erroredout = false;
	private bool Auto { get; set; }
	private int BadCombo { get; set; }
	private int FirstActive { get; set; }
	private int LastEndTime { get; set; }
	private float speedold = 1f;
	// pp v3 / competitive score state (see Game.PerformanceCalculator).
	private long comboPortion;
	private double scoreMult = 1.0;
	private double maxPpMods;
	private void AddComboWeight(int judgementWeight) =>
		comboPortion += Game.PerformanceCalculator.ComboWeight(judgementWeight, SettingsOperator.Gameplaycfg.Combo);
	private double CurrentPerformance(bool final) => final
		? Game.PerformanceCalculator.Performance(maxPpMods, SettingsOperator.Gameplaycfg.NoteCount,
			SettingsOperator.Gameplaycfg.Max, SettingsOperator.Gameplaycfg.Great, SettingsOperator.Gameplaycfg.Meh, SettingsOperator.Gameplaycfg.Bad)
		: Game.PerformanceCalculator.LivePerformance(maxPpMods, SettingsOperator.Gameplaycfg.NoteCount,
			SettingsOperator.Gameplaycfg.Max, SettingsOperator.Gameplaycfg.Great, SettingsOperator.Gameplaycfg.Meh, SettingsOperator.Gameplaycfg.Bad);
	private float audioOffset { get; set; } = 0;
	private float gametime { get; set; }
	private readonly SongClock Clock = new SongClock();
	private long startedtime { get; set; } = 0;
	private int scoreint { get; set; }
	private Tween scoretween { get; set; }
	private Tween MainScreenAnimation { get; set; }
	private Tween HurtAnimation { get; set; }
	private Tween ComboTween { get; set; }
	private Tween HUDTween { get; set; }
	private int DanceIndex { get; set; }
	private float dim { get; set; }
	private bool BreakTime { get; set; } = true;
	private Break Break { get; set; }
	private Tween BreakBGTween { get; set; }
	private static readonly Game.ScoreCalculator ScoreCalc = new Game.ScoreCalculator();

	// Input: [0],[1] = keyboard keys, [2],[3] = left/right mouse buttons
	private readonly bool[] Pressed = new bool[4];
	private readonly string[] KeyCodes = new string[2];
	private bool Held => Auto ? AutoHeld : Pressed.Any(p => p);
	private Vector2 CursorPos { get; set; } // osu units
	private bool AutoHeld { get; set; }

	// Visual state
	private Vector2 FieldOffset;
	private float FieldScale = 1f;
	private readonly List<(Vector2 pos, Texture2D tex, float time)> Popups = new();
	private readonly List<float> HitOffsets = new List<float>();

	private string _hitSoundPath;
	private string HitSoundPath => _hitSoundPath ??= "res://SelectableSkins/Slia/Sounds/" + (
		SettingsOperator.Gameplaycfg.SampleSet == SampleSet.Type[0] ? SampleSet.Normal.First() :
		SettingsOperator.Gameplaycfg.SampleSet == SampleSet.Type[1] ? SampleSet.Soft.First() :
		SettingsOperator.Gameplaycfg.SampleSet == SampleSet.Type[2] ? SampleSet.Drum.First() : SampleSet.Normal.First());

	// ───────────────────────────── Beatmap parsing ─────────────────────────────

	private struct TimingPoint
	{
		public int Time;
		public float BeatLength;
		public bool Uninherited;
	}

	private static float ParseF(string s, float def = 0f) =>
		float.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : def;

	private static int ParseI(string s, int def = 0) =>
		int.TryParse(s.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v
		: (float.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? (int)f : def);

	public void ReloadBeatmap(string filepath)
	{
		Objects.Clear();
		ComboColours.Clear();
		using var file = FileAccess.Open(filepath, FileAccess.ModeFlags.Read);
		var lines = file.GetAsText().Split("\n");
		BeatmapLegend beatmap = SettingsOperator.Beatmaps[SettingsOperator.SessionConfig.SongID];
		dance = beatmap.Dance;

		var timing = new List<TimingPoint>();
		var hitLines = new List<string>();
		float arValue = -1;
		string section = "";
		foreach (var raw in lines)
		{
			var line = raw.Trim();
			if (string.IsNullOrWhiteSpace(line) || line.StartsWith("//")) continue;
			if (line.StartsWith("[")) { section = line; continue; }
			switch (section)
			{
				case "[Difficulty]":
				{
					var kv = line.Split(':', 2);
					if (kv.Length < 2) break;
					switch (kv[0].Trim())
					{
						case "CircleSize": CS = ParseF(kv[1], 4); break;
						case "OverallDifficulty": OD = ParseF(kv[1], 5); break;
						case "ApproachRate": arValue = ParseF(kv[1], -1); break;
						case "SliderMultiplier": SliderMultiplier = ParseF(kv[1], 1.4f); break;
					}
					break;
				}
				case "[Colours]":
				{
					var kv = line.Split(':', 2);
					if (kv.Length < 2 || !kv[0].Trim().StartsWith("Combo")) break;
					var rgb = kv[1].Split(',');
					if (rgb.Length >= 3)
						ComboColours.Add(new Color(ParseI(rgb[0]) / 255f, ParseI(rgb[1]) / 255f, ParseI(rgb[2]) / 255f));
					break;
				}
				case "[TimingPoints]":
				{
					var p = line.Split(',');
					if (p.Length < 2) break;
					timing.Add(new TimingPoint
					{
						Time = (int)ParseF(p[0]),
						BeatLength = ParseF(p[1], 500),
						Uninherited = p.Length < 7 || p[6].Trim() != "0"
					});
					break;
				}
				case "[HitObjects]":
					hitLines.Add(line);
					break;
			}
		}

		AR = arValue < 0 ? OD : arValue;
		if (ComboColours.Count == 0)
		{
			ComboColours.AddRange(new[]
			{
				new Color("#ff6aa8"), new Color("#6ad1ff"), new Color("#ffd36a"), new Color("#9cff6a")
			});
		}
		timing.Sort((a, b) => a.Time.CompareTo(b.Time));

		Radius = 54.4f - 4.48f * CS;
		Preempt = AR < 5 ? 1200f + 600f * (5f - AR) / 5f : 1200f - 750f * (AR - 5f) / 5f;
		FadeIn = Math.Min(400f, Preempt * 0.66f);
		W300 = 80f - 6f * OD;
		W100 = 140f - 8f * OD;
		W50 = 200f - 10f * OD;
		float rps = OD < 5 ? 1.5f + (2.5f - 1.5f) * OD / 5f : 2.5f + (3.75f - 2.5f) * (OD - 5f) / 5f;

		int comboNumber = 0;
		int comboColour = -1;
		bool forceNewCombo = true;
		int index = 0;
		double multiplier = ModsOperator.ProcessMultiplierByModList(ModsOperator.Mods);

		foreach (var line in hitLines)
		{
			var p = line.Split(',');
			if (p.Length < 4) { continue; }
			int type = ParseI(p[3]);
			var obj = new TapObject
			{
				Pos = new Vector2(ParseF(p[0]), ParseF(p[1])),
				Time = ParseI(p[2]),
			};
			obj.EndTime = obj.Time;
			obj.EndPos = obj.Pos;

			if ((type & 8) != 0)
			{
				obj.Type = TapObjectType.Spinner;
				obj.Pos = SpinnerCenter;
				obj.EndPos = SpinnerCenter;
				obj.EndTime = p.Length > 5 ? Math.Max(obj.Time + 1, ParseI(p[5], obj.Time + 1)) : obj.Time + 1;
				obj.SpinRequired = Math.Max(1f, (obj.EndTime - obj.Time) / 1000f * rps);
			}
			else if ((type & 2) != 0 && p.Length >= 8)
			{
				obj.Type = TapObjectType.Slider;
				var curve = p[5].Split('|');
				char curveType = curve[0].Length > 0 ? curve[0][0] : 'L';
				var pts = new List<Vector2> { obj.Pos };
				for (int i = 1; i < curve.Length; i++)
				{
					var xy = curve[i].Split(':');
					if (xy.Length >= 2) pts.Add(new Vector2(ParseF(xy[0]), ParseF(xy[1])));
				}
				obj.Slides = Math.Max(1, ParseI(p[6], 1));
				float pixelLength = ParseF(p[7], 0);
				BuildSliderPath(obj, curveType, pts, pixelLength);

				// Duration from timing points
				float beat = 500f, sv = 1f;
				foreach (var tp in timing)
				{
					if (tp.Time > obj.Time) break;
					if (tp.Uninherited) { beat = tp.BeatLength; sv = 1f; }
					else if (tp.BeatLength < 0) sv = Mathf.Clamp(-100f / tp.BeatLength, 0.1f, 10f);
				}
				if (timing.Count > 0 && timing[0].Time > obj.Time && timing[0].Uninherited) beat = timing[0].BeatLength;
				float slideDur = obj.TotalLength / (SliderMultiplier * 100f * sv) * beat;
				obj.EndTime = obj.Time + Math.Max(1, (int)(slideDur * obj.Slides));
				obj.EndPos = obj.Slides % 2 == 1 ? obj.PathAt(obj.TotalLength) : obj.Pos;
			}
			else
			{
				obj.Type = TapObjectType.Circle;
			}

			bool newCombo = (type & 4) != 0 || forceNewCombo || obj.Type == TapObjectType.Spinner;
			if (newCombo)
			{
				int skip = (type >> 4) & 7;
				comboColour = (comboColour + 1 + skip) % ComboColours.Count;
				comboNumber = 1;
			}
			else comboNumber++;
			forceNewCombo = obj.Type == TapObjectType.Spinner;
			obj.ComboNumber = comboNumber;
			obj.ComboColour = Math.Max(0, comboColour);

			Objects.Add(obj);
			index++;
		}

		LastEndTime = Objects.Count > 0 ? Objects.Max(o => o.EndTime) : 0;
		SettingsOperator.Gameplaycfg.NoteCount = Objects.Count;
		comboPortion = 0;
		scoreMult = ModsOperator.ScoreMultiplierExact(ModsOperator.Mods);
		// No mode-specific star rating yet: the map's NoMod max pp with the pp mod factors.
		maxPpMods = SettingsOperator.Gameplaycfg.maxpp * Game.PerformanceCalculator.PpMultiplier(ModsOperator.AliasOf(ModsOperator.Mods));
		GD.Print($"[Tap!] Loaded {Objects.Count} objects (CS {CS}, AR {AR}, OD {OD})");
	}

	private static void BuildSliderPath(TapObject obj, char curveType, List<Vector2> pts, float pixelLength)
	{
		var raw = new List<Vector2>();
		if (pts.Count < 2)
		{
			raw.Add(obj.Pos);
			raw.Add(obj.Pos + new Vector2(1, 0));
		}
		else if (curveType == 'L')
		{
			raw.AddRange(pts);
		}
		else if (curveType == 'P' && pts.Count == 3 && TryArc(pts[0], pts[1], pts[2], raw))
		{
		}
		else if (curveType == 'C')
		{
			for (int i = 0; i < pts.Count - 1; i++)
			{
				var v0 = i > 0 ? pts[i - 1] : pts[i];
				var v1 = pts[i];
				var v2 = pts[i + 1];
				var v3 = i < pts.Count - 2 ? pts[i + 2] : v2 + (v2 - v1);
				int n = Math.Max(2, (int)(v1.DistanceTo(v2) / 4f));
				for (int s = 0; s < n; s++)
					raw.Add(CatmullRom(v0, v1, v2, v3, (float)s / n));
			}
			raw.Add(pts[^1]);
		}
		else
		{
			// Bezier; a repeated point marks a red anchor (segment boundary)
			var segment = new List<Vector2> { pts[0] };
			for (int i = 1; i < pts.Count; i++)
			{
				if (pts[i] == pts[i - 1] && segment.Count > 1)
				{
					AddBezier(segment, raw);
					segment = new List<Vector2>();
				}
				segment.Add(pts[i]);
			}
			AddBezier(segment, raw);
		}

		// Cumulative lengths, truncated / extended to the expected pixel length
		var path = new List<Vector2> { raw[0] };
		var len = new List<float> { 0f };
		for (int i = 1; i < raw.Count; i++)
		{
			float d = raw[i].DistanceTo(path[^1]);
			if (d < 0.01f) continue;
			if (pixelLength > 0 && len[^1] + d >= pixelLength)
			{
				path.Add(path[^1] + (raw[i] - path[^1]).Normalized() * (pixelLength - len[^1]));
				len.Add(pixelLength);
				break;
			}
			path.Add(raw[i]);
			len.Add(len[^1] + d);
		}
		if (path.Count < 2)
		{
			path.Add(path[0] + new Vector2(1, 0));
			len.Add(1f);
		}
		if (pixelLength > 0 && len[^1] < pixelLength - 0.5f)
		{
			var dir = (path[^1] - path[^2]).Normalized();
			path.Add(path[^1] + dir * (pixelLength - len[^1]));
			len.Add(pixelLength);
		}
		obj.Path = path;
		obj.PathLen = len;
	}

	private static void AddBezier(List<Vector2> ctrl, List<Vector2> output)
	{
		if (ctrl.Count == 0) return;
		if (ctrl.Count == 1) { output.Add(ctrl[0]); return; }
		float approx = 0;
		for (int i = 1; i < ctrl.Count; i++) approx += ctrl[i].DistanceTo(ctrl[i - 1]);
		int n = Mathf.Clamp((int)(approx / 3f), 2, 600);
		var tmp = new Vector2[ctrl.Count];
		for (int s = 0; s <= n; s++)
		{
			float t = (float)s / n;
			ctrl.CopyTo(tmp);
			for (int k = ctrl.Count - 1; k > 0; k--)
				for (int j = 0; j < k; j++)
					tmp[j] = tmp[j].Lerp(tmp[j + 1], t);
			output.Add(tmp[0]);
		}
	}

	private static Vector2 CatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
	{
		float t2 = t * t, t3 = t2 * t;
		return 0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
	}

	private static bool TryArc(Vector2 a, Vector2 b, Vector2 c, List<Vector2> output)
	{
		float d = 2f * (a.X * (b.Y - c.Y) + b.X * (c.Y - a.Y) + c.X * (a.Y - b.Y));
		if (Math.Abs(d) < 0.001f) return false;
		float aSq = a.LengthSquared(), bSq = b.LengthSquared(), cSq = c.LengthSquared();
		var center = new Vector2(
			(aSq * (b.Y - c.Y) + bSq * (c.Y - a.Y) + cSq * (a.Y - b.Y)) / d,
			(aSq * (c.X - b.X) + bSq * (a.X - c.X) + cSq * (b.X - a.X)) / d);
		float r = a.DistanceTo(center);
		float t0 = Mathf.Atan2(a.Y - center.Y, a.X - center.X);
		float t2 = Mathf.Atan2(c.Y - center.Y, c.X - center.X);
		bool ccw = (b - a).Cross(c - a) > 0;
		if (ccw) while (t2 < t0) t2 += Mathf.Tau;
		else while (t2 > t0) t2 -= Mathf.Tau;
		int n = Mathf.Clamp((int)(Math.Abs(t2 - t0) * r / 3f), 2, 1000);
		for (int i = 0; i <= n; i++)
		{
			float t = Mathf.Lerp(t0, t2, (float)i / n);
			output.Add(center + new Vector2(Mathf.Cos(t), Mathf.Sin(t)) * r);
		}
		// Let the length truncation run past c if pixelLength is longer
		float tEnd = t2 + (ccw ? 1 : -1) * Mathf.Pi * 0.5f;
		output.Add(center + new Vector2(Mathf.Cos(tEnd), Mathf.Sin(tEnd)) * r);
		return true;
	}

	// ───────────────────────────── Lifecycle ─────────────────────────────

	public override void _EnterTree()
	{
		SettingsOperator.inGameplay = true;
		oldtitle = GetWindow().Title;
		DisplayServer.WindowSetTitle($"{oldtitle} - {SettingsOperator.SessionConfig.BeatmapArtist ?? ""} - {SettingsOperator.SessionConfig.BeatmapTitle ?? ""}");
		SettingsOperator.Gameplaycfg.Username = ApiOperator.Username;
		SettingsOperator.Gameplaycfg.EpochTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
		SettingsOperator.ResetScore();
		SettingsOperator.Resetms();
	}

	public override void _Ready()
	{
		speedold = AudioPlayer.Instance.PitchScale;
		seed = new Random().Next(1, 214562543);
		Gameplay.seed = seed;
		SettingsOperator = GetNode<SettingsOperator>("/root/SettingsOperator");
		SpectatorPanel = GD.Load<PackedScene>("res://Panels/Overlays/SpectatorSettings.tscn").Instantiate().GetNode<Control>(".");
		ApiOperator = GetNode<ApiOperator>("/root/ApiOperator");
		Beatmap_Background = GetNode<TextureRect>("./Beatmap_Background");
		WaitClock = GetNode<Timer>("Wait");
		AudioPlayer.Instance.Stop();
		ClipContents = true;
		HUD = GetNode<Control>("HUD");
		HideHUD = Check.CheckBoolValue(SettingsOperator.GetSetting("hidehud").ToString());
		HUD.Visible = !HideHUD;
		if (HUD.HasNode("Combo")) ComboCounter = HUD.GetNode<Label>("Combo");

		Dead = false;
		Gameplay.Dead = false;
		BeatmapBackground.FlashEnable = false;
		HealthBar.Reset();

		Field = new TapPlayfield { Core = this, Name = "TapPlayfield" };
		AddChild(Field);
		MoveChild(Field, Beatmap_Background.GetIndex() + 1);

		KeyCodes[0] = SettingsOperator.GetSetting("TapKey0")?.ToString() ?? "Z";
		KeyCodes[1] = SettingsOperator.GetSetting("TapKey1")?.ToString() ?? "X";

		HitOffsets.Clear();
		if (SettingsOperator.SessionConfig.BeatmapURL != null) ReloadBeatmap(SettingsOperator.SessionConfig.BeatmapURL.ToString());
		if (Objects.Count > 0) CursorPos = Objects[0].Pos;
		for (int i = 0; i < Objects.Count; i++) Objects[i].Index = i;

		Auto = ModsOperator.Mods["auto"];
		if (Auto)
		{
			Replay.ResetReplay();
			SettingsOperator.SpectatorMode = true; // Auto never saves a replay or submits a score
			AddChild(SpectatorPanel);
		}
		else
		{
			if (SettingsOperator.SpectatorMode)
				Notify.Post("Replays aren't supported in Tap! yet, playing live instead.");
			Replay.ResetReplay();
		}
		Cursor.CursorVisible = true;
		audioOffset = !SettingsOperator.SpectatorMode ? SettingsOperator.AudioOffset * 0.001f : 0f;
		if (LanNetwork.InMatch) LanNetwork.Instance.SpecSendChart(BuildSpecChart());
	}

	/// <summary>The chart as LAN spectators draw it: object kind, times and a few points along each slider.</summary>
	private LanSpecChart BuildSpecChart()
	{
		const int SliderSamples = 12;
		var pts = new List<float>();
		var starts = new int[Objects.Count];
		for (int i = 0; i < Objects.Count; i++)
		{
			var o = Objects[i];
			starts[i] = pts.Count / 2;
			if (o.Type == TapObjectType.Slider && o.TotalLength > 0)
			{
				for (int k = 0; k <= SliderSamples; k++)
				{
					var p = o.PathAt(o.TotalLength * k / SliderSamples);
					pts.Add(p.X); pts.Add(p.Y);
				}
			}
			else
			{
				var p = o.Type == TapObjectType.Spinner ? SpinnerCenter : o.Pos;
				pts.Add(p.X); pts.Add(p.Y);
			}
		}
		return new LanSpecChart
		{
			Mode = 3,
			Height = 384,
			Preempt = Preempt,
			Radius = Radius,
			Times = Objects.Select(o => o.Time).ToArray(),
			Ends = Objects.Select(o => o.Type == TapObjectType.Circle ? o.Time : o.EndTime).ToArray(),
			Lanes = Objects.Select(o => (byte)((int)o.Type | (Math.Clamp(o.Slides, 1, 63) << 2))).ToArray(), // type + repeats
			PtStart = starts,
			Pts = pts.ToArray(),
		};
	}

	public override void _Notification(int what)
	{
		// Keys released while the pause menu was open never reach _Input
		if (what == NotificationUnpaused) Array.Fill(Pressed, false);
	}

	public override void _ExitTree()
	{
		// Only added to the tree when spectating; otherwise it was an orphan that leaked every play.
		if (IsInstanceValid(SpectatorPanel) && !SpectatorPanel.IsInsideTree()) SpectatorPanel.Free();
		SettingsOperator.inGameplay = false;
		DisplayServer.WindowSetTitle(oldtitle);
		HurtAnimation?.Kill();
		Modulate = new Color("#FFFFFF");
		Cursor.CursorVisible = true;
		SettingsOperator.SpectatorMode = false;
		AudioPlayer.Instance.PitchScale = speedold;
	}

	private void StartPlay()
	{
		BreakOver();
		songstarted = true;
		AudioPlayer.Instance.Play();
	}

	public float GetRemainingTime(float delta = 0f)
	{
		double songTime = Clock.Update(AudioPlayer.Instance, delta);
		SettingsOperator.Gameplaycfg.Time = -WaitClock.TimeLeft + songTime;
		SettingsOperator.Gameplaycfg.Time -= startedtime;
		SettingsOperator.Gameplaycfg.Time += audioOffset;
		return (float)SettingsOperator.Gameplaycfg.Time;
	}

	// ───────────────────────────── Input ─────────────────────────────

	public override void _Input(InputEvent @event)
	{
		if (Auto || Dead || Finished) return;
		int slot = -1;
		bool down = false;
		if (@event is InputEventKey key && !key.Echo)
		{
			var code = key.Keycode.ToString();
			if (code == KeyCodes[0]) slot = 0;
			else if (code == KeyCodes[1]) slot = 1;
			down = key.Pressed;
		}
		else if (@event is InputEventMouseButton mb)
		{
			if (mb.ButtonIndex == MouseButton.Left) slot = 2;
			else if (mb.ButtonIndex == MouseButton.Right) slot = 3;
			down = mb.Pressed;
		}
		if (slot < 0) return;
		bool wasDown = Pressed[slot];
		Pressed[slot] = down;
		if (wasDown != down && LanNetwork.InMatch) LanNetwork.Instance.SpecKey(slot, down, gametime);
		if (down && !wasDown)
		{
			if (Field != null) CursorPos = FromScreen(Field.GetLocalMousePosition());
			OnPress(GetRemainingTime(0f) / 0.001f);
		}
	}

	private int WindowResult(float absOffset)
	{
		if (absOffset <= W300) return 0;
		if (absOffset <= W100) return 1;
		if (absOffset <= W50) return 2;
		return 3;
	}

	private void OnPress(float now)
	{
		for (int i = FirstActive; i < Objects.Count; i++)
		{
			var o = Objects[i];
			if (o.Time - Preempt > now) break;
			if (o.Judged || o.HeadDone || o.Type == TapObjectType.Spinner) continue;
			if (CursorPos.DistanceTo(o.Pos) > Radius) continue;

			float offset = now - o.Time;
			if (offset < -W50)
			{
				o.ShakeAt = now; // too early: shake, no judgement
				return;
			}
			HitHead(o, WindowResult(Math.Abs(offset)), offset, now);
			return;
		}
	}

	private void HitHead(TapObject o, int result, float offset, float now)
	{
		if (o.Type == TapObjectType.Circle)
		{
			ApplyJudge(o, result, offset, o.Pos, now);
			return;
		}
		// Slider head: final judgement is decided at the slider end
		o.HeadDone = true;
		o.HeadResult = result;
		o.HeadOffset = offset;
		o.Tracking = true;
		Sample.PlaySample(HitSoundPath);
	}

	// ───────────────────────────── Judgement ─────────────────────────────

	private void ApplyJudge(TapObject o, int result, float offset, Vector2 popupPos, float now)
	{
		o.Judged = true;
		o.Result = result;
		o.JudgedAt = now;
		if (LanNetwork.InMatch) LanNetwork.Instance.SpecJudge(o.Index, result, now);
		switch (result)
		{
			case 0:
				SettingsOperator.Gameplaycfg.Max++;
				SettingsOperator.Gameplaycfg.Combo++;
				AddComboWeight(Game.PerformanceCalculator.WPerfect);
				HealthBar.Heal((5 * (SettingsOperator.Gameplaycfg.Combo / 100)) + 1);
				Popups.Add((popupPos, Skin.Element.JudgePerfect, now));
				break;
			case 1:
				SettingsOperator.Gameplaycfg.Great++;
				SettingsOperator.Gameplaycfg.Combo++;
				AddComboWeight(Game.PerformanceCalculator.WGreat);
				HealthBar.Heal((3 * (SettingsOperator.Gameplaycfg.Combo / 300)) + 1);
				Popups.Add((popupPos, Skin.Element.JudgeGreat, now));
				break;
			case 2:
				SettingsOperator.Gameplaycfg.Meh++;
				SettingsOperator.Gameplaycfg.Combo++;
				AddComboWeight(Game.PerformanceCalculator.WMeh);
				HealthBar.Heal((1 * (SettingsOperator.Gameplaycfg.Combo / 500)) + 1);
				Popups.Add((popupPos, Skin.Element.JudgeMeh, now));
				break;
			default:
				SettingsOperator.Gameplaycfg.Bad++;
				BadCombo++;
				HealthBar.Damage(5 * BadCombo);
				Popups.Add((popupPos, Skin.Element.JudgeMiss, now));
				ComboBreak();
				break;
		}
		if (result < 3)
		{
			BadCombo = 0;
			Sample.PlaySample(HitSoundPath);
			ComboAnimation();
			if (o.Type != TapObjectType.Spinner)
			{
				SettingsOperator.Addms(offset);
				HitOffsets.Add(offset);
				SettingsOperator.Gameplaycfg.ms = ComputeUnstableRate();
			}
		}
	}

	private void ComboBreak()
	{
		if (SettingsOperator.Gameplaycfg.Combo > 50) Sample.PlaySample("res://SelectableSkins/Slia/Sounds/combobreak.wav");
		SettingsOperator.Gameplaycfg.Combo = 0;
		ComboAnimation();
		if (HurtAnimation != null && HurtAnimation.IsRunning()) HurtAnimation.Stop();
		if (GetTree().CurrentScene is CanvasItem canvasScene) canvasScene.Modulate = new Color(1f, 0.5f, 0.5f, 1f);
		HurtAnimation = CreateTween();
		HurtAnimation.TweenProperty(GetTree().CurrentScene, "modulate", new Color(1f, 1f, 1f, 1f), 0.5f)
			.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
	}

	private float ComputeUnstableRate()
	{
		if (HitOffsets.Count < 2) return 0f;
		float mean = HitOffsets.Average();
		float variance = HitOffsets.Select(x => (x - mean) * (x - mean)).Average();
		return (float)Math.Sqrt(variance) * 10f;
	}

	public static float ReloadAccuracy(int max, int great, int meh, int bad) => Gameplay.ReloadAccuracy(max, great, meh, bad);

	// ───────────────────────────── Game tick ─────────────────────────────

	private void UpdateAuto(float now)
	{
		AutoHeld = false;
		int next = -1;
		for (int i = FirstActive; i < Objects.Count; i++)
		{
			var o = Objects[i];
			if (o.Time > now) { next = i; break; }
			if (o.EndTime >= now && o.Type != TapObjectType.Circle)
			{
				AutoHeld = true;
				CursorPos = o.Type == TapObjectType.Slider
					? o.SliderBallAt(now)
					: SpinnerCenter + new Vector2(Mathf.Cos(now * 0.035f), Mathf.Sin(now * 0.035f)) * 60f;
				return;
			}
		}
		if (next < 0) return;
		var target = Objects[next];
		var prev = next > 0 ? Objects[next - 1] : null;
		Vector2 from = prev?.Type == TapObjectType.Spinner ? SpinnerCenter : prev?.EndPos ?? target.Pos;
		float start = prev?.EndTime ?? (target.Time - 1000);
		float span = Math.Max(1, target.Time - start);
		float t = Mathf.Clamp((now - start) / span, 0, 1);
		t = 1f - (1f - t) * (1f - t); // ease out
		CursorPos = from.Lerp(target.Pos, t);
	}

	private void GameTick(double delta)
	{
		float now = gametime;
		float dtMs = (float)delta * 1000f * AudioPlayer.Instance.PitchScale;

		if (Auto) UpdateAuto(now);
		else if (Field != null) CursorPos = FromScreen(Field.GetLocalMousePosition());
		if (LanNetwork.InMatch) LanNetwork.Instance.SpecCursor(now, CursorPos);

		// Break detection (long gap before the next object)
		if (!BreakTime && songstarted)
		{
			int n = FirstActive;
			while (n < Objects.Count && Objects[n].Judged) n++;
			bool busy = Objects.Skip(FirstActive).Take(n - FirstActive + 1).Any(o => o.Time <= now && !o.Judged);
			if (n < Objects.Count && !busy && Objects[n].Time - Preempt - now >= 4500)
				BreakNow((int)(Objects[n].Time - Preempt));
		}

		for (int i = FirstActive; i < Objects.Count; i++)
		{
			var o = Objects[i];
			if (o.Time - Preempt > now) break;
			if (o.Judged) continue;

			switch (o.Type)
			{
				case TapObjectType.Circle:
					if (Auto && now >= o.Time) HitHead(o, 0, now - o.Time, now);
					else if (now - o.Time > W50) ApplyJudge(o, 3, 0, o.Pos, now);
					break;

				case TapObjectType.Slider:
					if (!o.HeadDone)
					{
						if (Auto && now >= o.Time) HitHead(o, 0, now - o.Time, now);
						else if (now - o.Time > W50 || now >= o.EndTime)
						{
							o.HeadDone = true;
							o.HeadResult = 3;
							o.Tracking = false;
							ComboBreak();
						}
					}
					if (now >= o.Time && now <= o.EndTime)
					{
						var ball = o.SliderBallAt(now);
						float follow = o.Tracking ? Radius * 2.4f : Radius;
						o.Tracking = Held && CursorPos.DistanceTo(ball) <= follow;
						if (o.Tracking) o.TrackedMs += dtMs;
					}
					if (now >= o.EndTime) FinishSlider(o, now);
					break;

				case TapObjectType.Spinner:
					if (now >= o.Time && now <= o.EndTime)
					{
						if (Held)
						{
							var v = CursorPos - SpinnerCenter;
							float angle = Mathf.Atan2(v.Y, v.X);
							if (!float.IsNaN(o.SpinLastAngle) && v.Length() > 2f)
							{
								float d = Mathf.Wrap(angle - o.SpinLastAngle, -Mathf.Pi, Mathf.Pi);
								float cap = 8f * Mathf.Tau * (float)delta; // ~480rpm max
								o.SpinTotal += Mathf.Clamp(d, -cap, cap);
							}
							o.SpinLastAngle = angle;
						}
						else o.SpinLastAngle = float.NaN;
					}
					if (now > o.EndTime)
					{
						float ratio = Math.Abs(o.SpinTotal) / Mathf.Tau / o.SpinRequired;
						int result = ratio >= 1f ? 0 : ratio >= 0.75f ? 1 : ratio >= 0.5f ? 2 : 3;
						ApplyJudge(o, result, 0, SpinnerCenter, now);
					}
					break;
			}
		}

		while (FirstActive < Objects.Count && Objects[FirstActive].Judged && now - Objects[FirstActive].JudgedAt > 300)
			FirstActive++;

		Popups.RemoveAll(p => now - p.time > 700);
	}

	private void FinishSlider(TapObject o, float now)
	{
		float frac = o.TrackedMs / o.Duration;
		int head = o.HeadResult < 0 ? 3 : o.HeadResult;
		int result;
		if (head <= 2 && frac >= 0.9f) result = head;
		else if (head <= 2 && frac >= 0.5f) result = Math.Max(head, 1);
		else if (head <= 2 || frac >= 0.5f) result = 2;
		else result = 3;
		ApplyJudge(o, result, head <= 2 ? o.HeadOffset : 0, o.EndPos, now);
	}

	// ───────────────────────────── Per-frame ─────────────────────────────

	public override void _Process(double delta)
	{
		dim = 1f - (1f * (SettingsOperator.SessionConfig.Backgrounddim * 0.01f));
		// While playing: projected score (drops on misses). Once finished: the exact final score.
		score = Finished
			? ScoreCalc.ProcessScore(SettingsOperator.Gameplaycfg.NoteCount, SettingsOperator.Gameplaycfg.Max,
				SettingsOperator.Gameplaycfg.Great, SettingsOperator.Gameplaycfg.Meh, comboPortion, scoreMult)
			: Game.PerformanceCalculator.LiveScore(SettingsOperator.Gameplaycfg.Max, SettingsOperator.Gameplaycfg.Great,
				SettingsOperator.Gameplaycfg.Meh, SettingsOperator.Gameplaycfg.Bad, comboPortion, scoreMult);
		if (!Finished) SettingsOperator.Gameplaycfg.pp = CurrentPerformance(false);
		Gameplay.score = score;
		if ((scoretween == null || !scoretween.IsRunning()) & scoreint != score)
		{
			scoretween?.Kill();
			scoretween = CreateTween();
			scoretween.TweenProperty(this, "scoreint", score, 0.3f);
			scoretween.Play();
		}
		SettingsOperator.Gameplaycfg.Score = scoreint;
		Cursor.CursorVisible = true;

		try
		{
			gametime = GetRemainingTime((float)delta) / 0.001f;

			if (HealthBar.Health == 0 && !Dead && !ModsOperator.Mods["no-fail"] && !LanNetwork.InMatch) // LAN matches are no-fail
			{
				Dead = true;
				Gameplay.Dead = true;
				FailAnimation();
			}

			if (songstarted && !Finished && !Dead && gametime > LastEndTime + 1000 && Objects.All(o => o.Judged))
			{
				Finished = true;
				SettingsOperator.Gameplaycfg.pp = CurrentPerformance(true);
				score = ScoreCalc.ProcessScore(SettingsOperator.Gameplaycfg.NoteCount, SettingsOperator.Gameplaycfg.Max,
					SettingsOperator.Gameplaycfg.Great, SettingsOperator.Gameplaycfg.Meh, comboPortion, scoreMult);
				Replay.SaveReplay();
				ApiOperator.SS();
				if (!SettingsOperator.Marathon)
				{
					AddChild(GD.Load<PackedScene>("res://Panels/Screens/EndScreen.tscn").Instantiate());
				}
				else
				{
					SettingsOperator.MarathonID++;
					if (SettingsOperator.MarathonID >= SettingsOperator.MarathonMapPaths.Count - 1) GetNode<SceneTransition>("/root/Transition").Switch("res://Panels/Screens/ResultScreenv2.tscn");
					SettingsOperator.SelectSongID(SettingsOperator.MarathonMapPaths[SettingsOperator.MarathonID]);
					GetNode<SceneTransition>("/root/Transition").Switch("res://Panels/Screens/SongLoadingScreen.tscn");
				}
			}

			if (SettingsOperator.Gameplaycfg.Combo > SettingsOperator.Gameplaycfg.MaxCombo)
				SettingsOperator.Gameplaycfg.MaxCombo = SettingsOperator.Gameplaycfg.Combo;

			if (!BreakTime) Beatmap_Background.SelfModulate = new Color(dim, dim, dim);
			SettingsOperator.Gameplaycfg.Accuracy = ReloadAccuracy(SettingsOperator.Gameplaycfg.Max, SettingsOperator.Gameplaycfg.Great, SettingsOperator.Gameplaycfg.Meh, SettingsOperator.Gameplaycfg.Bad);

			if (Input.IsActionJustPressed("pausemenu") && SettingsOperator.SpectatorMode)
			{
				BeatmapBackground.FlashEnable = true;
				SettingsOperator.toppaneltoggle(true);
				GetNode<SceneTransition>("/root/Transition").Switch("res://Panels/Screens/song_select.tscn");
			}
			else if (Input.IsActionJustPressed("pausemenu") && LanNetwork.InMatch)
			{
				// Can't pause a LAN match (the others keep playing): Esc forfeits back to the room.
				LanNetwork.Instance.ReportFinished(quit: true);
				BeatmapBackground.FlashEnable = true;
				SettingsOperator.toppaneltoggle(true);
				AudioPlayer.Instance.PitchScale = speedold;
				GetNode<SceneTransition>("/root/Transition").Switch(LanNetwork.Instance.ReturnScene);
			}
			else if (Input.IsActionJustPressed("pausemenu") && !SettingsOperator.Marathon)
			{
				ShowPauseMenu();
			}
			else if (Input.IsActionJustPressed("pausemenu") && SettingsOperator.Marathon)
			{
				SettingsOperator.toppaneltoggle(true);
				BeatmapBackground.FlashEnable = true;
				GetNode<SceneTransition>("/root/Transition").Switch("res://Panels/Screens/MarathonMode.tscn");
				SettingsOperator.Marathon = false;
			}
			else if (Input.IsActionJustPressed("retry") && !SettingsOperator.Marathon && !LanNetwork.InMatch)
			{
				BeatmapBackground.FlashEnable = true;
				SettingsOperator.toppaneltoggle(true);
				MainScreenAnimation?.Kill();
				AudioPlayer.Instance.PitchScale = speedold;
				GetNode<SceneTransition>("/root/Transition").Switch("res://Panels/Screens/SongLoadingScreen.tscn");
			}
			else if (Input.IsActionJustPressed("HideHUD"))
			{
				SettingsOperator.SetSetting("hidehud", !HideHUD);
				HideHUD = !HideHUD;
				HUDAni(HideHUD);
			}

			if (dance != null && dance.Count > 0)
			{
				if ((int)gametime >= dance[DanceIndex].time + BeatmapBackground.bpm && BeatmapBackground.FlashEnable)
				{
					Transitioning(dance[DanceIndex].flash);
					IncreaseDanceIndex();
				}
				else if ((int)gametime >= dance[DanceIndex].time && !BeatmapBackground.FlashEnable)
				{
					Transitioning(dance[DanceIndex].flash);
					IncreaseDanceIndex();
				}
			}

			if (!Dead) GameTick(delta);
			if (LanNetwork.InMatch) LanNetwork.Instance.SpecTime(gametime); // spectators follow this clock
		}
		catch (Exception e)
		{
			if (!erroredout)
			{
				erroredout = true;
				GD.PrintErr(e);
				GD.PushError(e);
				Notify.Post("Can't play the beatmap because\n" + e.Message, Type: NotificationIcons.NotificationType.Warning);
				SettingsOperator.toppaneltoggle(true);
				BeatmapBackground.FlashEnable = true;
				GetNode<SceneTransition>("/root/Transition").Switch("res://Panels/Screens/song_select.tscn");
			}
		}
	}

	// ───────────────────────────── Drawing ─────────────────────────────

	private Vector2 ToScreen(Vector2 p) => FieldOffset + p * FieldScale;
	private Vector2 FromScreen(Vector2 s) => (s - FieldOffset) / FieldScale;

	private void UpdateFieldTransform(Vector2 size)
	{
		FieldScale = Math.Min(size.X / 640f, size.Y / 480f);
		FieldOffset = new Vector2((size.X - FieldSize.X * FieldScale) / 2f, (size.Y - FieldSize.Y * FieldScale) / 2f + 8f * FieldScale);
	}

	public void DrawField(Control c)
	{
		UpdateFieldTransform(c.Size);
		float now = gametime;
		float r = Radius * FieldScale;
		var font = ThemeDB.FallbackFont;
		SweepSliderBodies(now);

		// Find visible range then draw back-to-front so earlier objects sit on top
		int last = FirstActive;
		while (last < Objects.Count && Objects[last].Time - Preempt <= now) last++;
		for (int i = last - 1; i >= FirstActive; i--)
		{
			var o = Objects[i];
			float fadeOut = FadeOutMs(o);
			if (o.Judged && now - o.JudgedAt > fadeOut) continue;
			float appear = o.Time - Preempt;
			float alpha = Mathf.Clamp((now - appear) / FadeIn, 0f, 1f);
			if (o.Judged) alpha *= 1f - Mathf.Clamp((now - o.JudgedAt) / fadeOut, 0f, 1f);
			var col = ComboColours[o.ComboColour % ComboColours.Count];

			switch (o.Type)
			{
				case TapObjectType.Spinner:
					DrawSpinner(c, o, now, alpha, font);
					break;

				case TapObjectType.Slider:
					DrawSliderBody(c, o, now, alpha, r, col);
					if (!o.HeadDone) DrawCircleObject(c, o, now, alpha, r, col, font);
					if (now >= o.Time && !o.Judged)
					{
						var ball = ToScreen(o.SliderBallAt(now));
						c.DrawCircle(ball, r * 0.85f, col.Lightened(0.25f));
						c.DrawArc(ball, r * 0.85f, 0, Mathf.Tau, 48, Colors.White, Math.Max(2f, r * 0.1f), true);
						if (o.Tracking)
							c.DrawArc(ball, r * 2.4f, 0, Mathf.Tau, 64, new Color(1, 1, 1, 0.6f), Math.Max(2f, r * 0.08f), true);
					}
					break;

				default:
					DrawCircleObject(c, o, now, alpha, r, col, font);
					break;
			}
		}

		// Judgement popups
		foreach (var (pos, tex, time) in Popups)
		{
			if (tex == null) continue;
			float t = Mathf.Clamp((now - time) / 700f, 0f, 1f);
			float h = r * 0.45f;
			float w = h * tex.GetWidth() / Math.Max(1, tex.GetHeight());
			var sp = ToScreen(pos) + new Vector2(0, -r * 0.2f - t * r * 0.4f);
			c.DrawTextureRect(tex, new Rect2(sp - new Vector2(w, h) / 2f, new Vector2(w, h)), false, new Color(1, 1, 1, 1f - t * t));
		}

		// Auto cursor
		if (Auto)
		{
			var cp = ToScreen(CursorPos);
			c.DrawCircle(cp, 10f * FieldScale, new Color(1f, 0.85f, 0.4f, 0.9f));
			c.DrawArc(cp, 10f * FieldScale, 0, Mathf.Tau, 24, Colors.White, 2f, true);
		}
	}

	private void DrawCircleObject(Control c, TapObject o, float now, float alpha, float r, Color col, Font font)
	{
		var pos = ToScreen(o.Pos);
		float shake = now - o.ShakeAt < 200 ? Mathf.Sin((now - o.ShakeAt) * 0.12f) * 6f * FieldScale * (1f - (now - o.ShakeAt) / 200f) : 0f;
		pos.X += shake;
		c.DrawCircle(pos, r, new Color(col.R * 0.85f, col.G * 0.85f, col.B * 0.85f, alpha));
		c.DrawArc(pos, r, 0, Mathf.Tau, 64, new Color(1, 1, 1, alpha), Math.Max(2f, r * 0.12f), true);
		int fs = Math.Max(10, (int)(r * 0.9f));
		c.DrawString(font, pos + new Vector2(-r, fs * 0.35f), o.ComboNumber.ToString(), HorizontalAlignment.Center, r * 2f, fs, new Color(1, 1, 1, alpha));

		if (now < o.Time && !o.Judged)
		{
			float prog = Mathf.Clamp((o.Time - now) / Preempt, 0f, 1f);
			float ar = r * (1f + 3f * prog);
			c.DrawArc(pos, ar, 0, Mathf.Tau, 64, new Color(col.R, col.G, col.B, alpha), Math.Max(2f, r * 0.08f), true);
		}
	}

	private const float SliderFadeOutMs = 200f;
	private static float FadeOutMs(TapObject o) => o.Type == TapObjectType.Slider ? SliderFadeOutMs : 150f;
	private readonly List<TapObject> ActiveBodies = new();

	/// <summary>Frees slider body nodes once their fade-out has finished.</summary>
	private void SweepSliderBodies(float now)
	{
		for (int i = ActiveBodies.Count - 1; i >= 0; i--)
		{
			var o = ActiveBodies[i];
			if (!o.Judged || now - o.JudgedAt <= SliderFadeOutMs) continue;
			if (IsInstanceValid(o.BodyGroup)) o.BodyGroup.QueueFree();
			o.BodyGroup = null;
			ActiveBodies.RemoveAt(i);
		}
	}

	private static Line2D MakeBodyLine(Color colour) => new Line2D
	{
		DefaultColor = colour,
		JointMode = Line2D.LineJointMode.Round,
		BeginCapMode = Line2D.LineCapMode.Round,
		EndCapMode = Line2D.LineCapMode.Round,
		Antialiased = true,
	};

	private void DrawSliderBody(Control c, TapObject o, float now, float alpha, float r, Color col)
	{
		if (o.ScreenPath == null || !Mathf.IsEqualApprox(o.ScreenPathScale, FieldScale))
		{
			// Screen-space path with near-duplicate points dropped (they make Line2D joints glitch)
			var list = new List<Vector2>();
			foreach (var p in o.Path)
			{
				var sp = ToScreen(p);
				if (list.Count == 0 || sp.DistanceTo(list[^1]) >= 1.5f) list.Add(sp);
			}
			var lastPt = ToScreen(o.Path[^1]);
			if (list[^1] != lastPt) list.Add(lastPt);
			if (list.Count < 2) list.Add(list[0] + new Vector2(0.5f, 0));
			o.ScreenPath = list.ToArray();
			o.ScreenPathScale = FieldScale;
			o.BodyShownCount = -1;
		}
		var pts = o.ScreenPath;

		if (o.BodyGroup == null && Field?.Bodies != null)
		{
			o.BodyGroup = new CanvasGroup();
			o.BodyBorder = MakeBodyLine(Colors.White);
			o.BodyFill = MakeBodyLine(new Color(col.R * 0.35f, col.G * 0.35f, col.B * 0.35f));
			o.BodyGroup.AddChild(o.BodyBorder);
			o.BodyGroup.AddChild(o.BodyFill);
			Field.Bodies.AddChild(o.BodyGroup);
			Field.Bodies.MoveChild(o.BodyGroup, 0); // later sliders sit underneath earlier ones
			ActiveBodies.Add(o);
		}
		if (o.BodyGroup == null) return;

		// Snake the body in over the first third of the approach
		float snake = Mathf.Clamp((now - (o.Time - Preempt)) / (Preempt / 3f), 0f, 1f);
		int count = Math.Max(2, (int)Math.Ceiling(pts.Length * snake));
		if (count != o.BodyShownCount)
		{
			var shown = count >= pts.Length ? pts : pts[..count];
			o.BodyBorder.Points = shown;
			o.BodyFill.Points = shown;
			o.BodyShownCount = count;
		}
		o.BodyBorder.Width = r * 2f;
		o.BodyFill.Width = r * 1.76f;
		o.BodyGroup.SelfModulate = new Color(1, 1, 1, alpha);

		// Reverse arrow indicator on the end that will bounce next
		if (o.Slides > 1 && !o.Judged && snake >= 1f)
		{
			float slideDur = o.Duration / o.Slides;
			int span = now < o.Time ? 0 : Math.Min(o.Slides - 1, (int)((now - o.Time) / slideDur));
			if (span < o.Slides - 1)
			{
				bool atEnd = span % 2 == 0;
				var tip = atEnd ? pts[^1] : pts[0];
				var back = atEnd ? pts[Math.Max(0, pts.Length - 6)] : pts[Math.Min(pts.Length - 1, 5)];
				var dir = (back - tip).Normalized();
				var nrm = new Vector2(-dir.Y, dir.X);
				var a = tip + dir * r * 0.5f;
				var tri = new[] { a, tip - dir * r * 0.15f + nrm * r * 0.4f, tip - dir * r * 0.15f - nrm * r * 0.4f };
				c.DrawColoredPolygon(new[] { tri[0], tri[1], tri[2] }, new Color(1, 1, 1, alpha));
			}
		}
	}

	private void DrawSpinner(Control c, TapObject o, float now, float alpha, Font font)
	{
		var center = ToScreen(SpinnerCenter);
		float maxR = 180f * FieldScale;
		float progress = Mathf.Clamp(Math.Abs(o.SpinTotal) / Mathf.Tau / o.SpinRequired, 0f, 1f);
		float timeLeft = Mathf.Clamp((o.EndTime - now) / o.Duration, 0f, 1f);
		c.DrawCircle(center, maxR, new Color(0, 0, 0, 0.45f * alpha));
		c.DrawArc(center, maxR * Math.Max(0.05f, timeLeft), 0, Mathf.Tau, 96, new Color(1, 1, 1, 0.8f * alpha), 3f, true);
		var fill = progress >= 1f ? new Color(0.5f, 1f, 0.6f, alpha) : new Color(0.6f, 0.8f, 1f, alpha);
		c.DrawArc(center, maxR * 0.35f, -Mathf.Pi / 2f, -Mathf.Pi / 2f + Mathf.Tau * progress, 96, fill, 10f * FieldScale, true);
		float spin = o.SpinTotal;
		c.DrawLine(center, center + new Vector2(Mathf.Cos(spin), Mathf.Sin(spin)) * maxR * 0.3f, new Color(1, 1, 1, alpha), 4f, true);
		int fs = Math.Max(12, (int)(28 * FieldScale));
		string label = now < o.Time ? "SPIN!" : $"{Math.Abs(o.SpinTotal) / Mathf.Tau:0.0} / {o.SpinRequired:0.0}";
		c.DrawString(font, center + new Vector2(-maxR, maxR * 0.6f), label, HorizontalAlignment.Center, maxR * 2f, fs, new Color(1, 1, 1, alpha));
	}

	// ───────────────────────────── Misc (mirrors the other cores) ─────────────────────────────

	private void ShowPauseMenu()
	{
		PauseMenu = GD.Load<PackedScene>("res://Panels/Screens/PauseMenu.tscn").Instantiate().GetNode<Control>(".");
		GetTree().Root.AddChild(PauseMenu);
	}

	private void FailAnimation()
	{
		var Interval = 1f;
		MainScreenAnimation?.Kill();
		MainScreenAnimation = CreateTween();
		PivotOffset = new Vector2(Size.X / 2, Size.Y / 2);
		MainScreenAnimation.TweenInterval(0.5f);
		MainScreenAnimation.Parallel().TweenProperty(this, "modulate", new Color(1f, 0.8f, 0.8f, 1f), Interval).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		MainScreenAnimation.Parallel().TweenProperty(this, "position", new Vector2(Position.X, Position.Y + 20), Interval).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		MainScreenAnimation.Parallel().TweenProperty(this, "rotation", 0.05f, Interval).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		MainScreenAnimation.Parallel().TweenProperty(AudioPlayer.Instance, "pitch_scale", 0.01f, Interval).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		MainScreenAnimation.Parallel().TweenProperty(this, "scale", Scale * 0.7f, Interval).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		MainScreenAnimation.Connect("finished", new Callable(this, nameof(ShowPauseMenu)));
	}

	private void BreakNow(int untilMs)
	{
		BreakTime = true;
		BreakBGTween?.Kill();
		BreakBGTween = GetTree().CreateTween();
		BreakBGTween.SetTrans(Tween.TransitionType.Cubic);
		BreakBGTween.SetEase(Tween.EaseType.Out);
		BreakBGTween.TweenProperty(Beatmap_Background, "self_modulate", new Color(1f, 1f, 1f, 1f), 0.5f);
		if (IsInstanceValid(Break)) Break?.QueueFree();
		Break = GD.Load<PackedScene>("res://Panels/Screens/Break.tscn").Instantiate().GetNode<Break>(".");
		Break.MaxTick = untilMs * 0.001;
		AddChild(Break);
		Break.BreakFinished += BreakOver;
	}

	private void BreakOver()
	{
		BreakBGTween?.Kill();
		BreakBGTween = GetTree().CreateTween();
		BreakBGTween.SetTrans(Tween.TransitionType.Cubic);
		BreakBGTween.SetEase(Tween.EaseType.Out);
		BreakBGTween.TweenProperty(Beatmap_Background, "self_modulate", new Color(dim, dim, dim, 1f), 0.5f);
		BreakBGTween.TweenCallback(Callable.From(() => { BreakTime = false; }));
	}

	private void HUDAni(bool value)
	{
		HUDTween?.Kill();
		HUDTween = CreateTween();
		HUDTween.SetEase(Tween.EaseType.Out);
		HUDTween.SetTrans(Tween.TransitionType.Cubic);
		if (value)
		{
			HUDTween.TweenProperty(HUD, "modulate:a", 0f, 0.5f);
			HUDTween.TweenCallback(Callable.From(() => HUD.Hide()));
		}
		else
		{
			HUDTween.TweenProperty(HUD, "modulate:a", 0f, 0f);
			HUDTween.TweenCallback(Callable.From(() => HUD.Show()));
			HUDTween.TweenProperty(HUD, "modulate:a", 1f, 0.5f);
		}
	}

	private void IncreaseDanceIndex()
	{
		if (DanceIndex + 1 < dance.Count) DanceIndex++;
	}

	private Tween DancePrepare { get; set; }
	private void Transitioning(bool value)
	{
		if (value)
		{
			DancePrepare?.Kill();
			DancePrepare = GetTree().CreateTween();
			DancePrepare.TweenProperty(GetTree().CurrentScene, "modulate", new Color(1f, 1f, 1f, 0.6f), BeatmapBackground.bpm)
				.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
			DancePrepare.TweenCallback(Callable.From(() => BeatmapBackground.FlashEnable = true));
			DancePrepare.TweenProperty(GetTree().CurrentScene, "modulate", new Color(1f, 1f, 1f, 1f), 0.5)
				.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
			DancePrepare.Play();
		}
		else BeatmapBackground.FlashEnable = value;
	}

	private void ComboAnimation()
	{
		if (ComboCounter == null) return;
		ComboCounter.Scale = new Vector2(1.0f, 1.2f);
		ComboTween?.Kill();
		ComboTween = CreateTween();
		ComboTween.TweenProperty(ComboCounter, "scale", new Vector2(1f, 1f), 0.5f)
			.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		ComboTween.Play();
	}
}
