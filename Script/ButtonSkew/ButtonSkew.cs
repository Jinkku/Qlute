using Godot;
using System;

public partial class ButtonSkew : PanelContainer
{
	private Tween tween { get; set; }
	private Vector2 NormalSize { get; set; }
	private Vector2 SmallSize { get; set; }
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		PivotOffset = Size / 2;
		NormalSize = Scale;
		SmallSize = Scale * 0.9f;
	}

	public override void _GuiInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton mouseEvent && mouseEvent.ButtonIndex == MouseButton.Left)
		{
			tween?.Kill();
			if (mouseEvent.Pressed)
			{
				tween = CreateTween().BindNode(this);
				tween.SetParallel(true);
				tween.SetTrans(Tween.TransitionType.Bounce);
				tween.SetEase(Tween.EaseType.Out);
				tween.TweenProperty(this, "offset_transform_scale",SmallSize, 0.2f);
			}
			else
			{
				tween = CreateTween().BindNode(this);
				tween.SetParallel(true);
				tween.SetTrans(Tween.TransitionType.Bounce);
				tween.SetEase(Tween.EaseType.Out);
				tween.TweenProperty(this, "offset_transform_scale",NormalSize, 0.2f);
			}
		}
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}
}
