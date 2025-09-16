using Godot;
using System;

public partial class Countdown : TextureProgressBar
{
	private TextureProgressBar CountdownProgress { get; set; }
	private Label CountdownValue { get; set; }
	private Label Speed { get; set; }
	private Timer Wait { get; set; }
	private int MaxTick = 3;
	private int TickValue = 0;
	private Tween Tween { get; set; }
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		CountdownProgress = this;
		CountdownValue = GetNode<Label>("Label");
		Wait = GetNode<Timer>("Wait");
		CountdownProgress.MaxValue = MaxTick;
		CountdownProgress.Value = MaxTick;
		TickValue = MaxTick;
	}

	private void Tick()
	{
		var newval = TickValue-1;
		if (newval < 1)
		{
			GetTree().Paused = false;
		}
		TickValue= newval;
		var vaka = ((float)TickValue / (float)MaxTick) * 1f;
		Tween?.Kill();
		Tween = CreateTween();
		Tween.SetParallel(true);
		Tween.TweenProperty(CountdownProgress, "modulate", new Color(vaka,vaka,vaka,vaka), Wait.WaitTime).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		Tween.TweenProperty(CountdownProgress, "value", TickValue, Wait.WaitTime).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		Tween.Play();
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		if (!GetTree().Paused)
		{
			GetNode<Control>("../").QueueFree();
		}
		CountdownProgress.MaxValue = MaxTick;
		CountdownValue.Text = CountdownProgress.Value.ToString("N0");
	}
}
