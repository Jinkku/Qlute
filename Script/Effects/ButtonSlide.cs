using Godot;
using System;

public partial class ButtonSlide : Button
{
	private void _focus()
	{
		Sample.PlaySample("res://SelectableSkins/Slia/Sounds/hover.wav");
		var _tween = CreateTween();
		_tween.TweenProperty(this, "custom_minimum_size", new Vector2(1125, 63), 0.2f)
			.SetTrans(Tween.TransitionType.Bounce)
			.SetEase(Tween.EaseType.Out);
		_tween.Play();
	}
	private void _unfocus()
	{
		var _tween = CreateTween();
		_tween.TweenProperty(this, "custom_minimum_size", new Vector2(1000, 63), 0.2f)
			.SetTrans(Tween.TransitionType.Bounce)
			.SetEase(Tween.EaseType.Out);
		_tween.Play();
	}
	private void _click()
	{
		Sample.PlaySample("res://SelectableSkins/Slia/Sounds/selected.wav");
	}
	
	private void _down()
	{
		Sample.PlaySample("res://SelectableSkins/Slia/Sounds/hover.wav");
	}
}
