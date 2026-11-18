using Godot;
using System;

/// <summary>
/// Base of song select's carousel cards (MusicCard.tscn for a set, DifficultyCard.tscn for a difficulty).
/// The look comes from the scene: its "normal" / "hover" styleboxes are copied and only recoloured here.
/// </summary>
public abstract partial class CarouselCard : Button
{
	internal SongCarousel Carousel;
	protected int SongId = -1;
	protected abstract bool IsSetCard { get; }
	private StyleBox _normal, _hover;

	public override void _Ready()
	{
		_normal = GetThemeStylebox("normal");
		_hover = HasThemeStyleboxOverride("hover") ? GetThemeStylebox("hover") : _normal;
		MouseEntered += () => SettingsOperator.SessionConfig.SongIDHighlighted = SongId; // right-click menu target
		MouseExited += () => { if (SettingsOperator.SessionConfig.SongIDHighlighted == SongId) SettingsOperator.SessionConfig.SongIDHighlighted = -1; };
		Pressed += () => { if (SongId >= 0) Carousel?.Clicked(SongId, IsSetCard); };
		Setup();
	}

	/// <summary>
	/// Whichever of <paramref name="light"/> / <paramref name="dark"/> text has more contrast on <paramref name="bg"/>
	/// (WCAG contrast ratio, so it flips where both are equally readable). Keeps the text's alpha.
	/// </summary>
	public static Color TextOn(Color bg, Color light, Color dark)
	{
		float L(Color c) { var l = c.SrgbToLinear(); return 0.2126f * l.R + 0.7152f * l.G + 0.0722f * l.B; }
		float Ratio(float a, float b) => (Mathf.Max(a, b) + 0.05f) / (Mathf.Min(a, b) + 0.05f);
		float back = L(bg);
		return Ratio(back, L(light)) >= Ratio(back, L(dark)) ? light : dark;
	}

	/// <summary>Find the scene's parts (called once from _Ready).</summary>
	protected abstract void Setup();

	/// <summary>Copies of the scene's normal / hover styleboxes changed by <paramref name="edit"/> (bool = the hover one).</summary>
	protected void Restyle(Action<StyleBoxFlat, bool> edit)
	{
		void Set(string name, StyleBox src, bool hover)
		{
			if (src is not StyleBoxFlat flat) return;
			var copy = (StyleBoxFlat)flat.Duplicate();
			edit(copy, hover);
			AddThemeStyleboxOverride(name, copy);
		}
		Set("normal", _normal, false);
		Set("hover", _hover, true);
		Set("pressed", _hover, true);
		Set("hover_pressed", _hover, true);
	}
}
