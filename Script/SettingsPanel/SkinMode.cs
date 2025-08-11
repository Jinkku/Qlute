using Godot;
using System;

public partial class SkinMode : OptionButton
{
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		foreach (var skin in Skin.List)
		{
			AddItem(skin.Name);
		}
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}
}
