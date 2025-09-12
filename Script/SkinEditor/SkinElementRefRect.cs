using Godot;
using System;

public partial class SkinElementRefRect : ReferenceRect
{
    private Button TopLeft { get; set; }
    private Button TopCenter { get; set; }
    private Button TopRight { get; set; }
    private Button CenterRight { get; set; }
    private Button CenterLeft { get; set; }
    private Button BottomRight { get; set; }
    private Button BottomCenter { get; set; }
    private Button BottomLeft { get; set; }

    private bool BR = false;
    private Vector2 OldSize { get; set; }
    private Vector2 OldPosition { get; set; }

    public override void _Ready()
    {
        BottomRight = GetNode<Button>("BottomRight");
        Position = new Vector2(100, 100);
    }

    private void brsideedge(bool br)
    {
        OldPosition = Position;
        OldSize = Size;
        BR = br;
    }

    public override void _Process(double delta)
    {
        
    }
}
