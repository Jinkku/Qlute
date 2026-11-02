using Godot;
using System;

public partial class ScoreCounter : Label
{
	private int _last = int.MinValue;
	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		int score = SettingsOperator.Gameplaycfg.Score;
		if (score == _last) return;
		_last = score;
		Text = score.ToString();
	}
}
