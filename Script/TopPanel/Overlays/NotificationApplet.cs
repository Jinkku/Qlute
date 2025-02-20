using Godot;
using System;

public partial class NotificationApplet : Button
{
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	private void _pressed(){
		QueueFree();
	}
	public override void _Process(double delta)
	{
	}
}
