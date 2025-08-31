using Godot;
using System;

public partial class SkinEditor : Control
{
	Node currentScene;
	private SettingsOperator SettingsOperator { get; set; }
	private VBoxContainer Menu { get; set; }
	public override void _Ready()
	{
		currentScene = GetTree().CurrentScene;
		SettingsOperator = GetNode<SettingsOperator>("/root/SettingsOperator");
		Menu = GetNode<VBoxContainer>("Creativity/VBoxContainer/ScrollContainer/Elements");
		if (SettingsOperator.TopPanelPosition > 49)
		{
			SettingsOperator.toppaneltoggle();
		}
		Check();
	}
	private bool MissingFunction { get; set; } = false;
	private Container MissingDisclaimer { get; set; } = null;

	private void Check() {
		if (GetTree().CurrentScene == null && MissingFunction == false)
		{
			MissingFunction = true;
			Menu.Hide();
		}
		else if (GetTree().CurrentScene.Name == "Gameplay" && MissingFunction == true)
		{
			MissingFunction = false;
			Menu.Show();
		}
		else if (GetTree().CurrentScene.Name != "Gameplay" && MissingFunction == false)
		{
			MissingFunction = true;
			Menu.Hide();
		}
	}
	public override void _Process(double delta)
	{
		Check();

	}

	private void _on_back()
	{
		Global._SkinStart = !Global._SkinStart;
	}
}
