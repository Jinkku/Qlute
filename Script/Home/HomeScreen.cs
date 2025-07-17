using Godot;
using System;
using System.Diagnostics.Tracing;
using System.IO;
using System.Collections;
using System.Collections.Generic;


public static class HomeButtonID
{
	public static int ID = -1;
}
public partial class HomeScreen : Control
{
	public Label SongTitle { get; set; }
	public Label SongArtist { get; set; }
	public SettingsOperator SettingsOperator { get; set; }
	public PanelContainer SubButtons { get; set; } // For more buttons for more playability
	public PanelContainer HomePanel { get; set; } // Main Buttons
	public override void _Ready()
	{
		SettingsOperator = GetNode<SettingsOperator>("/root/SettingsOperator");
		SubButtons = GetNode<PanelContainer>("SubButton");
		HomePanel = GetNode<PanelContainer>("HomeButtonBG");
		SettingsOperator.loopaudio = false;
		SongTitle = GetNode<Label>("./Titlesong");
		SongArtist = GetNode<Label>("./Descsong");
		AnimationPlayer ani = GetNode<AnimationPlayer>("Flash/AnimationPlayer");
		HomeButtonID.ID = -1;
		//ani.Play("Flash");
		_Process(0);
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double _delta)
	{
		SongTitle.Text = SettingsOperator.Sessioncfg["beatmaptitle"]?.ToString() ?? "No song selected";
		SongArtist.Text = SettingsOperator.Sessioncfg["beatmapartist"]?.ToString() ?? "";

		if (SettingsOperator.Gameplaycfg.TimeTotal - (AudioPlayer.Instance.GetPlaybackPosition() * 1000) < -1000 && SettingsOperator.Beatmaps.Count > 0)
		{
			SettingsOperator.SelectSongID(SettingsOperator.RndSongID());
			AudioPlayer.Instance.Play();
		}
		_subunf();
	}
	private void _test()
	{
		Notify.Post("Hai Hai! ;w;");
	}
	private void _marathonmode()
	{ 
		GetNode<SceneTransition>("/root/Transition").Switch("res://Panels/Screens/MarathonMode.tscn");
	}
	private void _play()
	{
		if (Input.IsActionPressed("TestMode")) GetNode<SceneTransition>("/root/Transition").Switch("res://Panels/Screens/SongSelectV2.tscn");
		else GetNode<SceneTransition>("/root/Transition").Switch("res://Panels/Screens/song_select.tscn");
	}
	private void _browse() {
		GetNode<SceneTransition>("/root/Transition").Switch("res://Panels/Screens/Browse.tscn");
	}
	private void _create() {
		GetNode<SceneTransition>("/root/Transition").Switch("res://Panels/Screens/Create.tscn");
	}
	private void _leave() {
		var tween = CreateTween();
		tween.SetParallel(true);
		tween.TweenProperty(this, "modulate", new Color(0f, 0f, 0f, 0f), 1f)
			.SetTrans(Tween.TransitionType.Linear)
			.SetEase(Tween.EaseType.Out);
		tween.TweenProperty(AudioPlayer.Instance, "volume_db", -80f, 1f)
			.SetTrans(Tween.TransitionType.Linear)
			.SetEase(Tween.EaseType.Out);
		tween.Connect("finished", new Callable(this, nameof(_leavesignal)));
	}
	private void _leavesignal() {
		GetTree().Quit();
	}
	private void _playf()
	{
		HomeButtonID.ID = 1;
	}
	private void _browsef() { HomeButtonID.ID = -1; }
	private void _leavef() { HomeButtonID.ID = -1; }
	private void _createf()
	{
		HomeButtonID.ID = -1;
	}
	private void _subunf()
	{
		if ((GetViewport().GetMousePosition().Y < HomePanel.Position.Y && SubButtons.Visible) | (GetViewport().GetMousePosition().Y > SubButtons.Position.Y + SubButtons.Size.Y && SubButtons.Visible))
			HomeButtonID.ID = -1;
	}
}
