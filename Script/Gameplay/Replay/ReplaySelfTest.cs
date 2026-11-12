using Godot;
using System;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Dev-only: plays a replay through the real mania judging and prints the result, so it can be
/// compared with the server's ppv2calc.py (they must match exactly). Run headless:
///   ... --headless --path /Storage/Qlute -- --replay-test=/path/file.qrf [--replay-test-version=DiffName]
/// Prints one "[REPLAYTEST] result ..." line and quits. Use an XDG_DATA_HOME sandbox.
/// </summary>
public partial class ReplaySelfTest : Node
{
	private static readonly string[] Args = OS.GetCmdlineUserArgs();
	private static string Value(string prefix) => Args.FirstOrDefault(a => a.StartsWith(prefix))?[prefix.Length..];
	public static bool Enabled => Value("--replay-test=") != null;

	private double _t;
	private bool _started, _done;

	public override void _Ready() => ProcessMode = ProcessModeEnum.Always;

	public override void _Process(double delta)
	{
		_t += delta;
		if (_t > 600) { GD.Print("[REPLAYTEST] timeout"); GetTree().Quit(1); return; }
		if (!_started && _t > 3 && SettingsOperator.Beatmaps.Count > 0) Start();
		if (_started && (int)(_t / 10) != (int)((_t - delta) / 10))
			GD.Print($"[REPLAYTEST] t={_t:0} song={SettingsOperator.Gameplaycfg.Time:0.00}/{SettingsOperator.Gameplaycfg.TimeTotalGame:0.00} playing={AudioPlayer.Instance.Playing} inst={Gameplay.Instance != null} started={Gameplay.Instance?.songstarted} paused={GetTree().Paused} scene={GetTree().CurrentScene?.Name}");
		if (_started && !_done && Gameplay.Instance is { Finished: true })
		{
			_done = true;
			GD.Print(string.Create(CultureInfo.InvariantCulture,
				$"[REPLAYTEST] result P{SettingsOperator.Gameplaycfg.Max} G{SettingsOperator.Gameplaycfg.Great} M{SettingsOperator.Gameplaycfg.Meh} B{SettingsOperator.Gameplaycfg.Bad} pp={SettingsOperator.Gameplaycfg.pp:R}"));
			GetTree().Quit();
		}
	}

	private void Start()
	{
		_started = true;
		string path = Value("--replay-test=");
		var header = System.IO.File.ReadAllLines(path).Where(l => l.StartsWith('#') && l.Contains(": "))
			.ToDictionary(l => l[1..l.IndexOf(": ")], l => l[(l.IndexOf(": ") + 2)..]);
		int id = int.Parse(header["BeatmapID"]);
		string version = Value("--replay-test-version=");
		int index = SettingsOperator.Beatmaps.FindIndex(b => b.BeatmapID == id && (version == null || b.Version == version));
		if (index < 0) { GD.Print($"[REPLAYTEST] beatmap {id} not found"); GetTree().Quit(1); return; }
		ModsOperator.SetMods((header.TryGetValue("Mods", out var mods) ? mods : ""));
		ModsOperator.Mods["no-fail"] = true; // a fail would stop the run; compare against the server with NF
		SettingsOperator.Marathon = false;
		GetNode<SettingsOperator>("/root/SettingsOperator").SelectSongID(index);
		SettingsOperator.SessionConfig.GameMode = 0;
		Replay.ReloadReplay(path); // sets SpectatorMode
		GD.Print($"[REPLAYTEST] playing {SettingsOperator.Beatmaps[index].Title} [{SettingsOperator.Beatmaps[index].Version}]");
		GetNode<SceneTransition>("/root/Transition").Switch("res://Panels/Screens/SongLoadingScreen.tscn");
	}
}
