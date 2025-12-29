using Godot;
using System;
using System.Collections.Generic;

public partial class ModsResultsScreenShowcase : HBoxContainer
{
	[Export]
	public bool External { get; set; }

	[Export] public string ExternalMods { get; set; } = "";
	private List<string> ModsL { get; set; }
	private List<string> _mods = new List<string>();
	[Export] public bool IsExternalLoaded { get; set; }
	
	private void reset()
	{
		if (External && ExternalMods != null)
		{
			ModsL = ModsOperator.ParseModAlias(ExternalMods);
			IsExternalLoaded = true;
		}
		else
		{
			ModsL = ModsOperator.ModsEnabled;
			IsExternalLoaded = false;
		}
		foreach (string mod in ModsL)
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
			if (!ModsL.Contains(mod.GetMeta("ModName").ToString()))
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
