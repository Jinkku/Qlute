using Godot;
using System;

public partial class BeatmapBackground : TextureRect
{
	// Called when the node enters the scene tree for the first time.
	private SettingsOperator SettingsOperator { get; set; }
	public TextureRect self { get ;set; }
	Texture2D bg {get;set;}
	ColorRect Flash {get;set;}
	public Tween _tween {get;set;}
	public static bool FlashEnable {get;set;}
	public void _flashstart(){
		if (_tween != null){
        _tween.Kill();}
		Flash.SelfModulate = new Color(1f,1f,1f,1f);
		_tween = GetTree().CreateTween();
		_tween.TweenProperty(Flash, "self_modulate", new Color(1f,1f,1f,0f), bpm)
				.SetTrans(Tween.TransitionType.Cubic)
				.SetEase(Tween.EaseType.Out);
		_tween.Play();
	}
	public override void _Ready()
	{
		FlashEnable = true;
		SettingsOperator = GetNode<SettingsOperator>("/root/SettingsOperator");
		Flash = GetNode<ColorRect>("Flash");		
		check_background();
		resettime();
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
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
	public float bpm {get;set;}
	public double bpmtimewait {get;set;}
	public double bpmtime {get;set;}
	public void resettime(){
		bpmtimewait = bpmtime + (bpm/0.001f);
	}
	public override void _Process(double _delta)
	{
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
