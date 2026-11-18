/// <summary>
/// The game was laid out for a 1280x720 canvas; the base is now 1920x1080. Layout numbers were scaled by
/// <see cref="Factor"/> (not the full 1.5) so the UI is a bit smaller relative to the screen and there's more room.
/// </summary>
public static class UiScale
{
	/// <summary>UI sizes (fonts, margins, panels, skin elements) compared with the old 1280x720 layout.</summary>
	public const float Factor = 1.25f;
	/// <summary>Canvas size compared with the old one (1080 / 720).</summary>
	public const float ScreenRatio = 1.5f;
}
