using Godot;
using System;

public partial class Create : Control
{
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	private void _on_back(){
		GetNode<SceneTransition>("/root/Transition").Switch("res://Panels/Screens/home_screen.tscn");

	}
	private void _enter()
	{
		var s = GetNode<Label>("Info/ContextSections/Na");
		s.Text = "on!";
	}
		private void _off()
	{
		var s = GetNode<Label>("Info/ContextSections/Na");
		s.Text = "off";
	}
	public override void _Process(double delta)
	{
	}
}
