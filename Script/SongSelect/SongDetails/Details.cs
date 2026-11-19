using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

/// <summary>
/// Song select's Details tab (Panels/SongSelectElements/DetailsTab.tscn): the selected difficulty's level badge,
/// a note density graph, level / accuracy meters and a few stat tiles. Layout lives in the scene; parts are
/// found by unique name. Speed mods (DT/HT) are applied to the times and densities shown.
/// </summary>
public partial class Details : PanelContainer
{
	[Export] public Color TextLight { get; set; } = Colors.White;
	[Export] public Color TextDark { get; set; } = new("#1b1620");
	/// <summary>Colour of the level when mods change it.</summary>
	[Export] public Color ModColor { get; set; } = new("#c7b8ff");
	/// <summary>Accuracy meter fill, left end to right end.</summary>
	[Export] public Color AccuracyFrom { get; set; } = new("#8f7bff");
	[Export] public Color AccuracyTo { get; set; } = new("#ff7bd5");

	private PanelContainer _badge;
	private Label _badgeText, _diffName, _mapper, _modeText;
	private DensityGraph _graph;
	private Label _peakText, _startText, _endText;
	private StatBar _levelBar, _accBar;
	private Label _levelValue, _accValue;
	private Label _notes, _window, _avg, _peak;
	private Control _accRow;

	private (int song, string alias, double rate, int mode) _shownKey = (-2, null, 0, -1);

	public override void _Ready()
	{
		_badge = GetNode<PanelContainer>("%Badge");
		_badgeText = GetNode<Label>("%BadgeText");
		_diffName = GetNode<Label>("%DiffName");
		_mapper = GetNode<Label>("%Mapper");
		_modeText = GetNode<Label>("%ModeText");
		_graph = GetNode<DensityGraph>("%Graph");
		_peakText = GetNode<Label>("%PeakText");
		_startText = GetNode<Label>("%StartTime");
		_endText = GetNode<Label>("%EndTime");
		_levelBar = GetNode<StatBar>("%LevelBar");
		_levelValue = GetNode<Label>("%LevelValue");
		_accBar = GetNode<StatBar>("%AccuracyBar");
		_accValue = GetNode<Label>("%AccuracyValue");
		_accRow = GetNode<Control>("%AccuracyRow");
		_notes = GetNode<Label>("%NotesValue");
		_window = GetNode<Label>("%HitWindowValue");
		_avg = GetNode<Label>("%AverageValue");
		_peak = GetNode<Label>("%PeakValue");
		_badge.AddThemeStyleboxOverride("panel", (StyleBox)_badge.GetThemeStylebox("panel").Duplicate());
		Refresh();
	}

	public override void _Process(double delta)
	{
		if (IsVisibleInTree()) Refresh();
	}

	/// <summary>Rebuilds the tab when the selection, mods, speed or mode change (a cheap check otherwise).</summary>
	private void Refresh()
	{
		int id = SettingsOperator.SessionConfig.SongID;
		string alias = ModsOperator.AliasOf(ModsOperator.Mods);
		double rate = Game.PerformanceCalculator.GetRate(alias, AudioPlayer.Instance?.PitchScale ?? 1);
		var key = (id, alias, rate, SettingsOperator.SessionConfig.GameMode);
		if (key == _shownKey) return;
		_shownKey = key;
		if (id < 0 || id >= SettingsOperator.Beatmaps.Count) ShowEmpty();
		else Fill(SettingsOperator.Beatmaps[id], alias, rate);
	}

	private void ShowEmpty()
	{
		_badgeText.Text = "Lv -";
		_diffName.Text = "No beatmap selected";
		_mapper.Text = _modeText.Text = "";
		_graph.Set(Array.Empty<int>(), 1, Colors.White);
		_peakText.Text = _startText.Text = _endText.Text = "";
		_levelBar.Set(0, Colors.White);
		_accBar.Set(0, Colors.White);
		_levelValue.Text = _accValue.Text = "";
		_notes.Text = _window.Text = _avg.Text = _peak.Text = "-";
	}

