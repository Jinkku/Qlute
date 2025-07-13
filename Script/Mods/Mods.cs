using Godot;
using System;

public partial class Mods : PanelContainer
{
	public static SettingsOperator SettingsOperator { get; set; }
	private GridContainer ModEntry { get; set; }
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		SettingsOperator = GetNode<SettingsOperator>("/root/SettingsOperator");
		ModEntry = GetNode<GridContainer>("ModsEntries");
	}

	private void _on_resized()
	{
		if (ModEntry != null) ModEntry.Columns = (int)(Size.X / 404);
	}
	private void _reset()
	{
		ModsOperator.Reset();
		GD.Print("Mods Reset");
	}
}
