using Godot;
using System;
using System.Collections.Generic;

public partial class UnstableRate : ColorRect
{
	public static List<float> Rate = new List<float>();
	public override void _Ready()
	{
		Rate.Clear();
	}
	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		if (Rate.Count > 0)
		{
			// Create a copy of Rate to avoid modifying the collection while iterating
			foreach (float ms in new List<float>(Rate))
			{
				var urnote = new ColorRect();
				urnote.Position = new Vector2(-5, (Size.Y / 2) + ((ms / SettingsOperator.MehJudge) * Size.Y));
				urnote.Size = new Vector2(15, 2);
				AddChild(urnote);
				var urani = urnote.CreateTween();
				urani.TweenProperty(urnote, "color", new Color(1f, 1f, 1f, 0f), 1).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
				urani.Play();
				urani.TweenCallback(Callable.From(urnote.QueueFree));
				Rate.Remove(ms);
			}
		}
	}
}
