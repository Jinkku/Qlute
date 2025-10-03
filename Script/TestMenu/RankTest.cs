using Godot;
using System;

public partial class RankTest : Control
{
	private SettingsOperator SettingsOperator { get; set; }
	private bool isstarted { get; set; }
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		SettingsOperator = GetNode<SettingsOperator>("/root/SettingsOperator");
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		if (SettingsOperator != null && !isstarted) SettingsOperator.toppaneltoggle(true);
	}

	private void _rankup()
	{
		SettingsOperator.Sessioncfg["ranknumber"] = 1000000;
		SettingsOperator.ranked_points = 2999;
		RankUpdate.Update(200000, 4000);
	}
	private void _rankdown()
	{
		SettingsOperator.Sessioncfg["ranknumber"] = 200000;
		SettingsOperator.ranked_points = 4000;
		RankUpdate.Update(1000000, 2999);
	}
	private void _rankstay()
	{
		SettingsOperator.Sessioncfg["ranknumber"] = 200000;
		SettingsOperator.ranked_points = 4000;
		RankUpdate.Update(200000, 4000);
	}
}
