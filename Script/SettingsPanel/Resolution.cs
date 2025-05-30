using Godot;
using System;

public partial class Resolution : OptionButton
{
	// Called when the node enters the scene tree for the first time.
	public SettingsOperator SettingsOperator { get; set; }
	public Label ResolutionLabel { get; set; }
	public override void _Ready()
	{
		ResolutionLabel = GetNode<Label>("../WindowNoticeRes");
		SettingsOperator = GetNode<SettingsOperator>("/root/SettingsOperator");
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		if ((int.TryParse(SettingsOperator.GetSetting("windowmode")?.ToString(), out int mode) ? mode : 0) == 0)
		{
			Visible = true;
			ResolutionLabel.Visible = true;
		}
		else
		{
			Visible = false;
			ResolutionLabel.Visible = false;
		}
	}
}
