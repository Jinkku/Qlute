using Godot;
using System;
using System.Collections.Generic;

public partial class ModsResultsScreenShowcase : HBoxContainer
{
	private List<string> _mods = new List<string>();

	private void reset()
	{
		
		foreach (string mod in ModsOperator.ModsEnabled)
		{
			if (!_mods.Contains(mod))
			{
				_mods.Add(mod);
				// Load the mod emblem scene and instantiate it
				var modd = GD.Load<PackedScene>("res://Panels/Overlays/ModEmblemResults.tscn").Instantiate().GetNode<Label>(".");
				modd.SetMeta("ModName", mod);
				AddChild(modd);
			}
		}
		foreach (var mod in GetChildren())
		{
			if (!ModsOperator.ModsEnabled.Contains(mod.GetMeta("ModName").ToString()))
			{
				mod.QueueFree();
				_mods.Remove(mod.GetMeta("ModName").ToString());
			}
		}
	}
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		reset();
	}

	public override void _PhysicsProcess(double delta)
	{
		reset();
	}
}
