using Godot;
using System;
public partial class ModsMulti : Label
{
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{_Process(0);}
	public static float multiplier {get;set;}
	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double _delta)
	{
		multiplier = new ModsOperator().ProcessMultiplierByModList(ModsOperator.Mods);
		Text = multiplier.ToString("0.00")+"x";
	}
}
