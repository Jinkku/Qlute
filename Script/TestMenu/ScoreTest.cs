using Godot;
using System;

public partial class ScoreTest : ColorRect
{
	private void _changed(float value)
	{
		SettingsOperator.Gameplaycfg.Score = (int)value;
	}
	private void _max(float value)
	{
		SettingsOperator.Gameplaycfg.Max = (int)value;
		SettingsOperator.Gameplaycfg.Combo = (int)value;
		Gameplay.ReloadppCounter();
		Gameplay.ReloadAccuracy();
	}

	private void _great(float value)
	{
		SettingsOperator.Gameplaycfg.Great = (int)value;
		Gameplay.ReloadppCounter();
		Gameplay.ReloadAccuracy();
	}
	private void _meh(float value)
	{
		SettingsOperator.Gameplaycfg.Meh = (int)value;
		Gameplay.ReloadppCounter();
		Gameplay.ReloadAccuracy();
	}
	private void _bad(float value)
	{
		SettingsOperator.Gameplaycfg.Bad = (int)value;
		Gameplay.ReloadppCounter();
		Gameplay.ReloadAccuracy();
	}

}
