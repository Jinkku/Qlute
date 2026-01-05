using Godot;
using System;

public partial class Break : TextureProgressBar
{
	private Label CountdownValue { get; set; }
	private Label Speed { get; set; }
	[Export] public double MaxTick { get; set; }
	private int TickValue = 0;
	private Tween Tween { get; set; }
	private double GameTickDep { get; set; }

	private double GetMaxTick() => MaxTick / AudioPlayer.Instance.PitchScale;

	private double GetValueTick() => MaxTick - (SettingsOperator.Gameplaycfg.Time - GameTickDep);
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		GameTickDep = SettingsOperator.Gameplaycfg.Time;
		MaxTick -= GameTickDep;
		var vaka = Math.Min(((float)Value / (float)MaxTick) * 1f,1);
		Tween = CreateTween();
		Modulate = new Color(0f, 0f, 0f, 0f);
		Tween.TweenProperty(this, "modulate", new Color(vaka, vaka, vaka, vaka), 0.2f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		Tween.Play();
		CountdownValue = GetNode<Label>("Label");
		MaxValue = MaxTick;
#if DEBUG
		GD.Print($"V:{GetValueTick()}/M:{GetMaxTick()}");	
#endif
	}

	private void Tick()
	{
		QueueFree();
	}
 	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		Value = GetValueTick();
		if (!Tween.IsRunning())
		{
			var vaka = Math.Min(((float)Value / (float)MaxTick) * 1f,1);
			Modulate = new Color(vaka, vaka, vaka, vaka);	
		}
	if (MaxTick - (SettingsOperator.Gameplaycfg.Time - GameTickDep) <= 0)
	{
		QueueFree();
	}
		MaxValue = GetMaxTick();
		CountdownValue.Text = Value.ToString("N0");
	}
}
