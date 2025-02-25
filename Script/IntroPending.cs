using Godot;
using System;

public partial class IntroPending : Control
{
	// Called when the node enters the scene tree for the first time.
	public PanelContainer HomeButtons {get;set;}
	public static Vector2 HomeButtonsPOS {get;set;}
	public override void _Ready()
	{
		HomeButtons = GetNode<PanelContainer>("../HomeButtonBG");
		HomeButtonsPOS = HomeButtons.Position;
		Visible = true;
		HomeButtons.Visible = false;
	}
	private void _on_bomb_pressed(){
		HomeButtons.Position = new Vector2(HomeButtons.Position.X,HomeButtons.Position.Y+150);
		HomeButtons.Modulate = new Color(0f,0f,0f,0f);
		var tom = 0.3f;
		var _tween = GetTree().CreateTween();
			_tween.TweenProperty(this, "position", new Vector2(Position.X,Position.Y-150), tom)
				.SetTrans(Tween.TransitionType.Cubic)
				.SetEase(Tween.EaseType.Out);
			_tween.Play();
		var _tween2 = GetTree().CreateTween();
			_tween2.TweenProperty(this, "modulate", new Color(0f,0f,0f,0f), tom)
				.SetTrans(Tween.TransitionType.Cubic)
				.SetEase(Tween.EaseType.Out);
			_tween2.Play();
		var _tween3 = GetTree().CreateTween();
			_tween3.TweenProperty(this, "visible", false, tom)
				.SetTrans(Tween.TransitionType.Cubic)
				.SetEase(Tween.EaseType.Out);
			_tween3.Play();
		var _tween4 = GetTree().CreateTween();
			_tween4.TweenProperty(HomeButtons, "visible", true, 0f)
				.SetTrans(Tween.TransitionType.Cubic)
				.SetEase(Tween.EaseType.Out);
			_tween4.Play();
		var _tween5 = GetTree().CreateTween();
			_tween5.TweenProperty(HomeButtons, "position", HomeButtonsPOS, tom)
				.SetTrans(Tween.TransitionType.Cubic)
				.SetEase(Tween.EaseType.Out);
			_tween5.Play();
		var _tween6 = GetTree().CreateTween();
			_tween6.TweenProperty(HomeButtons, "modulate", new Color(1f,1f,1f,1f), tom)
				.SetTrans(Tween.TransitionType.Cubic)
				.SetEase(Tween.EaseType.Out);
			_tween6.Play();

	}
	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{        
		Vector2 mousePos = GetViewport().GetMousePosition();
        Vector2 screenSize = GetViewportRect().Size;
		float offsetX = (mousePos.X / screenSize.X * 10) - 10;
        float offsetY = (mousePos.Y / screenSize.Y * 10) - 10;

        Position = new Vector2(offsetX, offsetY);
	}
}
