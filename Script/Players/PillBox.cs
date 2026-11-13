using Godot;
using System.Collections.Generic;

/// <summary>
/// A pill (fully rounded ends) with a thin border drawn as one continuous anti-aliased outline.
/// StyleBoxFlat draws a translucent 1px border as separate straight and curved pieces: the curves come out
/// dimmer than the straight edges and the joins overlap into brighter spots. This keeps the line even all the way
/// round, like the website's CSS pills. Optional soft glow (box-shadow) behind it.
/// </summary>
public partial class PillBox : StyleBox
{
	public Color Bg = Colors.Transparent;
	public Color Border = Colors.Transparent;
	public float BorderWidth = 1;
	public Color GlowColor = Colors.Transparent;
	public int GlowSize;

	private StyleBoxFlat _glow;

	public PillBox() { }

	public PillBox(Color bg, Color border, float padX, float padY, float borderWidth = 1)
	{
		Bg = bg;
		Border = border;
		BorderWidth = borderWidth;
		ContentMarginLeft = ContentMarginRight = padX + borderWidth;
		ContentMarginTop = ContentMarginBottom = padY + borderWidth;
	}

	public override void _Draw(Rid canvasItem, Rect2 rect)
	{
		if (GlowSize > 0 && GlowColor.A > 0)
		{
			// a StyleBoxFlat's shadow is soft and round already; only its shadow is drawn
			_glow ??= MakeGlow();
			_glow.Draw(canvasItem, rect);
		}
		var fill = Outline(rect, 0);
		if (Bg.A > 0)
		{
			RenderingServer.CanvasItemAddPolygon(canvasItem, fill, new[] { Bg });
			// the polygon itself isn't anti-aliased: soften its edge with a hairline in the fill colour
			if (Border.A <= 0) RenderingServer.CanvasItemAddPolyline(canvasItem, Closed(fill), new[] { Bg }, 1, true);
		}
		if (Border.A > 0 && BorderWidth > 0)
			RenderingServer.CanvasItemAddPolyline(canvasItem, Closed(Outline(rect, BorderWidth / 2)), new[] { Border }, BorderWidth, true);
	}

	private StyleBoxFlat MakeGlow()
	{
		var s = new StyleBoxFlat { BgColor = Colors.Transparent, DrawCenter = false, ShadowColor = GlowColor, ShadowSize = GlowSize, CornerDetail = 24 };
		s.SetCornerRadiusAll(999);
		return s;
	}

	/// <summary>Points round the pill, inset by <paramref name="inset"/>.</summary>
	private static Vector2[] Outline(Rect2 rect, float inset)
	{
		var r = rect.Grow(-inset);
		float radius = Mathf.Max(0.5f, Mathf.Min(r.Size.X, r.Size.Y) / 2);
		int steps = Mathf.Clamp(Mathf.CeilToInt(radius * 1.6f), 10, 48); // per half circle
		var left = new Vector2(r.Position.X + radius, r.Position.Y + r.Size.Y / 2);
		var right = new Vector2(r.End.X - radius, left.Y);
		var pts = new List<Vector2>(steps * 2 + 2);
		for (int i = 0; i <= steps; i++) // right end, top to bottom
		{
			var a = -Mathf.Pi / 2 + Mathf.Pi * i / steps;
			pts.Add(right + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius);
		}
		for (int i = 0; i <= steps; i++) // left end, bottom to top
		{
			var a = Mathf.Pi / 2 + Mathf.Pi * i / steps;
			pts.Add(left + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius);
		}
		// A circle (no straight part) gives the same point twice where the arcs meet; duplicates break the fill.
		var clean = new List<Vector2>(pts.Count);
		foreach (var pt in pts)
			if (clean.Count == 0 || clean[^1].DistanceSquaredTo(pt) > 0.0001f) clean.Add(pt);
		if (clean.Count > 1 && clean[^1].DistanceSquaredTo(clean[0]) <= 0.0001f) clean.RemoveAt(clean.Count - 1);
		return clean.ToArray();
	}

	private static Vector2[] Closed(Vector2[] pts)
	{
		var c = new Vector2[pts.Length + 1];
		pts.CopyTo(c, 0);
		c[^1] = pts[0];
		return c;
	}
}
