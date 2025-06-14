using Godot;
using System;

public partial class SetupScreenEditor : Control
{
    private Tween _focus_animation;
	private int _current_step = 0;
	private int _max_steps = 0; // Adjust this based on the number of steps in your setup
	public override void _Ready()
	{
		_max_steps = GetNode<HBoxContainer>("ScrollContainer/HBoxContainer").GetChildren().Count; // Count the number of steps
	}
	private void _back()
	{
		
	}
	private void _next()
	{
		if (_current_step < _max_steps - 1)
		{
			_current_step++;
			if (_focus_animation != null)
			{
				_focus_animation.Kill();
			}
			_focus_animation = CreateTween();
			_focus_animation.TweenProperty(GetNode<ScrollContainer>("ScrollContainer"), "scroll_horizontal", _current_step*100, 1f)
				.SetTrans(Tween.TransitionType.Cubic)
				.SetEase(Tween.EaseType.Out);
			_focus_animation.Play();
		}
		
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}
}
