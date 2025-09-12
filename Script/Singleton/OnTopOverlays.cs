using Godot;
using System;

public partial class OnTopOverlays : CanvasLayer
{
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
        Node TopPanel = GD.Load<PackedScene>("res://Panels/Overlays/TopPanel.tscn").Instantiate();
        AddChild(TopPanel); // Adds the Top Panel indicator
        Node FpsIndicator = GD.Load<PackedScene>("res://Panels/Overlays/fps_counter.tscn").Instantiate();
        AddChild(FpsIndicator); // Adds FPS Counter
        Node Cursor = GD.Load<PackedScene>("res://Panels/Overlays/Cursor.tscn").Instantiate();
        AddChild(Cursor); // Adds FPS Counter
	}
}
