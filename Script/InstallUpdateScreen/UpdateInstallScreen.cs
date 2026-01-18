using Godot;
using System;

public partial class UpdateInstallScreen : Control
{
	private Tween Tween { get; set; }
	public Kiko Kiko { get; set; }
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		ZIndex = 1024;
		Kiko = GetNode<Kiko>("/root/Kiko");
		Modulate = new Color(1f, 1f, 1f, 0f);
		Tween = CreateTween();
		Tween.SetTrans(Tween.TransitionType.Cubic);
		Tween.SetEase(Tween.EaseType.Out);
		Tween.TweenProperty(this, "modulate:a", 1f, 0.3f);
	}

	private void Install()
	{
		Kiko.PrepareUpdateProcess();
	}
	private void Exit()
	{
		Modulate = new Color(1f, 1f, 1f, 1f);
		Tween = CreateTween();
		Tween.SetTrans(Tween.TransitionType.Cubic);
		Tween.SetEase(Tween.EaseType.Out);
		Tween.TweenProperty(this, "modulate:a", 0f, 0.3f);
		Tween.Finished += () => QueueFree();
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}
}
