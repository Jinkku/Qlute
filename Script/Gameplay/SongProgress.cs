using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// Song progress HUD component (Panels/GameplayElements/Customizable/SongProgress.tscn).
/// Two looks, picked per skin in the skin editor (<see cref="ISkinSettings"/>):
/// <list type="bullet">
/// <item>Density graph (default): a column per slice of the song showing how many notes it has, coloured from calm
/// (blue) to hardest (red); the played part is lit, with a thin bar and knob underneath. The graph rises above the
/// node's rect by "Graph height".</item>
/// <item>Classic bar: the original progress bar, untouched.</item>
/// </list>
/// While watching a replay it's also the seek bar: click or drag to jump (<see cref="Gameplay.SeekTo"/>).
/// </summary>
public partial class SongProgress : ProgressBar, ISkinSettings
{
	private static readonly SkinSetting[] Settings =
	{
		new("style", "Style", 0, Options: new[] { "Density graph", "Classic bar" }),
		new("height", "Graph height (px)", 44, 8, 300, 1, Tooltip: "How far the density graph rises above the bar"),
		new("columns", "Column width (px)", 6, 2, 40, 1),
		new("heat", "Colour by difficulty", 1, Toggle: true, Tooltip: "Off: one colour for the whole graph"),
	};
	public IReadOnlyList<SkinSetting> SkinSettings => Settings;

	[Export] public Color PlayedColor { get; set; } = new("#c7b8ff");
	[Export] public Color UnplayedColor { get; set; } = new(1, 1, 1, 0.16f);
	[Export] public Color KnobColor { get; set; } = Colors.White;
	[Export(PropertyHint.Range, "0,1")] public float UnplayedAlpha { get; set; } = 0.3f;

	private bool _graph = true, _heat = true;
	private float _graphHeight = 44, _columnWidth = 6;

	private float[] _density = Array.Empty<float>(); // 0..1 per column
	private int _builtFor = -1, _builtColumns;
	private float _builtLength;

	private bool _dragging, _hover;
	private float _previewSec;
	private StyleBox _empty = new StyleBoxEmpty();

	public override void _Ready()
	{
		Resized += ApplyStyle;
		MouseEntered += () => _hover = true;
		MouseExited += () => _hover = false;
		ApplyStyle();
		_Process(0);
	}

	public void ApplySkinSettings(IReadOnlyDictionary<string, float> values)
	{
		float Get(string key) => values.TryGetValue(key, out var v) ? v : Array.Find(Settings, s => s.Key == key).Default;
		bool graph = Get("style") < 0.5f;
		float height = Mathf.Max(0, Get("height")), cols = Mathf.Max(2, Get("columns"));
		bool heat = Get("heat") >= 0.5f;
		if (graph == _graph && height == _graphHeight && cols == _columnWidth && heat == _heat) return;
		_graph = graph; _graphHeight = height; _heat = heat;
		if (cols != _columnWidth) { _columnWidth = cols; _builtFor = -1; }
		ApplyStyle();
	}

	public void PreviewSkinSettings() => QueueRedraw();

	private readonly Dictionary<Control, (float top, float bottom)> _labelOffsets = new();

	/// <summary>Graph style: the ProgressBar's own drawing is switched off and _Draw does everything.</summary>
	private void ApplyStyle()
	{
		// the time labels sit above the bar; with the graph they move above the graph
		foreach (var child in GetChildren())
		{
			if (child is not Label label) continue;
			if (!_labelOffsets.TryGetValue(label, out var o)) _labelOffsets[label] = o = (label.OffsetTop, label.OffsetBottom);
			float lift = _graph ? _graphHeight + 6 - Size.Y + 4 : 0;
			label.OffsetTop = o.top - Mathf.Max(0, lift);
			label.OffsetBottom = o.bottom - Mathf.Max(0, lift);
		}
		if (_graph)
		{
			AddThemeStyleboxOverride("background", _empty);
			AddThemeStyleboxOverride("fill", _empty);
		}
		else
		{
			RemoveThemeStyleboxOverride("background");
			RemoveThemeStyleboxOverride("fill");
		}
		QueueRedraw();
	}

	private static Gameplay Game => GodotObject.IsInstanceValid(Gameplay.Instance) ? Gameplay.Instance : null;
	private static bool Seekable => Game is { CanSeek: true };

	/// <summary>Song length in seconds (the audio; falls back to the last note).</summary>
	private static float LengthSec
	{
		get
		{
			float t = (float)SettingsOperator.Gameplaycfg.TimeTotal;
			return t > 0 ? t : Mathf.Max(1, (float)SettingsOperator.Gameplaycfg.TimeTotalGame);
		}
	}

	public override void _Process(double delta)
	{
		MaxValue = LengthSec;
		Value = SettingsOperator.Gameplaycfg.Time;
		bool seekable = Seekable;
		MouseFilter = seekable ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
		MouseDefaultCursorShape = seekable ? CursorShape.PointingHand : CursorShape.Arrow;
		if (!seekable) { _dragging = false; _hover = false; }
		if (_graph) BuildDensity();
		QueueRedraw();
	}

