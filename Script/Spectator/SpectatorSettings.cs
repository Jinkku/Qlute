using Godot;
using System;

public partial class SpectatorSettings : Control
{
	private bool SpectatorStatus { get; set; }
	private PanelContainer SpectatorMenu { get; set; }
	private Tween Tween { get; set; }
	private Vector2 idle { get; set; }
	private Vector2 focused { get; set; }
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		SpectatorMenu = GetNode<PanelContainer>("SpectatorMenu");
		idle = new Vector2(GetViewportRect().Size.X, SpectatorMenu.Position.Y);
		focused = new Vector2(GetViewportRect().Size.X - SpectatorMenu.Size.X, SpectatorMenu.Position.Y);
		SpectatorMenu.Position = idle;
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		if (SettingsOperator.MouseMovement.X > SpectatorMenu.Position.X - 60)
		{
			Tween?.Kill();
			Tween = CreateTween();
			Tween.TweenProperty(SpectatorMenu, "position", focused, 0.2f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
			Tween.Play();
		}
		else
		{
			Tween?.Kill();
			Tween = CreateTween();
			Tween.TweenProperty(SpectatorMenu, "position", idle, 0.2f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
			Tween.Play();
		}
	}
}
