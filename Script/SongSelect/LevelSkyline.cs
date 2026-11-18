using Godot;
using System;
using System.Collections.Generic;

/// <summary>One bar per difficulty, height and colour by level: a set's difficulty spread at a glance (MusicCard).</summary>
public partial class LevelSkyline : Control
{
	[Export] public int MaxBars { get; set; } = 18;
	[Export] public float BarWidth { get; set; } = 7;
	[Export] public float BarGap { get; set; } = 3;
	[Export] public float MinHeight { get; set; } = 8;
	[Export] public float MaxHeight { get; set; } = 28;

	private List<float> _levels = new();
	public List<float> Levels
	{
		set
		{
			_levels = value;
			CustomMinimumSize = new Vector2(Math.Min(_levels.Count, MaxBars) * (BarWidth + BarGap), MaxHeight);
			QueueRedraw();
		}
	}

	public override void _Draw()
	{
		for (int i = 0; i < _levels.Count && i < MaxBars; i++)
		{
			float lv = Mathf.Clamp(_levels[i], 0, 100);
			float h = MinHeight + lv / 100f * (MaxHeight - MinHeight);
			var c = SettingsOperator.ReturnLevelColour((int)Math.Min(lv, 99));
			var rect = new Rect2(i * (BarWidth + BarGap), Size.Y - h, BarWidth, h);
			DrawRect(rect, c);
			DrawRect(new Rect2(rect.Position, new Vector2(BarWidth, 2)), c.Lightened(0.4f));
		}
	}
}
