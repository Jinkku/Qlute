using Godot;
using System;
using System.Collections.Generic;
using System.IO;
public partial class Main : Control
{
	// Called when the node enters the scene tree for the first time.
	public SettingsOperator SettingsOperator { get; set; }
	private void debugbuttonpressed(string path)
	{
		GD.Print("Button Pressed: " + path);
	}
	private int startmark {get;set;}
	public static List<Label> PostMarks = new List<Label>();
	public static List<ColorRect> GraphBars = new List<ColorRect>();
	public override void _Ready()
	{
	string Scenefolder = ProjectSettings.GlobalizePath("res://") + "Panels";
	SettingsOperator = GetNode<SettingsOperator>("/root/SettingsOperator");
	var panelContainer = GetNode<VBoxContainer>("./PanelContainer/ScrollContainer/VBoxContainer");
	var Marks = GetNode<ProgressBar>("Viewport/ProgressBar").Size.X/16;
	startmark = (int)GetNode<ProgressBar>("Viewport/ProgressBar").Position.X;
	var marky = (int)GetNode<ProgressBar>("Viewport/ProgressBar").Position.Y;
	var tick = 0;
	for (var i = 0; i*10 < Marks;i++){
		var Markings = new Label();
		var Bars = new ColorRect();
		Markings.Text = "0pp";
		Markings.Position = new Vector2(startmark + (GetNode<ProgressBar>("Viewport/ProgressBar").Size.X/10*i), marky-40);
		Bars.Position = new Vector2(startmark + (GetNode<ProgressBar>("Viewport/ProgressBar").Size.X/10*i), marky+40);
		Bars.Size = new Vector2(10,10);
		GetNode<Control>("Viewport").AddChild(Markings);
		GetNode<Control>("Viewport").AddChild(Bars);
		PostMarks.Add(Markings);
		GraphBars.Add(Bars);
	}
	foreach (BeatmapLegend file in SettingsOperator.Beatmaps)
	{
		var button = ResourceLoader.Load<PackedScene>("res://Panels/Testing/TestButtonpp.tscn").Instantiate().GetNode<Button>(".");
		button.Text = file.Title;
		button.SetMeta("SongID",tick);
//		button.Connect("pressed", this, "debugbuttonpressed");
		panelContainer.AddChild(button);
		tick++;
	}
	}
	

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}
}
