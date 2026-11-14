using Godot;

/// <summary>
/// Map-provided visuals every game mode adds in _Ready: the storyboard (above the background) and the
/// beatmap shader (above the storyboard, or above the playfield when the map asks for the whole screen).
/// </summary>
public static class BeatmapVisuals
{
	public static void Attach(Control gameplayRoot, CanvasItem background)
	{
		var storyboard = StoryboardPlayer.AttachToGameplay(gameplayRoot, background);
		BeatmapShaderLayer.AttachToGameplay(gameplayRoot, (CanvasItem)storyboard ?? background);
	}
}
