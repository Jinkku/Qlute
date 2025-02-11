using Godot;
using System;

public partial class TopPanelOnTop : CanvasLayer
{
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
        Node TopPanel = GD.Load<PackedScene>("res://Panels/Overlays/TopPanel.tscn").Instantiate();
        AddChild(TopPanel);
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double _delta)
	{
	}
}
