using Godot;
using System;

public partial class TimeLeft : Label
{
	// Called when the node enters the scene tree for the first time.
	public static SettingsOperator SettingsOperator { get; set; }
	public override void _Ready()
	{
		SettingsOperator = GetNode<SettingsOperator>("/root/SettingsOperator");
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		var etho = SettingsOperator.Gameplaycfg["timetotal"]-SettingsOperator.Gameplaycfg["time"];
		if (etho < 0)
		{
			etho = 0;
		}
		Text = "-" + TimeSpan.FromMilliseconds(etho).ToString(@"mm\:ss") ?? "-00:00";
	}
}
