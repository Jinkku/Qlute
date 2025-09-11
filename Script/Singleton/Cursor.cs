using Godot;
using System;

public partial class Cursor : Sprite2D
{
	public static bool CursorVisible { get; set; } = true;
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		Input.SetMouseMode(Input.MouseModeEnum.Hidden);
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		Position = GetGlobalMousePosition();
		Visible = CursorVisible;
	}

    public override void _ExitTree()
    {
		Input.SetMouseMode(Input.MouseModeEnum.Visible);
    }
}
