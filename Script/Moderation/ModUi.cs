using Godot;
using System;

/// <summary>Small UI pieces for the report dialog and admin panel, in the website's style (see PlayerOverlays).</summary>
public static class ModUi
{
	public static readonly Color Red = new("#d43737");
	public static readonly Color Green = new("#67b930");
	public static readonly Color Purple = new("#983aaf");
	public static readonly Color Yellow = new("#c5c220");
	public static readonly Color Accent = new("#f3baff");
	/// <summary>Input fields: dark enough to show on cards and on the page.</summary>
	private static readonly Color Field = new(0, 0, 0, 0.3f);
	private static readonly Color FieldFocus = new(0, 0, 0, 0.45f);

	public static LineEdit Input(string placeholder, int maxLength = 256)
	{
		var e = new LineEdit { PlaceholderText = placeholder, MaxLength = maxLength, ClearButtonEnabled = true, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		e.AddThemeFontSizeOverride("font_size", 13);
		e.AddThemeStyleboxOverride("normal", PlayerOverlays.Style(Field, 5, 10, 6));
		e.AddThemeStyleboxOverride("focus", PlayerOverlays.Style(FieldFocus, 5, 10, 6));
		return e;
	}

	public static TextEdit TextArea(string placeholder, float height = 90)
	{
		var t = new TextEdit
		{
			PlaceholderText = placeholder, WrapMode = TextEdit.LineWrappingMode.Boundary,
			CustomMinimumSize = new Vector2(0, height), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
		};
		t.AddThemeFontSizeOverride("font_size", 13);
		t.AddThemeStyleboxOverride("normal", PlayerOverlays.Style(Field, 5, 10, 6));
		t.AddThemeStyleboxOverride("focus", PlayerOverlays.Style(FieldFocus, 5, 10, 6));
		return t;
	}

	/// <summary>TextEdit has no max length; trim what we send.</summary>
	public static string Clip(string s, int max) => s.Length > max ? s[..max] : s;

	public static OptionButton Options(params string[] items)
	{
		var o = new OptionButton { FocusMode = Control.FocusModeEnum.None };
		o.AddThemeFontSizeOverride("font_size", 13);
		foreach (var item in items) o.AddItem(item);
		o.AddThemeStyleboxOverride("normal", PlayerOverlays.Style(PlayerOverlays.Card, 5, 10, 6));
		o.AddThemeStyleboxOverride("hover", PlayerOverlays.Style(PlayerOverlays.CardHover, 5, 10, 6));
		o.AddThemeStyleboxOverride("pressed", PlayerOverlays.Style(PlayerOverlays.CardHover, 5, 10, 6));
		return o;
	}

	/// <summary>Coloured button (bg null = the plain flat one).</summary>
	public static Button Button(string text, Color? bg = null)
	{
		var b = PlayerOverlays.FlatButton(text);
		b.Flat = false; // flat buttons skip their styleboxes; these need the coloured backgrounds
		if (bg is Color c)
		{
			b.AddThemeStyleboxOverride("normal", PlayerOverlays.Style(new Color(c, 0.45f), 5, 10, 4));
			b.AddThemeStyleboxOverride("hover", PlayerOverlays.Style(new Color(c, 0.75f), 5, 10, 4));
			b.AddThemeStyleboxOverride("pressed", PlayerOverlays.Style(new Color(c, 0.75f), 5, 10, 4));
		}
		return b;
	}

	/// <summary>A button that needs a second click within 4 seconds ("Ban" -> "Click again to ban").</summary>
	public static Button ConfirmButton(string text, string confirmText, Color? bg, Action action)
	{
		var b = Button(text, bg);
		var armed = false;
		b.Pressed += () =>
		{
			if (!armed)
			{
				armed = true;
				b.Text = confirmText;
				b.GetTree().CreateTimer(4).Timeout += () => { if (GodotObject.IsInstanceValid(b) && armed) { armed = false; b.Text = text; } };
				return;
			}
			armed = false;
			b.Text = text;
			action();
		};
		return b;
	}

	public static Control Pill(string text, Color bg, Color? fg = null)
	{
		var p = PlayerOverlays.PillLabel(text, bg, 11);
		if (fg is Color c) p.GetChild<Label>(0).AddThemeColorOverride("font_color", c);
		return p;
	}

	public static HBoxContainer Row(int gap = 8)
	{
		var r = new HBoxContainer();
		r.AddThemeConstantOverride("separation", gap);
		return r;
	}

	public static HFlowContainer Flow(int gap = 6)
	{
		var f = new HFlowContainer();
		f.AddThemeConstantOverride("h_separation", gap);
		f.AddThemeConstantOverride("v_separation", gap);
		return f;
	}

	public static VBoxContainer Column(int gap = 6)
	{
		var v = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		v.AddThemeConstantOverride("separation", gap);
		return v;
	}

	/// <summary>Rounded card holding a column.</summary>
	public static VBoxContainer Card(Control parent, Color? bg = null, float alpha = 1)
	{
		var card = PlayerOverlays.Panel(bg ?? PlayerOverlays.Card, 8, 14, 10);
		card.Modulate = new Color(1, 1, 1, alpha);
		parent.AddChild(card);
		var v = Column(6);
		card.AddChild(v);
		return v;
	}

	/// <summary>Text that wraps inside the panel width.</summary>
	public static Label Para(string text, int size = 13, Color? color = null)
	{
		var l = PlayerOverlays.Text(text, size, color, wrap: true);
		l.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		return l;
	}

	/// <summary>Quoted text block (report details, ban reasons).</summary>
	public static Control Quote(string text)
	{
		var box = PlayerOverlays.Panel(PlayerOverlays.Pill, 5, 10, 6);
		box.AddChild(Para(text, 13));
		return box;
	}

	/// <summary>"3 Hours Ago" for the past, "in 2 Days" for the future.</summary>
	public static string When(long unix)
	{
		if (unix <= 0) return "never";
		var diff = (DateTimeOffset.FromUnixTimeSeconds(unix) - DateTimeOffset.UtcNow).TotalSeconds;
		if (diff <= 0) return PlayerApi.Ago(unix).ToLowerInvariant();
		(double size, string unit) = diff >= 86400 ? (86400d, "day") : diff >= 3600 ? (3600d, "hour") : diff >= 60 ? (60d, "minute") : (1d, "second");
		var x = (long)(diff / size);
		return $"in {x} {unit}{(x == 1 ? "" : "s")}";
	}

	public static string Date(long unix) =>
		unix <= 0 ? "" : DateTimeOffset.FromUnixTimeSeconds(unix).ToLocalTime().ToString("d MMM yyyy HH:mm");
}
