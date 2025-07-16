using Godot;
using System;

public partial class Clock : Label
{
	// Called when the node enters the scene tree for the first time.
	private int prestart = 0;
	private int runtime = 0;
	public override void _Ready()
	{
		prestart = DateTime.Now.Second;
	}
	private string formatTime(int seconds)
	{
		int hours = seconds / 3600;
		int minutes = (seconds % 3600) / 60;
		int secs = seconds % 60;
		return $"{hours:D2}:{minutes:D2}:{secs:D2}";
	}
	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		if (prestart != DateTime.Now.Second)
		{
			prestart = DateTime.Now.Second;
			runtime++;
		}
		var run = formatTime(runtime);
		Text = DateTime.Now.ToString("HH:mm:ss tt") + "\nrunning " + run;
	}
}
