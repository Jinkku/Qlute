using Godot;
using System;

/// <summary>Shared LAN UI helpers: styling for buttons created in code, and formatting.</summary>
public static class LanUi
{
	/// <summary>Seconds -> "m:ss", rounded up so a countdown never shows 0:00 early.</summary>
	public static string FormatTime(double seconds)
	{
		var t = TimeSpan.FromSeconds(Math.Max(0, Math.Ceiling(seconds)));
		return $"{(int)t.TotalMinutes}:{t.Seconds:00}";
	}

	/// <summary>
	/// Keeps lobby/spectate music at normal speed. Needed because SceneTransition frees the old scene about a
	/// second after the new one is ready, and Gameplay._ExitTree then restores the DT/HT speed it saved.
	/// </summary>
	public static void KeepNormalSpeed()
	{
		if (SettingsOperator.inGameplay || ModsOperator.Mods["dt"] || ModsOperator.Mods["ht"]) return;
		if (AudioPlayer.Instance != null && !Mathf.IsEqualApprox(AudioPlayer.Instance.PitchScale, 1f))
			AudioPlayer.Instance.PitchScale = 1f;
	}

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
