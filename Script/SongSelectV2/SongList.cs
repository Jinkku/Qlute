using Godot;
using System;

public partial class SongList : Control
{
	private int _resizePos = 0;
	private void _AdjustPos()
	{
		Position = new Vector2(
			0,
			_resizePos / 2 - 40 + SettingsOperator.TopPanelPosition
		);
	}
	// Set the resize pos for Pos func

	private void _resize()
	{
		_resizePos = (int)Size.Y;
		
	}
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		_resize();
		_AdjustPos();
	}
	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double _delta)
	{
		_AdjustPos();
	}
}
