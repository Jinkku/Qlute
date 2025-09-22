using Godot;
using System;

public partial class Details : PanelContainer
{
	private ProgressBar Difficulty { get; set; }
	private ProgressBar Accuracy { get; set; }
	private Label NoteCount { get; set; }
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		Accuracy = GetNode<ProgressBar>("Info/Row1/ProgressBar");
		Difficulty = GetNode<ProgressBar>("Info/Row2/ProgressBar");
		//NoteCount = GetNode<Label>("Info/Row3/Value");
		CheckValue();
	}

	private void CheckValue()
	{
		Accuracy.Value = SettingsOperator.Gameplaycfg.Accuracy;
		Difficulty.Value = SettingsOperator.LevelRating;
		//NoteCount.Text = SettingsOperator.LevelRating;
	}
	
	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		CheckValue();
	}
}
