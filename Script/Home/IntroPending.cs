using Godot;
using System;

public partial class IntroPending : Control
{
	public  SettingsOperator SettingsOperator { get; set; }
	public PanelContainer HomeButtons {get;set;}
	public static bool JustStarted { get; set; }
	public static Vector2 HomeButtonsPOS { get; set; }
	private Vector2 HomeLogoSize {get;set;}
	private Vector2 HomeLogoPos {get;set;}
	private TextureButton Logo {get;set;}
	private Tween LogoTween {get;set;}
	private bool HeartbeatHover { get; set; }
	private int beattick = 1;
	private Timer GobackHome { get; set; }
	
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		GobackHome = GetNode<Timer>("GobackLogo");
		HomeButtons = GetNode<PanelContainer>("../HomeButtonBG");
		SettingsOperator = GetNode<SettingsOperator>("/root/SettingsOperator");
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
		IScale = HoverScale;
		HoverChg();
	}

	private void _unhover()
	{
		HeartbeatHover = false;
		IScale = IdleScale;
		HoverChg();
	}
    public override void _Input(InputEvent @event)
    {
		if (Input.IsActionJustPressed("ui_cancel") && hidden)
		{
			AnimationTick(false);
			SettingsOperator.toppaneltoggle(false);
		}
		else if (Input.IsActionJustPressed("ui_cancel") && !hidden)
		{
			SettingsOperator.Quit();
		}
		else if ((Input.IsPhysicalKeyPressed(Key.Enter) || Input.IsPhysicalKeyPressed(Key.Space) || Input.IsPhysicalKeyPressed(Key.P)) && !hidden)
		{
			_on_bomb_pressed();
		}
    }

	private Vector2 IScale { get; set; } = new Vector2(1f, 1f);
	private Vector2 IdleScale { get; set; } = new Vector2(1f, 1f);
	private Vector2 HoverScale { get; set; } = new Vector2(1.1f, 1.1f);

	private void GoBackLogo()
	{
		AnimationTick(false);
		SettingsOperator.toppaneltoggle(false);
	}
	private void HoverChg()
	{
		if (LogoTween != null)
		{
			LogoTween.Kill();
		}
		LogoTween = Logo.CreateTween();
		LogoTween.Parallel().TweenProperty(Logo, "scale", IScale, 0.2f)
			.SetTrans(Tween.TransitionType.Bounce)
			.SetEase(Tween.EaseType.Out);
		LogoTween.Play();
	}
	/// <summary>
	/// Logo Bounce system.
	/// </summary>
	private void StartLogoBounce()
	{
		Logo.Scale = new Vector2(IScale.X + 0.02777777778f, IScale.Y + 0.02777777778f);
		LogoTween?.Kill();
		LogoTween = Logo.CreateTween();
		LogoTween.Parallel().TweenProperty(Logo, "scale", IScale, 60000 / (SettingsOperator.bpm * AudioPlayer.Instance.PitchScale) * 0.001)
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.Out);
		LogoTween.Play();
	}

	private void _tick()
	{
		if (HeartbeatHover && beattick < 4) Sample.PlaySample("res://SelectableSkins/Slia/Sounds/heartbeat.wav");
		if (HeartbeatHover && beattick == 4) Sample.PlaySample("res://SelectableSkins/Slia/Sounds/downbeat.wav");
		if (beattick > 3) beattick = 1;
		else beattick++; // for the feedback of hovering the Qlute logo
		StartLogoBounce();
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
			GobackHome.Start();
			HomeButtons.Scale = new Vector2(1.2f, 1.2f);
			HomeButtons.Modulate = new Color(1f, 1f, 1f, 0f);
			PivotOffset = Size / 2;
			HomeButtons.PivotOffset = HomeButtons.Size / 2;
			HomeButtons.Visible = true;
			Visible = true;
			
			_tween.TweenProperty(HomeButtons, "modulate", new Color(1f, 1f, 1f, 1f), time)
				.SetTrans(Tween.TransitionType.Cubic)
				.SetEase(Tween.EaseType.Out);
			_tween.TweenProperty(HomeButtons, "scale", new Vector2(1f, 1f), time)
				.SetTrans(Tween.TransitionType.Cubic)
				.SetEase(Tween.EaseType.Out);
			_tween.TweenProperty(this, "modulate", new Color(1f, 1f, 1f, 0f), time)
				.SetTrans(Tween.TransitionType.Cubic)
				.SetEase(Tween.EaseType.Out);
			_tween.TweenProperty(this, "scale", new Vector2(1.2f, 1.2f), time)
				.SetTrans(Tween.TransitionType.Cubic)
				.SetEase(Tween.EaseType.Out);
			_tween.TweenCallback(Callable.From(() => hidden = true));
			_tween.TweenCallback(Callable.From(() => HomeScreen.StayOpen = true));
			_tween.TweenCallback(Callable.From(() => Visible = false)).SetDelay(time);
		}
		else
		{
			GobackHome.Stop();
			HomeButtons.Scale = new Vector2(1f, 1f);
			Scale = new Vector2(1.2f, 1.2f);
			HomeButtons.Modulate = new Color(1f, 1f, 1f, 1f);
			Modulate = new Color(1f, 1f, 1f, 0f);
			PivotOffset = Size / 2;
			HomeButtons.PivotOffset = HomeButtons.Size / 2;
			HomeButtons.Visible = true;
			Visible = true;
			
			_tween.TweenProperty(HomeButtons, "modulate", new Color(1f, 1f, 1f, 0f), time)
				.SetTrans(Tween.TransitionType.Cubic)
				.SetEase(Tween.EaseType.Out);
			_tween.TweenProperty(HomeButtons, "scale", new Vector2(1.2f, 1.2f), time)
				.SetTrans(Tween.TransitionType.Cubic)
				.SetEase(Tween.EaseType.Out);
			_tween.TweenProperty(this, "modulate", new Color(1f, 1f, 1f, 1f), time)
				.SetTrans(Tween.TransitionType.Cubic)
				.SetEase(Tween.EaseType.Out);
			_tween.TweenProperty(this, "scale", new Vector2(1f, 1f), time)
				.SetTrans(Tween.TransitionType.Cubic)
				.SetEase(Tween.EaseType.Out);
			_tween.TweenCallback(Callable.From(() => hidden = false));
			_tween.TweenCallback(Callable.From(() => HomeScreen.StayOpen = false));
			_tween.TweenCallback(Callable.From(() => HomeButtons.Visible = false)).SetDelay(time);
		}
		_tween.Play();
	}
	private void _on_bomb_pressed()
	{
		SettingsOperator.toppaneltoggle(true);
		Sample.PlaySample("res://SelectableSkins/Slia/Sounds/play.wav");
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
