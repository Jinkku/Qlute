using Godot;
using System;
using System.Security.Cryptography.X509Certificates;

public partial class Bootstrap : Control
{
	public  SettingsOperator SettingsOperator { get; set; }
	private AnimationPlayer animationPlayer { get; set; }
	public Control Home { get; set; }
	private Label Welcome { get; set; }
	public override void _Ready()
	{
		Cursor.CursorVisible = false;
		Welcome = GetNode<Label>("Welcome");
		Welcome.PivotOffset = Welcome.Size / 2;
		animationPlayer = GetNode<AnimationPlayer>("./AnimationPlayer");
		SettingsOperator = GetNode<SettingsOperator>("/root/SettingsOperator");
		db = (int)(Math.Log10(SettingsOperator.MasterVol / 100.0) * 20) - 5;
		SettingsOperator.toppaneltoggle(false);
	}
	private bool Finished { get; set;}

	private void PlayIntro()
	{
		if (Check.CheckBoolValue(SettingsOperator.GetSetting("hidedevintro").ToString()))
		{
			animationPlayer.Play("Intro");
		}
		else
		{
			animationPlayer.Play("IntroDev");
		}
	}
    public override void _Process(double delta)
	{
		if (!animationPlayer.IsPlaying() && !Finished)
		{
			PlayIntro();
		}
	}
	private Tween NewTween { get; set; }
	private float time { get; set; } = 2.0333f;
	private float db { get; set; }
	private void _StartPreview()
	{
		if (SettingsOperator.Beatmaps.Count > 0)
		{
			AudioPlayer.Instance.VolumeDb = -40;
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
		Finished = true;
		Cursor.CursorVisible = true;
		GetNode<SceneTransition>("/root/Transition").Switch("res://Panels/Screens/home_screen.tscn",mode: SceneTransition.TransitionMode.CrossFade,time: 0.3f);
	}
}
