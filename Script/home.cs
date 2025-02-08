using Godot;
using System;
using System.Diagnostics.Tracing;
using System.IO;
using System.Collections;
using System.Collections.Generic;



public partial class home : Node
{
	public Label SongTitle { get; set; }
	public Label SongArtist { get; set; }
	public static SettingsOperator SettingsOperator { get; set; }
	public override void _Ready()
	{	
		SettingsOperator = GetNode<SettingsOperator>("/root/SettingsOperator");
		SongTitle = GetNode<Label>("./Titlesong");
		SongArtist = GetNode<Label>("./Descsong");
		_Process(0);
	}
	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		SongTitle.Text = SettingsOperator.Sessioncfg["beatmaptitle"]?.ToString() ?? "No song selected";
		SongArtist.Text = SettingsOperator.Sessioncfg["beatmapartist"]?.ToString() ?? "";
	}
	private void _play(){
		GetTree().ChangeSceneToFile("res://Panels/Screens/song_select.tscn");
	}
	private void _leave(){
		GetTree().Quit();
		}
	
}