	private void Fill(BeatmapLegend b, string alias, double rate)
	{
		float level = b.Levelrating;
		var c = SettingsOperator.ReturnLevelColour((int)Math.Clamp(level, 0, 99));

		// header
		if (_badge.GetThemeStylebox("panel") is StyleBoxFlat badge) badge.BgColor = c;
		_badgeText.AddThemeColorOverride("font_color", CarouselCard.TextOn(c, TextLight, TextDark));
		_badgeText.Text = $"Lv {level:0}";
		_diffName.Text = b.Version ?? "";
		_mapper.Text = string.IsNullOrEmpty(b.Mapper) ? "" : $"mapped by {b.Mapper}";
		_modeText.Text = SettingsOperator.SessionConfig.GameMode switch
		{
			1 => "Taiko",
			3 => "Tap!",
			_ => "4K Mania", // every map plays on 4 lanes (other key counts are folded onto them)
		};

		// density: the notes pp is rated on (song select's pp display has already loaded them)
		var times = b.RatedNotes.Select(n => n.time).ToList();
		times.Sort();
		float end = times.Count > 0 ? times[^1] : b.Timetotal;
		_graph.Set(times, end, c);
		double peak = PeakPerSecond(times) * rate;
		double drain = times.Count > 1 ? (times[^1] - times[0]) / 1000.0 / rate : 0;
		double avg = drain > 0 ? times.Count / drain : 0;
		_peakText.Text = times.Count > 0 ? $"peak {peak:0.#} notes/s" : "no notes";
		_startText.Text = "0:00";
		_endText.Text = Clock(end / 1000.0 / rate);

		// meters
		int baseLevel = (int)Math.Round(level);
		int modLevel = alias.Length > 0 ? SettingsOperator.GetLevelWithMods() : baseLevel;
		bool modded = modLevel != baseLevel;
		_levelBar.Set(modded ? modLevel : level, x => SettingsOperator.ReturnLevelColour((int)(x * 100)));
		_levelValue.Text = modded ? $"{baseLevel} → {modLevel}" : $"{baseLevel}";
		_levelValue.AddThemeColorOverride("font_color", modded ? ModColor : TextLight);
		_accBar.Set(b.Accuracy, x => AccuracyFrom.Lerp(AccuracyTo, x));
		_accValue.Text = $"{b.Accuracy:0.#}";
		_accRow.TooltipText = $"Overall difficulty {b.Accuracy:0.#} / 10: higher means tighter timing";

		// tiles
		_notes.Text = $"{b.NoteCount:N0}";
		_window.Text = $"±{PerfectWindow(b.Accuracy):0}ms";
		_avg.Text = $"{avg:0.0}/s";
		_peak.Text = $"{peak:0.0}/s";
	}

	/// <summary>Half-width of the Perfect window in ms, as the selected mode judges it (Gameplay / Taiko / Tap!).</summary>
	private static float PerfectWindow(float od) => SettingsOperator.SessionConfig.GameMode == 3
		? 80f - 6f * od
		: Math.Max(16, (int)(SettingsOperator.PerfectJudgeMin * (1f - (int)od / 10f)));

	/// <summary>Most notes in any one-second window (times sorted).</summary>
	private static int PeakPerSecond(List<int> times)
	{
		int best = 0;
		for (int i = 0, j = 0; i < times.Count; i++)
		{
			while (times[i] - times[j] >= 1000) j++;
			best = Math.Max(best, i - j + 1);
		}
		return best;
	}

	private static string Clock(double seconds)
	{
		var t = TimeSpan.FromSeconds(Math.Max(0, seconds));
		return t.TotalHours >= 1 ? t.ToString(@"h\:mm\:ss") : $"{(int)t.TotalMinutes}:{t.Seconds:00}";
	}
}
