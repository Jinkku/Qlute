using Godot;
using System;
using System.Diagnostics.Tracing;
using System.IO;
using System.Collections;
using System.Collections.Generic;



public partial class home : Node
{
	public override void _Ready()
	{	
	}
	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}
	private void _play(){
		GetTree().ChangeSceneToFile("res://Panels/Screens/song_select.tscn");
	}
	private void _leave(){
		GetTree().Quit();
		}
	
}
