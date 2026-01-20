using Godot;
using System;

public partial class BeatmapBackground : TextureRect
{
	private SettingsOperator SettingsOperator { get; set; }
	public TextureRect self { get ;set; }
	Texture2D bg {get;set;}
	TextureRect Flash {get;set;}
	public Tween _tween {get;set;}
	public static bool FlashEnable {get;set;}
	private bool flip { get; set; }
	private TextureRect TempImage { get; set; }
	private Texture2D DefaultImage { get; set; }
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		DefaultImage = Texture;
		FlashEnable = true;
		SettingsOperator = GetNode<SettingsOperator>("/root/SettingsOperator");
		Flash = GetNode<TextureRect>("Flash");
		TempImage = GetNode<TextureRect>("TempImage");
		Texture = SettingsOperator.Background;
		check_background(true);
		resettime();
	}
	public void _flashstart()
	{
		flip = !flip;
		Flash.FlipH = flip;
		if (_tween != null)
		{
			_tween.Kill();
		}
		Flash.SelfModulate = new Color(1f, 1f, 1f, 1f);
		_tween = GetTree().CreateTween();
		_tween.Parallel().TweenProperty(Flash, "self_modulate", new Color(1f, 1f, 1f, 0f), bpm)
				.SetTrans(Tween.TransitionType.Cubic)
				.SetEase(Tween.EaseType.Out);
		_tween.Play();
	}
	private Color oldmod { get; set; }
	private Tween ImageTween { get; set; }
	private void Switch_Background(Texture2D Image, bool Instant)
	{
		try
		{
			TempImage.Hide();
			ImageTween?.Kill();
			ImageTween = null;

			if (Instant)
			{
				TempImage.Hide();
				Texture = Image;
				return;
			}

			TempImage.Texture = Texture;
			TempImage.Modulate = SelfModulate;
			TempImage.Visible = true;

			Texture = Image;

			ImageTween = CreateTween();
			ImageTween.TweenProperty(
				TempImage,
				"modulate:a",
				0f,
				0.5f
			);
			ImageTween.TweenCallback(Callable.From(() => TempImage.Hide()));
			
		} catch (Exception err)
		{
			GD.PrintErr(err.Message);
		}
	}
	private void check_background(bool Instant = false)
	{
		Vector2 screenSize = GetViewportRect().Size;

		float offsetX = (SettingsOperator.MouseMovement.X / screenSize.X * 10) - 10;
		float offsetY = (SettingsOperator.MouseMovement.Y / screenSize.Y * 10) - 10;

		Position = new Vector2(offsetX, offsetY); // Sets the position via mouse movements.

		if (Texture != SettingsOperator.Background && SettingsOperator.SongID != -1 && SettingsOperator.Background != null)
		{
			GD.Print("[Qlute] Switching to Specified image");
			Switch_Background(SettingsOperator.Background, Instant);
			Size = new Vector2(GetViewportRect().Size[0] + 20, GetViewportRect().Size[1] + 20);
			Position = new Vector2(GetViewportRect().Size[0] - 5, GetViewportRect().Size[1] - 5);
		}
		else if (Texture != SettingsOperator.GetNullImage() && SettingsOperator.Background == null)
		{
			GD.Print("[Qlute] Switching to Default background");
			Switch_Background(SettingsOperator.GetNullImage(), Instant);
			Size = new Vector2(GetViewportRect().Size[0] + 20, GetViewportRect().Size[1] + 20);
			Position = new Vector2(GetViewportRect().Size[0] - 5, GetViewportRect().Size[1] - 5);	
		}
	}
	public static float bpm {get;set;}
	public double bpmtimewait {get;set;}
	public double bpmtime {get;set;}
	public void resettime(){
		bpmtimewait = bpmtime + (bpm/0.001f);
	}
	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double _delta)
	{
		oldmod = SelfModulate;

		bpm = 60000 / (SettingsOperator.bpm * AudioPlayer.Instance.PitchScale) * 0.001f;
		bpmtime = Extras.GetMilliseconds();
		if (bpmtimewait - bpmtime < 0) // Ticks for BPM
		{
			resettime();
			_flashstart();
		}
		if (!Flash.Visible && FlashEnable)
		{
			Flash.Visible = true;
			resettime();
			_flashstart();
		}
		else if (Flash.Visible && !FlashEnable)
		{
			Flash.Visible = false;
		}

		check_background(Instant: false);
	}
}
