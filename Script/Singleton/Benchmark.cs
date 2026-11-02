using Godot;
using System;

/// <summary>
/// Dev-only FPS benchmark. Launch with `-- --bench` (implies --uncapped).
/// Walks Home -> Song Select -> Gameplay (Auto) and prints per-second stats:
/// FPS, CPU process time, GPU render time, draw calls and node count, then quits.
/// </summary>
public partial class Benchmark : Node
{
	private static readonly (string Scene, double Seconds)[] Steps =
	{
		(null, 8), // whatever boots first (intro/home)
		("res://Panels/Screens/song_select.tscn", 8),
		("res://Panels/Screens/SongLoadingScreen.tscn", 20), // loads into gameplay
	};

	private int _step = -1;
	private double _stepTime, _secTime;
	private int _frames;
	private double _cpuAccum;
	private double _minFps = double.MaxValue, _sumFps;
	private int _samples;

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
		RenderingServer.ViewportSetMeasureRenderTime(GetViewport().GetViewportRid(), true);
		GD.Print($"[bench] display={DisplayServer.GetName()} renderer={RenderingServer.GetCurrentRenderingMethod()} gpu={RenderingServer.GetVideoAdapterName()}");
		NextStep();
	}

	private void NextStep()
	{
		if (_step >= 0) Summary();
		_step++;
		_stepTime = 0;
		_minFps = double.MaxValue; _sumFps = 0; _samples = 0;
		if (_step >= Steps.Length)
		{
			GetTree().Quit();
			return;
		}
		var scene = Steps[_step].Scene;
		if (scene == null) return;
		if (scene.Contains("SongLoading")) ModsOperator.SetMods("AT");
		GetNode<SceneTransition>("/root/Transition").Switch(scene);
	}

	private void Summary()
	{
		if (_samples == 0) return;
		GD.Print($"[bench] SUMMARY step {_step} ({Steps[_step].Scene ?? "boot"}): avg {_sumFps / _samples:0} fps, min {_minFps:0} fps");
	}

	public override void _Process(double delta)
	{
		_frames++;
		_stepTime += delta;
		_secTime += delta;
		_cpuAccum += Performance.GetMonitor(Performance.Monitor.TimeProcess);

		if (_secTime >= 1.0)
		{
			var vp = GetViewport().GetViewportRid();
			double fps = _frames / _secTime;
			// Skip the first second of each step (scene transition noise).
			if (_stepTime > 1.5) { _sumFps += fps; _minFps = Math.Min(_minFps, fps); _samples++; }
			GD.Print(
				$"[bench] {GetTree().CurrentScene?.Name,-20} fps {fps,6:0} | " +
				$"cpu process {_cpuAccum / _frames * 1000,6:0.000} ms | " +
				$"gpu {RenderingServer.ViewportGetMeasuredRenderTimeGpu(vp),6:0.000} ms | " +
				$"render cpu {RenderingServer.ViewportGetMeasuredRenderTimeCpu(vp),6:0.000} ms | " +
				$"draws {Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame),5:0} | " +
				$"nodes {Performance.GetMonitor(Performance.Monitor.ObjectNodeCount),5:0}");
			_frames = 0; _cpuAccum = 0; _secTime = 0;
		}

		if (_stepTime >= Steps[_step].Seconds) NextStep();
	}
}
