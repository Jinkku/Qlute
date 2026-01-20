using Godot;
using System;

public partial class Cursor : Sprite2D
{
	public static bool CursorVisible { get; set; } = true;
	private Window Popup { get; set; }
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		Popup = GetNode<Window>("../");
		Input.SetMouseMode(Input.MouseModeEnum.Hidden);
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	private Vector2 oldpos { get; set; }

    public override void _PhysicsProcess(double delta)
    {;
		if (oldpos != SettingsOperator.MouseMovement)
		{
			Popup.Position = new Vector2I((int)(SettingsOperator.MouseMovement.X + 1), (int)(SettingsOperator.MouseMovement.Y + 1));
		}
		Visible = CursorVisible;
		Popup.Visible = CursorVisible;
    }

    public override void _ExitTree()
    {
		Input.SetMouseMode(Input.MouseModeEnum.Visible);
    }
}
