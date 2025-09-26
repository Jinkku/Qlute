using Godot;
using System;

public partial class IntroPending : Control
{
	// Called when the node enters the scene tree for the first time.
	public PanelContainer HomeButtons {get;set;}
	public static bool JustStarted { get; set; }
	public static Vector2 HomeButtonsPOS { get; set; }
	private Vector2 HomeLogoSize {get;set;}
	private Vector2 HomeLogoPos {get;set;}
	private TextureButton Logo {get;set;}
	private Tween LogoTween {get;set;}
	private bool HeartbeatHover { get; set; }
	private int beattick = 1;
	public override void _Ready()
	{
		HomeButtons = GetNode<PanelContainer>("../HomeButtonBG");
		HomeButtonsPOS = HomeButtons.Position;
		Logo = GetNode<TextureButton>("logo");
		Logo.PivotOffset = Logo.Size / 2;
		Visible = true;
		HomeButtons.Visible = false;
		if (JustStarted)
		{
			AnimationTick(HomeScreen.StayOpen);	
		}
		JustStarted = true;
	}
	private void _hover()
	{
		HeartbeatHover = true;
	}

	private void _unhover()
	{
		HeartbeatHover = false;
	}
    public override void _Input(InputEvent @event)
    {
		if (Input.IsActionJustPressed("ui_cancel") && hidden)
		{
			AnimationTick(false);
		}
		else if (Input.IsActionJustPressed("ui_cancel") && !hidden)
		{
			GetTree().CurrentScene.SetProcessInput(false);
			var tween = CreateTween();
			tween.SetParallel(true);
			tween.TweenProperty(GetTree().CurrentScene, "modulate:a", 0f, 1f)
				.SetTrans(Tween.TransitionType.Linear)
				.SetEase(Tween.EaseType.Out);
			tween.TweenProperty(AudioPlayer.Instance, "volume_db", -80f, 1f)
				.SetTrans(Tween.TransitionType.Linear)
				.SetEase(Tween.EaseType.Out);
			tween.Connect("finished", Callable.From(() => GetTree().Quit()));
		}
		else if (@event.IsPressed() && !hidden)
		{
			_on_bomb_pressed(); // or QueueFree() if you wanna delete it instead
		}
    }


	private void _tick()
	{
		if (HeartbeatHover && beattick < 4) Sample.PlaySample("res://SelectableSkins/Slia/Sounds/heartbeat.wav");
		if (HeartbeatHover && beattick == 4) Sample.PlaySample("res://SelectableSkins/Slia/Sounds/downbeat.wav");
		if (beattick > 3) beattick = 1;
		else beattick++; // for the feedback of hovering the Qlute logo

		Logo.Scale = new Vector2(1.02777777778f, 1.02777777778f);
		if (LogoTween != null)
		{
			LogoTween.Kill();
		}
		LogoTween = Logo.CreateTween();
		LogoTween.Parallel().TweenProperty(Logo, "scale", new Vector2(1f, 1f), 60000 / ((int)SettingsOperator.Sessioncfg["beatmapbpm"] * AudioPlayer.Instance.PitchScale) * 0.001)
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.Out);
		LogoTween.Play();
	}
	private bool hidden { get; set; }
	private float time { get; set; } = 0.3f;
	private Tween _tween { get; set; }
	private void AnimationTick(bool type)
	{
		_tween = GetTree().CreateTween();
		_tween.SetParallel(true);
		if (type)
		{
			HomeButtons.Position = new Vector2(HomeButtons.Position.X, HomeButtons.Position.Y + 150);
			HomeButtons.Modulate = new Color(0f, 0f, 0f, 0f);
			_tween.TweenProperty(this, "position:y", Position.Y + 150, time)
					.SetTrans(Tween.TransitionType.Cubic)
					.SetEase(Tween.EaseType.Out);
			_tween.TweenProperty(this, "modulate", new Color(1f, 1f, 1f, 0f), time)
					.SetTrans(Tween.TransitionType.Cubic)
					.SetEase(Tween.EaseType.Out);
			_tween.TweenProperty(this, "visible", false, time)
					.SetTrans(Tween.TransitionType.Cubic)
					.SetEase(Tween.EaseType.Out);
			_tween.TweenProperty(HomeButtons, "visible", true, 0f)
					.SetTrans(Tween.TransitionType.Cubic)
					.SetEase(Tween.EaseType.Out);
			_tween.TweenProperty(HomeButtons, "position", HomeButtonsPOS, time)
					.SetTrans(Tween.TransitionType.Cubic)
					.SetEase(Tween.EaseType.Out);
			_tween.TweenProperty(HomeButtons, "modulate", new Color(1f, 1f, 1f, 1f), time)
					.SetTrans(Tween.TransitionType.Cubic)
					.SetEase(Tween.EaseType.Out);
			_tween.TweenCallback(Callable.From(() => hidden = true));
			_tween.TweenCallback(Callable.From(() => HomeScreen.StayOpen = true));
		}
		else
		{
			Position = new Vector2(Position.X, Position.Y + 150);
			Modulate = new Color(0f, 0f, 0f, 0f);
			_tween.TweenProperty(HomeButtons, "position:y", HomeButtonsPOS.Y + 150, time)
					.SetTrans(Tween.TransitionType.Cubic)
					.SetEase(Tween.EaseType.Out);
			_tween.TweenProperty(HomeButtons, "modulate", new Color(0f, 0f, 0f, 0f), time)
					.SetTrans(Tween.TransitionType.Cubic)
					.SetEase(Tween.EaseType.Out);
			_tween.TweenProperty(HomeButtons, "visible", false, time)
					.SetTrans(Tween.TransitionType.Cubic)
					.SetEase(Tween.EaseType.Out);
			_tween.TweenProperty(this, "position:y", Position.Y - 150, time)
					.SetTrans(Tween.TransitionType.Cubic)
					.SetEase(Tween.EaseType.Out);
			_tween.TweenProperty(this, "visible", true, 0f)
					.SetTrans(Tween.TransitionType.Cubic)
					.SetEase(Tween.EaseType.Out);
			_tween.TweenProperty(this, "modulate", new Color(1f, 1f, 1f, 1f), time)
					.SetTrans(Tween.TransitionType.Cubic)
					.SetEase(Tween.EaseType.Out);
			_tween.TweenCallback(Callable.From(() => hidden = false));
			_tween.TweenCallback(Callable.From(() => HomeScreen.StayOpen = false));
			
		}
		_tween.Play();
	}
	private void _on_bomb_pressed()
	{
		AnimationTick(true);
	}
    public override void _PhysicsProcess(double delta)
    {
		Vector2 mousePos = GetViewport().GetMousePosition();
		Vector2 screenSize = GetViewportRect().Size;
		float offsetX = (mousePos.X / screenSize.X * 5) - 5;
		float offsetY = (mousePos.Y / screenSize.Y * 5) - 5;

		Position = new Vector2(offsetX, offsetY);
    }
}
