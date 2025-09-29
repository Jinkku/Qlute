using Godot;
using System;

public partial class GameModeDifficulty : TextureButton
{
	private int index { get; set; }
	private int rootindex { get; set; }
	private Label Num { get; set; }
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		if (HasMeta("index") && HasMeta("rootindex"))
		{
			index = (int)GetMeta("index");
			rootindex = (int)GetMeta("rootindex");
			Num = GetNode<Label>("Num");
			var cache = Browse.BrowseCatalog[rootindex].beatmaps;
			var level = SettingsOperator.GetLevelRating(cache[index].count_circles + cache[index].count_sliders, cache[index].total_length);
			Num.Text = $"{(level).ToString("N0")}";
			SelfModulate = SettingsOperator.ReturnLevelColour((int)level);
			Idlecolour = SelfModulate;
			Focuscolour = SelfModulate * 1.5f;
			highlightcolour = SelfModulate * 2;
		}
	}
	private Tween _focus_animation;
	private void AnimationButton(Color colour)
	{
		_focus_animation?.Kill();

		_focus_animation = CreateTween();
		_focus_animation.SetParallel(true);
		_focus_animation.TweenProperty(this, "self_modulate", colour, 0.2f)
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.Out);
	}
	private Color Idlecolour = new Color(0.20f, 0.20f, 0.20f, 1f);
	private Color Focuscolour = new Color(1f, 1f, 1f, 1f);
	private Color highlightcolour = new Color(0.19f, 0.37f, 0.65f, 1f);
	private void _highlight() {
		BeatmapInfo.BeatmapIndex = index;
		AnimationButton(highlightcolour);
	}
	private void _focus()
	{
		BeatmapInfo.BeatmapIndexH = index;
		AnimationButton(Focuscolour);
	}
	private void _unfocus()
	{
		BeatmapInfo.BeatmapIndexH = -1;
		AnimationButton(Idlecolour);
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}
}
