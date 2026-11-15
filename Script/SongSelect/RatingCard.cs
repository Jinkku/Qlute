using Godot;
using System;

/// <summary>
/// "Lv. N" on a song card. Same level as song select and the server (PerformanceCalculator.LevelFromPp of the
/// SS pp, mods included): the selected map uses the exact value; other cards don't have their notes loaded, so
/// DT/HT is estimated as pp x rate^2.8 (pp grows with stars^2.8) and the mods' pp factors are applied exactly.
/// </summary>
public partial class RatingCard : Label
{
	private int SongID { get; set; }
	private (string alias, double rate, bool selected) _key = (null, -1, false);

	public override void _Ready()
	{
		SongID = (int)GetNode<MusicCard>("../../../../../").SongID;
	}

	public override void _Process(double delta)
	{
		if (SongID < 0 || SongID >= SettingsOperator.Beatmaps.Count) return;
		string alias = ModsOperator.AliasOf(ModsOperator.Mods);
		double rate = Game.PerformanceCalculator.GetRate(alias, AudioPlayer.Instance.PitchScale);
		bool selected = SettingsOperator.SessionConfig.SongID == SongID;
		var key = (alias, rate, selected);
		if (key == _key) return;
		_key = key;
		int level = selected
			? SettingsOperator.GetLevelWithMods()
			: Game.PerformanceCalculator.LevelFromPp(SettingsOperator.Beatmaps[SongID].pp
				* Game.PerformanceCalculator.PpMultiplier(alias) * Math.Pow(rate, 2.8));
		Text = $"Lv. {level:N0}";
	}
}
