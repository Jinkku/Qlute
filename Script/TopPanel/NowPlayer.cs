using Godot;
using System;

public partial class NowPlayer : Button
{
	private Tween Tween { get; set; }
	private PanelContainer Card { get; set; }
	private bool IsPlayerOpen { get; set; }
	private void toggleaccountpanel() {
		if (Tween != null && IsInstanceValid(Card))
		{
			Tween.Kill();
			Card?.QueueFree();
		}
		Tween = CreateTween();
		Tween.SetParallel(true);
		if (!IsPlayerOpen)
		{
			Card = GD.Load<PackedScene>("res://Panels/Overlays/NowPlaying.tscn").Instantiate().GetNode<PanelContainer>(".");
			Card.ZIndex = -1;
			AddChild(Card);
			Card.Position = new Vector2((-Card.Size.X/2) + 35, -Card.Size.Y);
			Tween.TweenProperty(Card, "position", new Vector2(Card.Position.X, SettingsOperator.TopPanelPosition + 10), 0.2f).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
			Tween.TweenProperty(GetTree().CurrentScene, "modulate", new Color(0.5f, 0.5f, 0.5f), 0.2f).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
			Tween.Play();
		}
		else
		{
			Tween.TweenProperty(Card, "position", new Vector2(Card.Position.X, -Card.Size.Y), 0.2f).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
			Tween.TweenProperty(GetTree().CurrentScene, "modulate", new Color(1f, 1f, 1f), 0.2f).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
			Tween.TweenCallback(Callable.From(Card.QueueFree));
			Tween.Play();
		}

		IsPlayerOpen = !IsPlayerOpen;
		}
	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}
}
