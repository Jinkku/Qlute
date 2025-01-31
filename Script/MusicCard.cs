using Godot;
using System;

public partial class MusicCard : Button
{
	// Called when the node enters the scene tree for the first time.
	public Button self { get ;set; }

	public override void _Ready()
	{
		self = GetNode<Button>(".");
	}

	
	public void _on_pressed(){
		GD.Print("Pressed");
	}
	public override void _Process(double _delta)
	{
		

	}
}
