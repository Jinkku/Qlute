using Godot;
using System;
using System.Linq;

public partial class Debugitem2 : Label
{
	// Called every frame. 'delta' is the elapsed time since the previous frame.
	private bool _isDebugEnabled = false;
	private SettingsOperator SettingsOperator { get; set; }
	private bool letgo = false;
	public override void _Ready()
	{
		SettingsOperator = GetNode<SettingsOperator>("/root/SettingsOperator");
		Visible = false; // Start hidden
	}
	public override void _Process(double delta)
	{
		if (Input.IsKeyPressed(Key.F3) && !letgo) // Toggle debug information with F3 key
		{
			letgo = true;
			_isDebugEnabled = !_isDebugEnabled;
			Visible = _isDebugEnabled;
		}else if (!Input.IsKeyPressed(Key.F3) && letgo)
		{
			letgo = false; // Reset letgo when the key is released
		}
		var moka = 0;
		try
		{
			moka = Replay.ReplayCache.ElementAt(Gameplay.ReplayINT).Time;
		} catch {
			moka = 0;
		}
		if (Visible)
		{
			Text = "Debug Infomation\n" +
				"FPS: " + Engine.GetFramesPerSecond() + "\n" +
				"Memory Usage: " + Godot.OS.GetStaticMemoryUsage() + " bytes\n" +
				"Scene: " + GetTree().CurrentScene.Name + "\n" +
				"Song ID Highlighted: " + SettingsOperator.SongIDHighlighted + "\n" +
				"Song ID: " + SettingsOperator.Sessioncfg["SongID"].ToString() + "\n" +
				"pp: " + SettingsOperator.Gameplaycfg.pp + "\n" +
				"Max pp: " + SettingsOperator.Gameplaycfg.maxpp * ModsMulti.multiplier + "\n" +
				"Score: " + SettingsOperator.Gameplaycfg.Score + "\n" +
				"Timeframe: " + AudioPlayer.Instance.GetPlaybackPosition() / 0.001 + "\n" +
				"Object: " + Performance.GetMonitor(Performance.Monitor.ObjectCount) + "\n" +
				"Replay file index: " + moka;
		}
	}
}
