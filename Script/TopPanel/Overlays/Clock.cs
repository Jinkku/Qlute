using Godot;
using System;

public partial class Clock : Label
{
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		Text = DateTime.Now.ToString("HH:mm:ss tt")+"\n" + DateTime.Now.ToString("yyyy-MM-dd");
	}
}
