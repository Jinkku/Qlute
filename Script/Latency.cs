using Godot;
using System;

public partial class Latency : Label
{
	// Called when the node enters the scene tree for the first time.
	Label self { get; set; }
	public override void _Ready()
	{
		self = GetNode<Label>(".");
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		var lat = (1/Engine.GetFramesPerSecond())/0.001;
		self.Text = "\n" + lat.ToString("0.00")+ "ms";
	}
}
