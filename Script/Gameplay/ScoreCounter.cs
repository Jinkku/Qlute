using Godot;
using System;
using Game;

public partial class ScoreCounter : Label
{
	private int _last = int.MinValue;
	private readonly ScoreCalculator _calc = new();
	private Tween _tween;
	// Animated display value used while in a LAN match.
	public int DisplayScore { get; set; }

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		int score;
		if (LanNetwork.InMatch)
		{
			// LAN skips song select, so ModsMulti.multiplier is never set (stays 0) and
			// Gameplaycfg.Score is always 0. Compute the score here with the real multiplier.
			int target = _calc.ProcessScore(SettingsOperator.Gameplaycfg.Max,
				SettingsOperator.Gameplaycfg.Great, SettingsOperator.Gameplaycfg.Meh,
				SettingsOperator.Gameplaycfg.NoteCount,
				ModsOperator.ProcessMultiplierByModList(ModsOperator.Mods));
			if ((_tween == null || !_tween.IsRunning()) && DisplayScore != target)
			{
				_tween?.Kill();
				_tween = CreateTween();
				_tween.TweenProperty(this, "DisplayScore", target, 0.3f);
			}
			score = DisplayScore;
		}
		else
		{
			score = SettingsOperator.Gameplaycfg.Score;
		}
		if (score == _last) return;
		_last = score;
		Text = score.ToString();
	}
}
