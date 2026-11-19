using System.Collections.Generic;
using Godot;

/// <summary>
/// Song select's Details tab: notes per second over the length of the map as a soft area chart,
/// in the difficulty's level colour. The part of the song the preview has already played is lit up.
/// </summary>
public partial class DensityGraph : Control
{
	/// <summary>Width of one column of the chart in pixels.</summary>
	[Export] public float ColumnWidth { get; set; } = 6;
	[Export] public Color LineColor { get; set; } = new(1, 1, 1, 0.9f);
	[Export(PropertyHint.Range, "0,1")] public float PlayedAlpha { get; set; } = 0.8f;
	[Export(PropertyHint.Range, "0,1")] public float UnplayedAlpha { get; set; } = 0.22f;
	[Export] public Color PlayheadColor { get; set; } = new(1, 1, 1, 0.8f);

	private readonly List<int> _times = new();
	private float _length = 1; // ms
	private float[] _heights = System.Array.Empty<float>(); // 0..1 per column
	private Color _colour = Colors.White;
	private float _playX = -1;

	/// <summary>The map's note times (ms from the start of the audio) and the time of its last note.</summary>
	public void Set(IEnumerable<int> times, float lengthMs, Color colour)
	{
		_times.Clear();
		_times.AddRange(times);
		_length = Mathf.Max(1, lengthMs);
		_colour = colour;
		Rebuild();
	}

	public override void _Notification(int what)
	{
		if (what == NotificationResized) Rebuild();
	}

	private void Rebuild()
	{
		int n = Mathf.Max(8, (int)(Size.X / Mathf.Max(1, ColumnWidth)));
		var counts = new float[n];
		foreach (var t in _times)
		{
			int i = (int)(t / _length * n);
			if (i >= 0 && i < n) counts[i]++;
			else if (i == n) counts[n - 1]++;
		}
		// a light blur so single busy columns don't spike
		var smooth = new float[n];
		for (int i = 0; i < n; i++)
		{
			float sum = 0, w = 0;
			for (int k = -2; k <= 2; k++)
			{
				int j = i + k;
				if (j < 0 || j >= n) continue;
				float kw = 3 - Mathf.Abs(k);
				sum += counts[j] * kw; w += kw;
			}
			smooth[i] = sum / w;
		}
		float max = 0;
		foreach (var v in smooth) max = Mathf.Max(max, v);
		_heights = new float[n];
		for (int i = 0; i < n; i++) _heights[i] = max > 0 ? smooth[i] / max : 0;
		QueueRedraw();
	}

	public override void _Process(double delta)
	{
		if (!IsVisibleInTree()) return;
		float x = -1;
		var player = AudioPlayer.Instance;
		if (player != null && player.Playing && _times.Count > 0)
			x = Mathf.Clamp(player.GetPlaybackPosition() * 1000f / _length, 0, 1) * Size.X;
		if (Mathf.Abs(x - _playX) < 0.5f) return;
		_playX = x;
		QueueRedraw();
	}

	public override void _Draw()
	{
		int n = _heights.Length;
		if (n < 2) return;
		float w = Size.X, h = Size.Y, top = 2; // room for the line's thickness
		Vector2 At(int i) => new(w * i / (n - 1), h - _heights[i] * (h - top));

		// baseline
		DrawLine(new Vector2(0, h - 0.5f), new Vector2(w, h - 0.5f), _colour with { A = 0.25f }, 1);

		var pts = new Vector2[4];
		var cols = new Color[4];
		for (int i = 0; i < n - 1; i++)
		{
			Vector2 a = At(i), b = At(i + 1);
			if (a.Y >= h - 0.01f && b.Y >= h - 0.01f) continue;
			float alpha = _playX >= 0 && b.X <= _playX + 0.01f ? PlayedAlpha : UnplayedAlpha;
			var topC = _colour with { A = alpha };
			var botC = _colour with { A = alpha * 0.12f };
			pts[0] = a; pts[1] = b; pts[2] = new(b.X, h); pts[3] = new(a.X, h);
			cols[0] = topC; cols[1] = topC; cols[2] = botC; cols[3] = botC;
			if (a.Y >= h - 0.01f) pts[0].Y = h - 0.05f; // keep 4 distinct points
			if (b.Y >= h - 0.01f) pts[1].Y = h - 0.05f;
			DrawPolygon(pts, cols);
		}

		var line = new Vector2[n];
		for (int i = 0; i < n; i++) line[i] = At(i);
		DrawPolyline(line, _colour.Lerp(LineColor, 0.35f), 2, true);

		if (_playX >= 0)
		{
			DrawLine(new Vector2(_playX, 0), new Vector2(_playX, h), PlayheadColor, 2);
			DrawCircle(new Vector2(_playX, 0), 3.5f, PlayheadColor);
		}
	}
}
