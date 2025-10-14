using Godot;
using System;

public partial class OnTopOverlays : CanvasLayer
{
        // Called when the node enters the scene tree for the first time.
        public override void _Ready()
        {
                Node TopPanel = GD.Load<PackedScene>("res://Panels/TopPanelv2/TopPanelv2.tscn").Instantiate();
                AddChild(TopPanel); // Adds the Top Panel indicator
                Node FpsIndicator = GD.Load<PackedScene>("res://Panels/Overlays/fps_counter.tscn").Instantiate();
                AddChild(FpsIndicator); // Adds FPS Counter
                ProcessMode = ProcessModeEnum.Always;
        }

        private int tick { get; set; } = -1;
        public override void _Process(double delta)
        {
                if (Cursor.CursorVisible && tick != 1)
                {
                        tick = 1;
                        Input.SetMouseMode(Input.MouseModeEnum.Visible);
                }
                else if (!Cursor.CursorVisible && tick != 0)
                {
                        tick = 0;
                        Input.SetMouseMode(Input.MouseModeEnum.Hidden);
                }
        }
}
