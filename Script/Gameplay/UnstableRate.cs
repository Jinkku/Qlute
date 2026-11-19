using Godot;
using System.Collections.Generic;

/// <summary>
/// Hit error meter (Panels/GameplayElements/Customizable/unstablerate.tscn), drawn like osu!'s:
/// the Perfect / Great / Meh windows as coloured zones (early at the top, late at the bottom),
/// a tick for every hit in its judgement colour that fades out over a few seconds, a centre line,
/// and an arrow that glides to the running average so you can see if you're hitting early or late.
/// Game modes call <see cref="SetWindows"/> with their real judgement windows; hits arrive through
/// SettingsOperator.Addms (ms from the note, negative = early).
/// </summary>
public partial class UnstableRate : Control
{
	/// <summary>Hits waiting to be drawn (ms, judgement 0 Perfect / 1 Great / 2 Meh, -1 = work it out from the windows).</summary>
	public static readonly List<(float ms, int judge)> Rate = new();

	/// <summary>Edges of each judgement window in ms: early (negative) and late, Perfect, Great, Meh.</summary>
	private static readonly float[] Early = { -40, -80, -120 };
	private static readonly float[] Late = { 40, 80, 120 };

	/// <summary>Sets the meter's zones to a game mode's judgement windows (early edges negative).</summary>
	public static void SetWindows((float early, float late) perfect, (float early, float late) great, (float early, float late) meh)
	{
		(Early[0], Late[0]) = perfect;
		(Early[1], Late[1]) = great;
		(Early[2], Late[2]) = meh;
		Rate.Clear();
	}

	[Export] public Color PerfectColor { get; set; } = new("#66ccff");
	[Export] public Color GreatColor { get; set; } = new("#88d14f");
	[Export] public Color MehColor { get; set; } = new("#ffcc22");
	[Export(PropertyHint.Range, "0,1")] public float ZoneAlpha { get; set; } = 0.75f;
	/// <summary>How far ticks reach past each side of the bar.</summary>
	[Export] public float TickOverhang { get; set; } = 7;
	[Export] public float TickThickness { get; set; } = 2;
	/// <summary>Seconds a tick stays before it has faded out.</summary>
	[Export] public float TickLifetime { get; set; } = 6;
	[Export] public int MaxTicks { get; set; } = 60;
	[Export] public float ArrowSize { get; set; } = 9;
	[Export] public Color ArrowColor { get; set; } = Colors.White;
	/// <summary>Weight of each new hit in the average the arrow points at (osu! uses 0.1).</summary>
	[Export(PropertyHint.Range, "0.01,1")] public float AverageWeight { get; set; } = 0.1f;

	private struct Tick { public float Ms; public int Judge; public float Age; }
	private readonly List<Tick> _ticks = new();
	private float _average, _arrowMs;
	private bool _hasAverage;

	public override void _Ready()
	{
		Rate.Clear();
		ClipContents = false;
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		bool dirty = false;
		if (Rate.Count > 0)
		{
			foreach (var (ms, judge) in Rate)
			{
				_ticks.Add(new Tick { Ms = ms, Judge = judge >= 0 && judge <= 2 ? judge : JudgeOf(ms), Age = 0 });
				_average = _hasAverage ? _average * (1 - AverageWeight) + ms * AverageWeight : ms;
				_hasAverage = true;
			}
			Rate.Clear();
			if (_ticks.Count > MaxTicks) _ticks.RemoveRange(0, _ticks.Count - MaxTicks);
			dirty = true;
		}
		for (int i = _ticks.Count - 1; i >= 0; i--)
		{
			var t = _ticks[i];
			t.Age += dt;
			if (t.Age >= TickLifetime) { _ticks.RemoveAt(i); continue; }
			_ticks[i] = t;
			dirty = true;
		}
		if (_hasAverage && Mathf.Abs(_arrowMs - _average) > 0.01f)
		{
			_arrowMs = Mathf.Lerp(_arrowMs, _average, 1 - Mathf.Exp(-dt * 6)); // ~osu!'s 800 ms OutQuint glide
			dirty = true;
		}
		if (dirty) QueueRedraw();
	}

	private static int JudgeOf(float ms)
	{
		for (int i = 0; i < 3; i++)
			if (ms > Early[i] && ms < Late[i]) return i;
		return 2;
	}

	private Color ColorOf(int judge) => judge switch { 0 => PerfectColor, 1 => GreatColor, _ => MehColor };

	/// <summary>Half the bar's length covers the widest Meh edge.</summary>
	private float Range => Mathf.Max(1, Mathf.Max(-Early[2], Late[2]));

	private float YOf(float ms) => Size.Y / 2 + Mathf.Clamp(ms / Range, -1, 1) * Size.Y / 2;

	public override void _Draw()
	{
		float w = Size.X;
		// zones, widest first
		for (int i = 2; i >= 0; i--)
		{
			float top = YOf(Early[i]), bottom = YOf(Late[i]);
			var c = ColorOf(i) with { A = ZoneAlpha };
			DrawRect(new Rect2(0, top, w, bottom - top), c);
			if (i == 2) // round the ends of the bar
			{
				DrawCircle(new Vector2(w / 2, top), w / 2, c);
				DrawCircle(new Vector2(w / 2, bottom), w / 2, c);
			}
		}

		// centre line
		DrawRect(new Rect2(-TickOverhang * 0.6f, Size.Y / 2 - 1, w + TickOverhang * 1.2f, 2), new Color(1, 1, 1, 0.9f));

		// ticks: newest on top, fading out with age; a fresh tick starts a little wider
		foreach (var t in _ticks)
		{
			float life = t.Age / TickLifetime;
			float alpha = 0.85f * (1 - life * life);
			float pop = 1 + 0.6f * Mathf.Max(0, 1 - t.Age / 0.15f);
			float over = TickOverhang * pop;
			var c = ColorOf(t.Judge).Lerp(Colors.White, 0.35f) with { A = alpha };
			DrawRect(new Rect2(-over, YOf(t.Ms) - TickThickness / 2, w + over * 2, TickThickness), c);
		}

		// average arrow, left of the bar, pointing at it
		if (_hasAverage)
		{
			float y = YOf(_arrowMs), x = -TickOverhang - 3;
			DrawColoredPolygon(new[]
			{
				new Vector2(x, y),
				new Vector2(x - ArrowSize, y - ArrowSize * 0.7f),
				new Vector2(x - ArrowSize, y + ArrowSize * 0.7f),
			}, ArrowColor);
		}
	}
}
