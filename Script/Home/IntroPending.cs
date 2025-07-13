using Godot;
using System;

public partial class IntroPending : Control
{
	// Called when the node enters the scene tree for the first time.
	public PanelContainer HomeButtons {get;set;}
	public static Vector2 HomeButtonsPOS {get;set;}
	private Vector2 HomeLogoSize {get;set;}
	private Vector2 HomeLogoPos {get;set;}
	private TextureRect Logo {get;set;}
	private Tween LogoTween {get;set;}
	private bool HeartbeatHover { get; set; }
	public override void _Ready()
	{
		HomeButtons = GetNode<PanelContainer>("../HomeButtonBG");
		HomeButtonsPOS = HomeButtons.Position;
		Logo = GetNode<TextureRect>("logo");
		HomeLogoSize = Logo.Size;
		HomeLogoPos = Logo.Position;
		Visible = true;
		HomeButtons.Visible = false;
	}
	private void _hover()
	{
		HeartbeatHover = true;
	}

	private void _unhover()
	{
		HeartbeatHover = false;
	}


	private void _tick()
	{
		if (HeartbeatHover) Sample.PlaySample("res://Skin/Sounds/heartbeat.wav");
		Logo.Size = new Vector2(HomeLogoSize[0] + 10, HomeLogoSize[1] + 10);
		Logo.Position = new Vector2(GetViewportRect().Size.X / 2 - (Logo.Size.X / 2), GetViewportRect().Size.Y / 2 - (Logo.Size.Y / 2));
		if (LogoTween != null)
		{
			LogoTween.Kill();
		}
		LogoTween = Logo.CreateTween();
		LogoTween.Parallel().TweenProperty(Logo, "size", HomeLogoSize, 60000 / ((int)SettingsOperator.Sessioncfg["beatmapbpm"] * AudioPlayer.Instance.PitchScale) * 0.001)
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.Out);
		LogoTween.Parallel().TweenProperty(Logo, "position", HomeLogoPos, 60000 / ((int)SettingsOperator.Sessioncfg["beatmapbpm"] * AudioPlayer.Instance.PitchScale) * 0.001)
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.Out);
		LogoTween.Play();

	}
	private void _on_bomb_pressed(){
		HomeButtons.Position = new Vector2(HomeButtons.Position.X,HomeButtons.Position.Y+150);
		HomeButtons.Modulate = new Color(0f,0f,0f,0f);
		var tom = 0.3f;
		var _tween = GetTree().CreateTween();
		_tween.SetParallel(true);
		_tween.TweenProperty(this, "position", new Vector2(Position.X,Position.Y-150), tom)
				.SetTrans(Tween.TransitionType.Cubic)
				.SetEase(Tween.EaseType.Out);
		_tween.TweenProperty(this, "modulate", new Color(0f,0f,0f,0f), tom)
				.SetTrans(Tween.TransitionType.Cubic)
				.SetEase(Tween.EaseType.Out);
		_tween.TweenProperty(this, "visible", false, tom)
				.SetTrans(Tween.TransitionType.Cubic)
				.SetEase(Tween.EaseType.Out);
		_tween.TweenProperty(HomeButtons, "visible", true, 0f)
				.SetTrans(Tween.TransitionType.Cubic)
				.SetEase(Tween.EaseType.Out);
		_tween.TweenProperty(HomeButtons, "position", HomeButtonsPOS, tom)
				.SetTrans(Tween.TransitionType.Cubic)
				.SetEase(Tween.EaseType.Out);
		_tween.TweenProperty(HomeButtons, "modulate", new Color(1f,1f,1f,1f), tom)
				.SetTrans(Tween.TransitionType.Cubic)
				.SetEase(Tween.EaseType.Out);
		_tween.Play();

	}
	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{        
		Vector2 mousePos = GetViewport().GetMousePosition();
        Vector2 screenSize = GetViewportRect().Size;
		float offsetX = (mousePos.X / screenSize.X * 5) - 5;
        float offsetY = (mousePos.Y / screenSize.Y * 5) - 5;

        Position = new Vector2(offsetX, offsetY);
	}
}
