using Godot;
using System;
[Tool]
public partial class Effect : Control
{
	private Vector2 SqSize { get; set; }
	private Tween Tween { get; set;}

	public bool Template { get; set; }
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		GD.Print(Size);
		GD.Print($"Effect Template is {Template}");
		SqSize = new Vector2(113f, 113f);
		if (!Engine.IsEditorHint() && !Template)
		{
			GD.Print($"Effect is doing animation");
			//Scale /= 2;
			SelfModulate = new Color(SelfModulate.R, SelfModulate.G, SelfModulate.B, 0.5f);
			Tween?.Kill();
			Tween = CreateTween().BindNode(this);
			Tween.SetParallel(true);
			Tween.SetEase(Tween.EaseType.Out);
			Tween.SetTrans(Tween.TransitionType.Cubic);
			//Tween.TweenProperty(this, "scale", new Vector2(1f, 1f), 0.5f);
			Tween.TweenProperty(this, "self_modulate:a", 1f, 0.5f);
			//Tween.TweenProperty(this, "scale", new Vector2(1.3f, 1.3f), 0.5f).SetDelay(0.5f);
			Tween.TweenProperty(this, "self_modulate:a", 0f, 0.5f).SetDelay(0.5f);
			Tween.TweenCallback(Callable.From(() => QueueFree())).SetDelay(1.1f);
			Tween.Play();
			
			GD.Print($"Effect is finished");
		}
	}

	public override void _Draw()
	{
		DrawRect(new Rect2(new Vector2(Position.X - SqSize.X / 2, Position.Y - SqSize.Y / 2 ), SqSize),new Color(1f,1f,1f,1f),false,3f);
	}
	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		QueueRedraw();
	}
}
