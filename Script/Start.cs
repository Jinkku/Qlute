using Godot;
using System;

public partial class Start : Button
{
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public void _Start(){
		GetTree().ChangeSceneToFile("res://Panels/Screens/Gameplay.tscn");
	}
	public override void _Process(double delta)
	{
	}
}
