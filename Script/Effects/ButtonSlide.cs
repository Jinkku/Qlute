using Godot;
using System;

public partial class ButtonSlide : Button
{
    private void _focus(){
		var _tween = CreateTween();
			_tween.TweenProperty(this, "custom_minimum_size", new Vector2(900,50), 0.2f)
				.SetTrans(Tween.TransitionType.Bounce)
				.SetEase(Tween.EaseType.Out);
			_tween.Play();}
    private void _unfocus(){
		var _tween = CreateTween();
			_tween.TweenProperty(this, "custom_minimum_size", new Vector2(800,50), 0.2f)
				.SetTrans(Tween.TransitionType.Bounce)
				.SetEase(Tween.EaseType.Out);
			_tween.Play();}
}
