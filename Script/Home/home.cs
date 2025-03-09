using Godot;
using System;
using System.Diagnostics.Tracing;
using System.IO;
using System.Collections;
using System.Collections.Generic;



public partial class home : Control
{
	public Label SongTitle { get; set; }
	public Label SongArtist { get; set; }
	public static SettingsOperator SettingsOperator { get; set; }
	public override void _Ready()
	{	
		SettingsOperator = GetNode<SettingsOperator>("/root/SettingsOperator");
		SettingsOperator.loopaudio = false;
		SongTitle = GetNode<Label>("./Titlesong");
		SongArtist = GetNode<Label>("./Descsong");
		AnimationPlayer ani = GetNode<AnimationPlayer>("Flash/AnimationPlayer");
		//ani.Play("Flash");
		_Process(0);
	}
	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		SongTitle.Text = SettingsOperator.Sessioncfg["beatmaptitle"]?.ToString() ?? "No song selected";
		SongArtist.Text = SettingsOperator.Sessioncfg["beatmapartist"]?.ToString() ?? "";

		if (SettingsOperator.Gameplaycfg["timetotal"]-(AudioPlayer.Instance.GetPlaybackPosition()*1000) < -1000 && SettingsOperator.Beatmaps.Count>0)
		{

			SettingsOperator.SelectSongID(SettingsOperator.RndSongID());
			AudioPlayer.Instance.Play();
		}



	}
	private void _test(){
		SettingsOperator.Gameplaycfg["max"] = 1141;
		SettingsOperator.Gameplaycfg["great"] = 0;
		SettingsOperator.Gameplaycfg["meh"] = 0;
		SettingsOperator.Gameplaycfg["bad"] = 0;
		SettingsOperator.Gameplaycfg["maxcombo"] = 1141;
		GetNode<ApiOperator>("/root/ApiOperator").SubmitScore();
	}
	private void _play(){
		GetNode<SceneTransition>("/root/Scene").Switch("res://Panels/Screens/song_select.tscn");
	}
	private void _browse(){
		GetNode<SceneTransition>("/root/Scene").Switch("res://Panels/Screens/Browse.tscn");
	}
	private void _create(){
		GetNode<SceneTransition>("/root/Scene").Switch("res://Panels/Screens/Create.tscn");
	}
	private void _leave(){
		GetTree().Quit();
		}
	
}
