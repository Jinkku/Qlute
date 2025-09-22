using Godot;
using System;
using System.Collections.Generic;

public partial class UnstableRate : ColorRect
{
	public static List<float> Rate = new List<float>();
	private float Space { get; set; }
	private float SMG { get; set; }
	private ColorRect Perfect { get; set; }
	private ColorRect Great { get; set; }
	public override void _Ready()
	{
		SMG = (float)SettingsOperator.PerfectJudge / (float)SettingsOperator.PerfectJudgeMin;
		Space = (float)SMG * Size.Y / 6;
		Perfect = GetNode<ColorRect>("Perfect");
		Great = GetNode<ColorRect>("Great");
		Size = new Vector2(Size.X, Space * 6);
		Great.Size = new Vector2(Size.X, Space * 4);
		Perfect.Size = new Vector2(Size.X, Space);

		Great.Position = new Vector2(
			0,
			(Size.Y / 2f) - (Great.Size.Y / 2f)
		);

		Perfect.Position = new Vector2(
			0,
			(Size.Y / 2f) - (Perfect.Size.Y / 2f)
		);



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
