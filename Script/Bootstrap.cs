using Godot;
using System;
using System.Security.Cryptography.X509Certificates;

public partial class Bootstrap : Control
{
	public  SettingsOperator SettingsOperator { get; set; }
	private AnimationPlayer animationPlayer { get; set; }
	public Control Home { get; set; }
	public override void _Ready()
	{
		Cursor.CursorVisible = false;
		animationPlayer = GetNode<AnimationPlayer>("./AnimationPlayer");
		SettingsOperator = GetNode<SettingsOperator>("/root/SettingsOperator");
		SettingsOperator.Sessioncfg["toppanelhide"] = true;
		db = AudioPlayer.Instance.VolumeDb;
	}

    public override void _Process(double delta)
    {
		if (!Kiko.isUpdating && !animationPlayer.IsPlaying())
		{
			animationPlayer.Play("Intro");	
		}
    }
	private Tween NewTween { get; set; }
	private float time { get; set; } = 1.4f;
	private float db { get; set; }
	private void _StartPreview()
	{

		if (SettingsOperator.Beatmaps.Count > 0)
		{
			NewTween = CreateTween();
			NewTween.TweenProperty(AudioPlayer.Instance, "volume_db", db, time).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
			NewTween.Play();
			var num = SettingsOperator.RndSongID();
			var PreviewTime = SettingsOperator.Beatmaps[num].PreviewTime - time;
			SettingsOperator.SelectSongID(num, PreviewTime);
			AudioPlayer.Instance.Play(PreviewTime);
		}


	}
	public void _intro_finished(string animationame)
	{
		Cursor.CursorVisible = true;
		SettingsOperator.toppaneltoggle();
		GetNode<SceneTransition>("/root/Transition").Switch("res://Panels/Screens/home_screen.tscn",false,SceneTransition.TransitionMode.FadeToWhite);
	}
}
