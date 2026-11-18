using Godot;
using System;

/// <summary>
/// Two-handle level range (0 .. 100+) drawn over Qlute's level colour scale. The part outside the range is dimmed.
/// <see cref="RangeChanged"/> fires when a handle is let go (filtering every frame while dragging would be wasteful).
/// </summary>
public partial class LevelRangeSlider : Control
{
	public event Action<int, int> RangeChanged;
	/// <summary>Every move while dragging (for labels outside the bar).</summary>
	public event Action<int, int> Moving;
	/// <summary>Draw "Lv n" under the handles. Off when the values are shown elsewhere.</summary>
	[Export] public bool Labels { get; set; } = true;
	public int Min { get; private set; }
	public int Max { get; private set; } = 100;

	private const float BarHeight = 14, Handle = 13, Pad = 16;
	private int _drag = -1; // 0 = min handle, 1 = max handle
	private bool _hover;

	public LevelRangeSlider()
	{
		CustomMinimumSize = new Vector2(240, 44);
		MouseFilter = MouseFilterEnum.Stop;
		MouseDefaultCursorShape = CursorShape.PointingHand;
		TooltipText = "Only show difficulties in this level range (drag the ends)";
		MouseEntered += () => { _hover = true; QueueRedraw(); };
		MouseExited += () => { _hover = false; QueueRedraw(); };
	}

	public void SetRange(int min, int max)
	{
		Min = Math.Clamp(min, 0, 100);
		Max = Math.Clamp(max, Min, 100);
		QueueRedraw();
	}

	private float Left => Pad;
	private float Right => Size.X - Pad;
	private float BarY => Labels ? Size.Y * 0.38f : Size.Y * 0.5f;
	private float XOf(int level) => Mathf.Lerp(Left, Right, level / 100f);
	private int LevelAt(float x) => (int)Mathf.Round(Mathf.Clamp((x - Left) / Mathf.Max(1, Right - Left), 0, 1) * 100);

	public override void _Draw()
	{
		var font = GetThemeDefaultFont();
		float y = BarY, h = BarHeight;
		// colour scale in 50 steps, rounded ends drawn as circles
		const int steps = 50;
		for (int i = 0; i < steps; i++)
		{
			float x0 = Mathf.Lerp(Left, Right, i / (float)steps), x1 = Mathf.Lerp(Left, Right, (i + 1) / (float)steps);
			int level = (int)(i * 100f / steps);
			bool inside = level >= Min && level <= Max;
			var c = SettingsOperator.ReturnLevelColour(Math.Min(level, 99));
			DrawRect(new Rect2(x0, y - h / 2, x1 - x0 + 0.5f, h), inside ? c : c.Darkened(0.7f));
		}
		DrawCircle(new Vector2(Left, y), h / 2, Min == 0 ? SettingsOperator.ReturnLevelColour(0) : SettingsOperator.ReturnLevelColour(0).Darkened(0.7f));
		DrawCircle(new Vector2(Right, y), h / 2, Max >= 100 ? SettingsOperator.ReturnLevelColour(99) : SettingsOperator.ReturnLevelColour(99).Darkened(0.7f));

		// handles: white rings with the level colour inside
		for (int k = 0; k < 2; k++)
		{
			int level = k == 0 ? Min : Max;
			var p = new Vector2(XOf(level), y);
			float r = Handle + ((_drag == k || _hover) ? 2 : 0);
			DrawCircle(p, r + 3, new Color(0, 0, 0, 0.35f));
			DrawCircle(p, r, Colors.White);
			DrawCircle(p, r - 4, SettingsOperator.ReturnLevelColour(Math.Min(level, 99)));
			if (!Labels) continue;
			var text = k == 1 && level >= 100 ? "100+" : $"Lv {level}";
			float w = font.GetStringSize(text, HorizontalAlignment.Left, -1, 15).X;
			float tx = Mathf.Clamp(p.X - w / 2, 0, Size.X - w);
			DrawString(font, new Vector2(tx, Size.Y - 2), text, HorizontalAlignment.Left, -1, 15, new Color(1, 1, 1, 0.85f));
		}
	}

	public override void _GuiInput(InputEvent e)
	{
		if (e is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left)
		{
			if (mb.Pressed)
			{
				int level = LevelAt(mb.Position.X);
				_drag = Math.Abs(level - Min) <= Math.Abs(level - Max) && !(level > Max) ? 0 : 1;
				if (Min == Max) _drag = level < Min ? 0 : 1; // stacked handles: pick by direction
				Move(level);
			}
			else if (_drag >= 0)
			{
				_drag = -1;
				QueueRedraw();
				RangeChanged?.Invoke(Min, Max);
			}
			AcceptEvent();
		}
		else if (e is InputEventMouseMotion mm && _drag >= 0)
		{
			Move(LevelAt(mm.Position.X));
			AcceptEvent();
		}
	}

	private void Move(int level)
	{
		if (_drag == 0) Min = Math.Min(level, Max);
		else Max = Math.Max(level, Min);
		QueueRedraw();
		Moving?.Invoke(Min, Max);
	}
}
