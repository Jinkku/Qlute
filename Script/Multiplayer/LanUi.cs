using Godot;

/// <summary>Styling for buttons created in code, matching the menus' blue-grey buttons.</summary>
public static class LanUi
{
	private static StyleBoxFlat Box(Color c)
	{
		var sb = new StyleBoxFlat { BgColor = c };
		sb.SetCornerRadiusAll(8);
		sb.SetContentMarginAll(8);
		return sb;
	}

	public static T Style<T>(T b) where T : Button
	{
		b.AddThemeStyleboxOverride("normal", Box(new Color(0.26f, 0.32f, 0.41f)));
		b.AddThemeStyleboxOverride("hover", Box(new Color(0.36f, 0.42f, 0.51f)));
		b.AddThemeStyleboxOverride("pressed", Box(new Color(0.24f, 0.55f, 0.36f)));
		b.AddThemeStyleboxOverride("hover_pressed", Box(new Color(0.3f, 0.62f, 0.42f)));
		b.AddThemeStyleboxOverride("disabled", Box(new Color(0.18f, 0.2f, 0.25f)));
		b.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
		foreach (var key in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_hover_pressed_color", "font_focus_color" })
			b.AddThemeColorOverride(key, Colors.White);
		b.AddThemeColorOverride("font_disabled_color", new Color(0.55f, 0.58f, 0.65f));
		return b;
	}
}
