using Godot;
using System;
using System.Collections.Generic;

public partial class ModsShown : HBoxContainer
{
	// Called every frame. 'delta' is the elapsed time since the previous frame.
	private List<string> _mods = new List<string>();
	private void _reset()
	{
		foreach (string mod in ModsOperator.ModsEnabled)
		{
			if (!_mods.Contains(mod))
			{
				_mods.Add(mod);
				// Load the mod emblem scene and instantiate it
				var modd = GD.Load<PackedScene>("res://Panels/Overlays/ModEmblem.tscn").Instantiate().GetNode<PanelContainer>(".");
				modd.SetMeta("ModName", mod);
				modd.UseParentMaterial = true;
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
	public override void _Ready()
	{
		_reset();
	}
	public override void _Process(double delta)
	{
		_reset();
	}
}
