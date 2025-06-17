using Godot;
using System;

public partial class SongSelectV2 : Control
{
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
	}
	private void _Exit()
	{
		GetNode<SceneTransition>("/root/Transition").Switch("res://Panels/Screens/home_screen.tscn");
	}
	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}
}
