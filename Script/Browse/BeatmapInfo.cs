using Godot;
using System;
using System.Collections;

public partial class BeatmapInfo : Control
{
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}
	private void _endit()
	{
		GetTree().CurrentScene.GetNode<ColorRect>("Blank-Chan").QueueFree();
		QueueFree();
	}
	private void _close()
	{
		var Animation = CreateTween();
		var Blank = GetTree().CurrentScene.GetNode<ColorRect>("Blank-Chan");
		var BeatmapInfoCard = GetTree().CurrentScene.GetNode<Control>("BeatmapInfo");
		var Back = BeatmapInfoCard.GetNode<Button>("Back");
		Animation.Connect("finished", new Callable(this, nameof(_endit)));
		Animation.SetParallel(true);
		Animation.TweenProperty(Blank, "modulate", new Color(1f, 1f, 1f, 0f), 0.5f)
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.Out);
		Animation.TweenProperty(BeatmapInfoCard, "position", new Vector2(0, GetViewportRect().Size.Y), 0.5f)
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.Out);
		Animation.Play();
	}
}
