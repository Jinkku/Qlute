using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// HUD popup shown when the scroll speed changes (F3 / F4), like osu!mania's toast
/// (Panels/GameplayElements/Customizable/ScrollSpeedPopup.tscn). Its animation is a skin option: style, in / hold /
/// out times and slide distance are set per skin in the skin editor (<see cref="ISkinSettings"/>). The root is
/// placed by the skin layout; only the inner "Box" moves, so the layout and the animation never fight.
/// </summary>
public partial class ScrollSpeedPopup : Control, ISkinSettings
{
	public enum Style { Pop, SlideUp, SlideDown, SlideFromLeft, SlideFromRight, Fade, Bounce }
	private static readonly string[] StyleNames = { "Pop", "Slide up", "Slide down", "Slide from left", "Slide from right", "Fade", "Bounce" };

	private static readonly SkinSetting[] Settings =
	{
		new("animation", "Animation", (int)Style.Pop, Options: StyleNames),
		new("in", "In time (s)", 0.2f, 0.01f, 2, 0.01f, Tooltip: "How long it takes to appear"),
		new("hold", "Hold time (s)", 1.0f, 0, 10, 0.05f, Tooltip: "How long it stays after the last change"),
		new("out", "Out time (s)", 0.35f, 0.01f, 3, 0.01f, Tooltip: "How long it takes to disappear"),
		new("distance", "Distance (px)", 40, 0, 600, 1, Tooltip: "How far the slide styles travel"),
		new("showtime", "Show time on screen", 1, Toggle: true, Tooltip: "The ms a note takes to reach the hit line"),
		new("always", "Always visible", 0, Toggle: true, Tooltip: "Never hide (a permanent scroll speed display)"),
	};

	public IReadOnlyList<SkinSetting> SkinSettings => Settings;

	private Style _style = Style.Pop;
	private float _in = 0.2f, _hold = 1f, _out = 0.35f, _distance = 40;
	private bool _showTime = true, _always;

	private Control _box;
	private Label _value, _time, _arrow;
	private Tween _tween;
	private bool _shown;

	public override void _Ready()
	{
		_box = GetNode<Control>("%Box");
		_value = GetNode<Label>("%Value");
		_time = GetNode<Label>("%Time");
		_arrow = GetNode<Label>("%Arrow");
		MouseFilter = MouseFilterEnum.Ignore;
		UpdateText(ScrollSpeed.Value, 0);
		_box.Modulate = _box.Modulate with { A = Resting ? 1 : 0 };
	}

	public override void _EnterTree() => ScrollSpeed.Changed += OnChanged;
	public override void _ExitTree() => ScrollSpeed.Changed -= OnChanged;

	/// <summary>Stays on screen: a permanent display, or so it can be placed in the skin editor.</summary>
	private bool Resting => _always || SkinEditor.IsOpen;

	public override void _Process(double delta)
	{
		_box.PivotOffset = _box.Size / 2;
		// idle in the skin editor: show it at rest so it can be seen and moved
		if (Resting && (_tween == null || !_tween.IsValid() || !_tween.IsRunning()) && _box.Modulate.A < 1)
		{
			_box.Position = Vector2.Zero;
			_box.Scale = Vector2.One;
			_box.Modulate = _box.Modulate with { A = 1 };
		}
	}

	public void ApplySkinSettings(IReadOnlyDictionary<string, float> values)
	{
		float Get(string key) => values.TryGetValue(key, out var v) ? v : Array.Find(Settings, s => s.Key == key).Default;
		_style = (Style)Math.Clamp((int)Get("animation"), 0, StyleNames.Length - 1);
		_in = Mathf.Max(0.01f, Get("in"));
		_hold = Mathf.Max(0, Get("hold"));
		_out = Mathf.Max(0.01f, Get("out"));
		_distance = Get("distance");
		_showTime = Get("showtime") >= 0.5f;
		_always = Get("always") >= 0.5f;
		if (_time != null) _time.Visible = _showTime;
	}

	public void PreviewSkinSettings() => Play(replayIn: true);

	private void OnChanged(float speed, int direction)
	{
		UpdateText(speed, direction);
		Play(replayIn: !_shown);
	}

	private void UpdateText(float speed, int direction)
	{
		if (_value == null) return;
		_value.Text = $"{speed:0}";
		_time.Text = $"{ScrollSpeed.MaxTimeRange / speed:0} ms";
		_time.Visible = _showTime;
		_arrow.Text = direction > 0 ? "▲" : direction < 0 ? "▼" : "";
	}

	/// <summary>Where the box starts (in) and ends (out) for the current style.</summary>
	private (Vector2 pos, float scale) Offstage(bool entering)
	{
		float d = _distance;
		return _style switch
		{
			Style.Pop => (Vector2.Zero, entering ? 0.6f : 0.9f),
			Style.Bounce => (Vector2.Zero, entering ? 1.35f : 0.8f),
			Style.SlideUp => (new Vector2(0, entering ? d : -d * 0.5f), 1),
			Style.SlideDown => (new Vector2(0, entering ? -d : d * 0.5f), 1),
			Style.SlideFromLeft => (new Vector2(entering ? -d : d * 0.5f, 0), 1),
			Style.SlideFromRight => (new Vector2(entering ? d : -d * 0.5f, 0), 1),
			_ => (Vector2.Zero, 1),
		};
	}

	private void Play(bool replayIn)
	{
		if (_box == null) return;
		_tween?.Kill();
		_tween = CreateTween();
		_shown = true;
		var inTrans = _style switch { Style.Pop => Tween.TransitionType.Back, Style.Bounce => Tween.TransitionType.Elastic, _ => Tween.TransitionType.Cubic };
		if (replayIn)
		{
			var (pos, scale) = Offstage(entering: true);
			_box.Position = pos;
			_box.Scale = Vector2.One * scale;
			_box.Modulate = _box.Modulate with { A = 0 };
			_tween.SetParallel(true);
			_tween.TweenProperty(_box, "position", Vector2.Zero, _in).SetTrans(inTrans).SetEase(Tween.EaseType.Out);
			_tween.TweenProperty(_box, "scale", Vector2.One, _style == Style.Bounce ? _in * 2.5f : _in).SetTrans(inTrans).SetEase(Tween.EaseType.Out);
			_tween.TweenProperty(_box, "modulate:a", 1f, Mathf.Min(_in, 0.15f));
			_tween.SetParallel(false);
		}
		else
		{
			// already up: a little nudge so every press is felt, then the hold restarts
			_box.Position = Vector2.Zero;
			_box.Modulate = _box.Modulate with { A = 1 };
			_box.Scale = Vector2.One * 1.08f;
			_tween.TweenProperty(_box, "scale", Vector2.One, 0.15f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		}
		if (Resting) { _tween.TweenCallback(Callable.From(() => _shown = false)); return; }
		_tween.TweenInterval(_hold);
		var (outPos, outScale) = Offstage(entering: false);
		_tween.SetParallel(true);
		_tween.TweenProperty(_box, "position", outPos, _out).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
		_tween.TweenProperty(_box, "scale", Vector2.One * outScale, _out).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
		_tween.TweenProperty(_box, "modulate:a", 0f, _out);
		_tween.SetParallel(false);
		_tween.TweenCallback(Callable.From(() => _shown = false));
	}
}
