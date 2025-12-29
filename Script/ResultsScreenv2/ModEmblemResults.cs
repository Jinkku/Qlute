using Godot;
using System;

public partial class ModEmblemResults : Label
{
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		if (HasMeta("ModName"))
			Text = GetMeta("ModName").ToString();
		else
			Text = "??";
	}
}
