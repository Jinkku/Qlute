using Godot;
using System;

public partial class TopPanel : PanelContainer
{
	private void _AdjustPos()
	{
		Position = new Vector2(
			0,
			SettingsOperator.TopPanelPosition
		);
	}
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{_AdjustPos();}
	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double _delta)
	{_AdjustPos();}
}
