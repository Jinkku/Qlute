using Godot;
using System;

public partial class ButtonBounce : Button
{
	private float vsize = 95;
	public override void _Ready()
	{
		vsize = Size.Y;
	}
    private void _focus()
	{
		var _tween = GetTree().CreateTween();
		_tween.TweenProperty(this, "custom_minimum_size", new Vector2(180, vsize), 0.2f)
			.SetTrans(Tween.TransitionType.Bounce)
			.SetEase(Tween.EaseType.Out);
		_tween.Play();
	}
    private void _unfocus(){
		var _tween = GetTree().CreateTween();
			_tween.TweenProperty(this, "custom_minimum_size", new Vector2(154,vsize), 0.2f)
				.SetTrans(Tween.TransitionType.Bounce)
				.SetEase(Tween.EaseType.Out);
			_tween.Play();}
}
