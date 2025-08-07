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
	
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		FlashEnable = true;
		SettingsOperator = GetNode<SettingsOperator>("/root/SettingsOperator");
		Flash = GetNode<TextureRect>("Flash");
		check_background();
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

	private void check_background(){
		Vector2 mousePos = GetViewport().GetMousePosition();
		Vector2 screenSize = GetViewportRect().Size;

		float offsetX = (mousePos.X / screenSize.X * 10) - 10;
		float offsetY = (mousePos.Y / screenSize.Y * 10) - 10;

		Position = new Vector2(offsetX, offsetY);

		if (Texture != SettingsOperator.Sessioncfg["background"] && (int)SettingsOperator.Sessioncfg["SongID"] != -1){
			Texture = (Texture2D)SettingsOperator.Sessioncfg["background"];
			Size = new Vector2(GetViewportRect().Size[0]+20,GetViewportRect().Size[1]+20);
			Position = new Vector2(GetViewportRect().Size[0]-5,GetViewportRect().Size[1]-5);
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

		bpm = 60000 / ((int)SettingsOperator.Sessioncfg["beatmapbpm"] * AudioPlayer.Instance.PitchScale) * 0.001f;
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

		check_background();
	}
}
