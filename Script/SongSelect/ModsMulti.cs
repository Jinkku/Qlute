using Godot;
using System;

public partial class ModsMulti : Label
{
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{_Process(0);}
	public static double multiplier {get;set;}
	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double _delta)
	{
		multiplier= 1.0f;
		foreach (var mod in ModsOperator.Mods)
		{
			if (mod.Value && mod.Key == "dt")
			{
				multiplier*= 1.15;
			}else if (mod.Value && mod.Key == "ht"){
				multiplier*=0.3;
			}else if (mod.Value && mod.Key == "black-out"){
				multiplier*=1.15;
			}else if (mod.Value && mod.Key != "auto" && mod.Key != "random"){
				multiplier*= 1.05;
			}
		}
		Text = multiplier.ToString("0.00")+"x";
	}
}
