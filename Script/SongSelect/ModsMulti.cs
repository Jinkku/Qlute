using Godot;
using System;

public partial class ModsMulti : Label
{
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{_Process(0);}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double _delta)
	{
		var multip= 1.0;
		foreach (var mod in ModsOperator.Mods)
		{
			if (mod.Value && mod.Key != "auto")
			{
				multip++;
			}
		}
		Text = multip.ToString("N0")+"x";
	}
}
