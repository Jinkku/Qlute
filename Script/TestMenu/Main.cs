using Godot;
using System;
using System.IO;
public partial class Main : Control
{
	// Called when the node enters the scene tree for the first time.
	public SettingsOperator SettingsOperator { get; set; }
	private void debugbuttonpressed(string path)
	{
		GD.Print("Button Pressed: " + path);
	}
	public override void _Ready()
	{
	string Scenefolder = ProjectSettings.GlobalizePath("res://") + "Panels";
	SettingsOperator = GetNode<SettingsOperator>("/root/SettingsOperator");
	var panelContainer = GetNode<VBoxContainer>("./PanelContainer/ScrollContainer/VBoxContainer");
	string[] allfiles = Directory.GetFiles(Scenefolder, "*.tscn", SearchOption.AllDirectories);
	foreach (var file in allfiles)
	{
		var button = new Button();
		button.Text = file.Replace(Scenefolder, "").Replace(".tscn", "");
		button.SetMeta("path", file);
//		button.Connect("pressed", this, "debugbuttonpressed");
		panelContainer.AddChild(button);
	}
	}
	

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}
}
