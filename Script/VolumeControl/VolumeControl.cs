using Godot;
using System;

public partial class VolumeControl : PanelContainer
{
	private VolumeKnob MasterSlider;
	private VolumeKnob SampleSlider;
	private Timer WaitTimeout;
	private Tween Tween;
	public SettingsOperator SettingsOperator { get; set; }
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		SettingsOperator = GetNode<SettingsOperator>("/root/SettingsOperator");
		MasterSlider = GetNode<VolumeKnob>("VBoxContainer/Master");
		WaitTimeout = GetNode<Timer>("WaitTimeout");
		SampleSlider = GetNode<VolumeKnob>("VBoxContainer/Sample");
		var samplev = AudioPlayer.SampleVol;
		var masterv = AudioPlayer.MasterVol;
		SampleSlider.CurrentValue = samplev;
		MasterSlider.CurrentValue = masterv;
		//MusicSlider.Value = Math.Pow(10, masterv / 10) * 100;
		WaitTimeout.Start();
	}

	public override void _ExitTree()
	{
		Tween?.Kill();
		Global.VolumeControlActive = false;
	}
	
	private void _master(float value)
	{
		AudioPlayer.Instance.VolumeDb = AudioPlayer.ToDB(value);
		AudioPlayer.MasterVol = (int)value;
		SettingsOperator.SetSetting("master", (int)value);
	}
	private void _sample(float value)
	{
		SettingsOperator.SetSetting("sample", (int)value);
		AudioPlayer.SampleVol = (int)value;
	}

	private void keep()
	{
		WaitTimeout.Stop();
	}

	private void close()
	{
		WaitTimeout.Start();
	}

	private void exit()
	{
		
		Tween?.Kill();
		Tween = CreateTween();
		WaitTimeout.Stop();
		GetTree().CurrentScene?.SetProcessInput(true);
		Tween.SetParallel(true);
		Tween.TweenProperty(this, "position", new Vector2(-Size.X, Position.Y), 0.3f)
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.Out);
		Tween.TweenProperty(this, "modulate", new Color(1f, 1f, 1f, 0f), 0.3f)
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.Out);
		Tween.SetParallel(false);
		Tween.TweenCallback(Callable.From(() =>
		{
			Global.VolumeControlActive = false;
			// Free the whole overlay (the CanvasLayer parent lives on /root), not just hide it.
			var owner = GetParent() as CanvasLayer;
			if (owner != null) owner.QueueFree();
			else QueueFree();
		}));
	}
}
