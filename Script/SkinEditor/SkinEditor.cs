using Godot;
using System;

public partial class SkinEditor : Control
{
	Node currentScene;
	private SettingsOperator SettingsOperator { get; set; }
	public override void _Ready()
	{
		currentScene = GetTree().CurrentScene;
		SettingsOperator = GetNode<SettingsOperator>("/root/SettingsOperator");
		if (SettingsOperator.TopPanelPosition > 49)
		{
			SettingsOperator.toppaneltoggle();
		}
		
	}

	private void _on_back()
	{
		Global._SkinStart = !Global._SkinStart;
	}
}
