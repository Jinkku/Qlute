using Godot;
using System;

public partial class SkinEditor : Control
{
	Node currentScene;
	private SettingsOperator SettingsOperator { get; set; }
	private VBoxContainer Menu { get; set; }
	private HBoxContainer ViewPoints { get; set; }
	public override void _Ready()
	{
		currentScene = GetTree().CurrentScene;
		SettingsOperator = GetNode<SettingsOperator>("/root/SettingsOperator");
		Menu = GetNode<VBoxContainer>("Creativity/VBoxContainer/ScrollContainer/Elements");
		ViewPoints = GetNode<HBoxContainer>("ToolBar/VBoxContainer/PartA/ViewPoints");
		if (SettingsOperator.TopPanelPosition > 49)
		{
			SettingsOperator.toppaneltoggle();
		}
		Check();
	}
	private bool MissingFunction { get; set; } = false;
	private Container MissingDisclaimer { get; set; } = null;

	private void Check()
	{
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

	private PanelContainer Settings { get; set; }
	private void _Game()
	{
		if (Settings != null)
		{
			Settings.QueueFree();
			Settings = null;
		}
	}
	private void _Settings()
	{
		if (Settings == null)
		{
			Settings = GD.Load<PackedScene>("res://Panels/SkinEditorElements/Settings.tscn").Instantiate().GetNode<PanelContainer>(".");
			AddChild(Settings);
			Settings.Size = new Vector2(Settings.Size.X, Settings.Size.Y - 150);
			Settings.Position = new Vector2(Settings.Position.X, 100);
		}
	}
	
}
