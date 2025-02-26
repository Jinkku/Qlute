using Godot;
using System;

public partial class SongOffset : Control
{
	public static SettingsOperator SettingsOperator { get; set; }
	public int tick {get;set;}
	public HSlider OffsetSlider {get;set;}
	public Label Offset {get;set;}
	public HSlider SpeedSlider {get;set;}
	public Label Speed {get;set;}
	public AudioStreamPlayer Audio {get;set;}
	public Timer Timer {get;set;}
	public override void _Ready()
	{
		SettingsOperator = GetNode<SettingsOperator>("/root/SettingsOperator");
		OffsetSlider = GetNode<HSlider>("ModsScreen/MarginContainer/VBoxContainer/OffsetSlider");
		Offset = GetNode<Label>("ModsScreen/MarginContainer/VBoxContainer/Offsetid");
		SpeedSlider = GetNode<HSlider>("ModsScreen/MarginContainer/VBoxContainer/SpeedSlider");
		Speed = GetNode<Label>("ModsScreen/MarginContainer/VBoxContainer/Speedid");
		Audio = GetNode<AudioStreamPlayer>("OffsetSound");
		Timer = GetNode<Timer>("Timer");
		var offset = SettingsOperator.GetSetting("audiooffset") != null ? float.Parse(SettingsOperator.GetSetting("audiooffset").ToString()) : 0;
		OffsetSlider.Value = 200-offset;
		AudioPlayer.Instance.Stop();
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	private void _tick(){
		if (tick+1 > 3){
			tick = 0;
		}else{
			tick++;
		}
		GD.Print("boop");
		var oldcol = new Color(1f,1f,1f,1f);
		var ticka = GetNode<PanelContainer>("ModsScreen/MarginContainer/VBoxContainer/TickBox/Tick"+tick);
		ticka.SelfModulate = oldcol;
		var _tween = GetTree().CreateTween();
			_tween.TweenProperty(ticka, "self_modulate", new Color(1f,1f,1f,0.5f), 0.375f)
				.SetTrans(Tween.TransitionType.Cubic)
				.SetEase(Tween.EaseType.Out);
			_tween.Play();
	}
	private void _goback(){
		AudioPlayer.Instance.Play();
		GetTree().ChangeSceneToFile("res://Panels/Screens/home_screen.tscn");
	}
	private void _speedchanged( float value){
		Audio.PitchScale = value;
		Timer.WaitTime = 0.375f/value;
	}
	public override void _Process(double delta)
	{
		var of = (double)OffsetSlider.Value - 200;
		Offset.Text = "Offset - " + of + "ms";
		var spe = (double)SpeedSlider.Value;
		Speed.Text = "Speed - " + spe + "x";
	}
}
