using Godot;
using System;

public partial class ScoreOverlay : Control
{
	// Called when the node enters the scene tree for the first time.
	public static SettingsOperator SettingsOperator { get; set; }
	public static Label pp { get; set; }
	public static Label score { get; set; }
	public override void _Ready()
	{
		SettingsOperator = GetNode<SettingsOperator>("/root/SettingsOperator");
		score = GetNode<Label>("Score");
		pp = GetNode<Label>("pp");
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		score.Text = SettingsOperator.Gameplaycfg["score"].ToString("00000000");
		pp.Text = SettingsOperator.Gameplaycfg["pp"].ToString("0,0") + "pp";
	}
}
