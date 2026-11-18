using Godot;

/// <summary>Ten segments filled up to the level (each = 10 levels), in the level's colour (DifficultyCard).</summary>
public partial class LevelMeter : Control
{
	[Export] public int Segments { get; set; } = 10;
	[Export] public float SegmentSize { get; set; } = 8;
	[Export] public float SegmentGap { get; set; } = 3;
	[Export] public Color EmptyColor { get; set; } = new(1, 1, 1, 0.15f);

	private float _level;
	private Color _c;

	public void Set(float level, Color c) { _level = level; _c = c; QueueRedraw(); }

	public override void _Draw()
	{
		float per = 100f / Mathf.Max(1, Segments);
		float y = (Size.Y - SegmentSize) / 2;
		for (int i = 0; i < Segments; i++)
		{
			float fill = Mathf.Clamp((_level - i * per) / per, 0, 1);
			var r = new Rect2(i * (SegmentSize + SegmentGap), y, SegmentSize, SegmentSize);
			DrawRect(r, EmptyColor);
			if (fill > 0) DrawRect(new Rect2(r.Position, new Vector2(SegmentSize * fill, SegmentSize)), _c);
		}
	}
}
