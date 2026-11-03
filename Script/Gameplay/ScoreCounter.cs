using Godot;

public partial class ScoreCounter : Label
{
	private int _last = int.MinValue;

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		// Gameplay already animates Gameplaycfg.Score (and computes it with the live mod multiplier,
		// so it's also correct in LAN matches, which skip song select).
		int score = SettingsOperator.Gameplaycfg.Score;
		if (score == _last) return;
		_last = score;
		Text = score.ToString();
	}
}
