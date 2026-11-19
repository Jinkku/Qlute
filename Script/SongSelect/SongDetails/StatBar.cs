using System;
using Godot;

/// <summary>
/// Rounded meter for song select's Details tab: a dim track with a fill that glides to its value.
/// The fill is shaded along the whole bar by a colour function (e.g. the level colours), or flat.
/// </summary>
public partial class StatBar : Control
{
	[Export] public float MaxValue { get; set; } = 10;
	[Export] public Color TrackColor { get; set; } = new(1, 1, 1, 0.08f);
	/// <summary>Seconds (roughly) to glide to a new value.</summary>
	[Export] public float GlideTime { get; set; } = 0.25f;

	private float _target, _shown;
	private Func<float, Color> _colour = _ => Colors.White;

	/// <param name="colourAt">Fill colour at a point of the full bar (0 = left end, 1 = right end).</param>
	public void Set(float value, Func<float, Color> colourAt)
	{
		_target = Mathf.Clamp(value / Mathf.Max(0.0001f, MaxValue), 0, 1);
		_colour = colourAt;
		SetProcess(true);
		QueueRedraw();
	}

	public void Set(float value, Color fill) => Set(value, _ => fill);

	public override void _Ready() => SetProcess(false);

	public override void _Process(double delta)
	{
		_shown = Mathf.Lerp(_shown, _target, 1 - Mathf.Exp(-(float)delta * 4 / Mathf.Max(0.01f, GlideTime)));
		if (Mathf.Abs(_shown - _target) < 0.001f) { _shown = _target; SetProcess(false); }
		QueueRedraw();
	}

	public override void _Draw()
	{
		DrawPill(Size.X, _ => TrackColor);
		if (_shown <= 0) return;
		DrawPill(Mathf.Max(Size.Y, Size.X * _shown), x => _colour(x / Mathf.Max(1, Size.X)));
	}

	/// <summary>A pill from x = 0 to <paramref name="width"/> (half-circle ends), drawn as thin vertical slices so the colour can change along it.</summary>
	private void DrawPill(float width, Func<float, Color> colourAtX)
	{
		float h = Size.Y, r = Mathf.Min(h / 2, width / 2), cy = h / 2;
		float Half(float x) // half height of the pill at x
		{
			float d = x < r ? r - x : x > width - r ? x - (width - r) : 0;
			return Mathf.Sqrt(Mathf.Max(0, r * r - d * d)) * (h / 2) / Mathf.Max(0.001f, r);
		}
		int n = Mathf.Max(2, (int)(width / 4));
		var pts = new Vector2[4];
		var cols = new Color[4];
		for (int i = 0; i < n; i++)
		{
			float x0 = width * i / n, x1 = width * (i + 1) / n;
			float h0 = Mathf.Max(0.05f, Half(x0)), h1 = Mathf.Max(0.05f, Half(x1)); // never 0: a 3-point quad fails to triangulate
			var c0 = colourAtX(x0); var c1 = colourAtX(x1);
			pts[0] = new(x0, cy - h0); pts[1] = new(x1, cy - h1); pts[2] = new(x1, cy + h1); pts[3] = new(x0, cy + h0);
			cols[0] = c0; cols[1] = c1; cols[2] = c1; cols[3] = c0;
			DrawPolygon(pts, cols);
		}
	}
}
