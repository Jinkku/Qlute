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
	public static bool StayOpen { get; set; }
	public Label SongIndicator { get; set; }
	public SettingsOperator SettingsOperator { get; set; }
	public PanelContainer SubButtons { get; set; } // For more buttons for more playability
	public PanelContainer HomePanel { get; set; } // Main Buttons

	public void Quit()
	{
		GetTree().Root.GetNode<SettingsOperator>("/root/SettingsOperator").Quit();
	}
	public override void _Ready()
	{
		SettingsOperator = GetNode<SettingsOperator>("/root/SettingsOperator");
		SubButtons = GetNode<PanelContainer>("SubButton");
		HomePanel = GetNode<PanelContainer>("HomeButtonBG");
		SettingsOperator.loopaudio = false;
		SongIndicator = GetNode<Label>("SongIndication");
		AnimationPlayer ani = GetNode<AnimationPlayer>("Flash/AnimationPlayer");
		HomeButtonID.ID = -1;
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double _delta)
	{
		if (SettingsOperator.Gameplaycfg.TimeTotal - SettingsOperator.Gameplaycfg.Time < 0.1f && SettingsOperator.Beatmaps.Count > 0)
		{
			SettingsOperator.SelectSongID(SettingsOperator.RndSongID());
			AudioPlayer.Instance.Play();
		}
		SongIndicator.Position = new Vector2(SongIndicator.Position.X, SettingsOperator.TopPanelPosition + 10);
		_subunf();
	}
	private void _marathonmode()
	{ 
		GetNode<SceneTransition>("/root/Transition").Switch("res://Panels/Screens/MarathonMode.tscn");
	}
	private void _play()
	{
		GetNode<SceneTransition>("/root/Transition").Switch("res://Panels/Screens/song_select.tscn");
	}
#pragma warning disable CS0108 // Member hides inherited member; missing new keyword
    private void Multiplayer()
#pragma warning restore CS0108 // Member hides inherited member; missing new keyword
    {
		GetNode<SceneTransition>("/root/Transition").Switch("res://Panels/Screens/MultiplayerList.tscn");
	}
	private void _browse() {
		GetNode<SceneTransition>("/root/Transition").Switch("res://Panels/Screens/Browse.tscn");
	}
	private void _create() {
		#if DEBUG
			GetNode<SceneTransition>("/root/Transition").Switch("res://Panels/Screens/Create.tscn");
		#else
			Notify.Post("Under Construction!");
		#endif
	}
	private void _leave() {
		SettingsOperator.Quit();
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