	/// <summary>The graph area counts for the mouse too (it rises above the rect).</summary>
	public override bool _HasPoint(Vector2 point)
	{
		float top = _graph ? -_graphHeight : 0;
		return point.X >= 0 && point.X <= Size.X && point.Y >= top - 6 && point.Y <= Size.Y + 6;
	}

	/// <summary>Notes per column over the song, from the map pp is rated on (same for every mode).</summary>
	private void BuildDensity()
	{
		int id = SettingsOperator.SessionConfig.SongID;
		int columns = Mathf.Max(8, (int)(Size.X / _columnWidth));
		float length = LengthSec;
		if (id == _builtFor && columns == _builtColumns && Mathf.IsEqualApprox(length, _builtLength)) return;
		_builtFor = id; _builtColumns = columns; _builtLength = length;
		_density = new float[columns];
		if (id < 0 || id >= SettingsOperator.Beatmaps.Count) return;
		var b = SettingsOperator.Beatmaps[id];
		if (!b.HasDetails()) { _builtFor = -1; return; } // loading screen parses them; try again next frame
		foreach (var (time, _) in b.RatedNotes)
		{
			int i = (int)(time / 1000f / length * columns);
			if (i >= 0 && i < columns) _density[i]++;
		}
		// soften single busy columns, then scale to the busiest
		var smooth = new float[columns];
		float max = 0;
		for (int i = 0; i < columns; i++)
		{
			float a = _density[Math.Max(0, i - 1)], c = _density[i], d = _density[Math.Min(columns - 1, i + 1)];
			smooth[i] = (a + 2 * c + d) / 4;
			max = Mathf.Max(max, smooth[i]);
		}
		for (int i = 0; i < columns; i++) _density[i] = max > 0 ? smooth[i] / max : 0;
	}

	/// <summary>Calm (blue) to hardest (red), the level colours.</summary>
	private Color Heat(float t) => _heat ? SettingsOperator.ReturnLevelColour((int)(Mathf.Clamp(t, 0, 1) * 60)) : PlayedColor;

	public override void _Draw()
	{
		float progress = Mathf.Clamp((float)(Value / Math.Max(0.001, MaxValue)), 0, 1);
		float barY = Size.Y - 4;
		if (_graph)
		{
			int n = _density.Length;
			float colW = n > 0 ? Size.X / n : 1, gap = Mathf.Min(2, colW * 0.3f);
			for (int i = 0; i < n; i++)
			{
				float h = Mathf.Max(2, _density[i] * _graphHeight);
				float x = i * colW;
				bool played = (i + 1) * colW <= Size.X * progress;
				var c = Heat(_density[i]);
				c = played ? c.Lerp(Colors.White, 0.15f) : c with { A = UnplayedAlpha };
				DrawRect(new Rect2(x + gap / 2, barY - 6 - h, colW - gap, h), c);
			}
			// bar + knob
			DrawRect(new Rect2(0, barY - 1.5f, Size.X, 3), UnplayedColor);
			DrawRect(new Rect2(0, barY - 1.5f, Size.X * progress, 3), PlayedColor);
			DrawCircle(new Vector2(Size.X * progress, barY), 5, KnobColor);
		}

		if (_hover || _dragging) // replays: where a click lands / where the drag is
		{
			float x = _dragging ? _previewSec / LengthSec * Size.X : Mathf.Clamp(GetLocalMousePosition().X, 0, Size.X);
			float top = _graph ? barY - 6 - _graphHeight : 0;
			DrawLine(new Vector2(x, top), new Vector2(x, Size.Y), KnobColor with { A = 0.85f }, 2);
			var font = GetThemeDefaultFont();
			var text = Clock(x / Mathf.Max(1, Size.X) * LengthSec);
			const int size = 16;
			float w = font.GetStringSize(text, HorizontalAlignment.Left, -1, size).X;
			DrawString(font, new Vector2(Mathf.Clamp(x - w / 2, 0, Size.X - w), top - 6), text, HorizontalAlignment.Left, -1, size, KnobColor);
		}
	}

	private float SecAt(float x) => Mathf.Clamp(x / Mathf.Max(1, Size.X), 0, 1) * LengthSec;

	public override void _GuiInput(InputEvent e)
	{
		var game = Game;
		if (game == null || !game.CanSeek) return;
		if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left } mb)
		{
			if (mb.Pressed) { _dragging = true; _previewSec = SecAt(mb.Position.X); }
			else if (_dragging) { _dragging = false; game.SeekTo(SecAt(mb.Position.X) * 1000f); }
			AcceptEvent();
		}
		else if (e is InputEventMouseMotion mm && _dragging)
		{
			_previewSec = SecAt(mm.Position.X);
			AcceptEvent();
		}
	}

	private static string Clock(float sec)
	{
		int s = Mathf.Max(0, (int)sec);
		return $"{s / 60}:{s % 60:00}";
	}
}
