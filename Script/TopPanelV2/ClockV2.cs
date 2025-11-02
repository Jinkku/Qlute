using Godot;
using System;

public partial class ClockV2 : RichTextLabel
{
	private int prestart = 0;
	private int runtime = 0;
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		prestart = DateTime.Now.Second;
		Tick();
	}
	private string formatTime(int seconds)
	{
		int hours = seconds / 3600;
		int minutes = (seconds % 3600) / 60;
		int secs = seconds % 60;
		return $"{hours:D2}:{minutes:D2}:{secs:D2}";
	}

	private void Tick()
	{
		if (prestart != DateTime.Now.Second)
		{
			prestart = DateTime.Now.Second;
			runtime++;
		}
		var run = formatTime(runtime);
		Text = $"{DateTime.Now.ToString("HH:mm:ss")}\n[color=#FF9D00]running {run}[/color]";
	}
}
