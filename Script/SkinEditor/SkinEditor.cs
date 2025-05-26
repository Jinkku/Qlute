using Godot;
using System;

public partial class SkinEditor : Control
{
	// Called when the node enters the scene tree for the first time.
	private void _on_back()
	{
		Global._SkinStart = !Global._SkinStart;
	}
}
