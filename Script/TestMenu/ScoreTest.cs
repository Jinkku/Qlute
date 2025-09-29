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
		SettingsOperator.Gameplaycfg.Accuracy = Gameplay.ReloadAccuracy(SettingsOperator.Gameplaycfg.Max, SettingsOperator.Gameplaycfg.Great, SettingsOperator.Gameplaycfg.Meh, SettingsOperator.Gameplaycfg.Bad);
	}

	private void _great(float value)
	{
		SettingsOperator.Gameplaycfg.Great = (int)value;
		Gameplay.ReloadppCounter();
		SettingsOperator.Gameplaycfg.Accuracy = Gameplay.ReloadAccuracy(SettingsOperator.Gameplaycfg.Max, SettingsOperator.Gameplaycfg.Great, SettingsOperator.Gameplaycfg.Meh, SettingsOperator.Gameplaycfg.Bad);
	}
	private void _meh(float value)
	{
		SettingsOperator.Gameplaycfg.Meh = (int)value;
		Gameplay.ReloadppCounter();
		SettingsOperator.Gameplaycfg.Accuracy = Gameplay.ReloadAccuracy(SettingsOperator.Gameplaycfg.Max, SettingsOperator.Gameplaycfg.Great, SettingsOperator.Gameplaycfg.Meh, SettingsOperator.Gameplaycfg.Bad);
	}
	private void _bad(float value)
	{
		SettingsOperator.Gameplaycfg.Bad = (int)value;
		Gameplay.ReloadppCounter();
		SettingsOperator.Gameplaycfg.Accuracy = Gameplay.ReloadAccuracy(SettingsOperator.Gameplaycfg.Max, SettingsOperator.Gameplaycfg.Great, SettingsOperator.Gameplaycfg.Meh, SettingsOperator.Gameplaycfg.Bad);
	}
}
