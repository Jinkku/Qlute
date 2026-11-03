using Godot;
using System;

public partial class Updates : PanelContainer
{
	private Kiko KikoNode { get; set; }
	private Label Warning { get; set; }
	private Label VersionNode { get; set; }
	private OptionButton BranchSelector { get; set; }
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		KikoNode = GetTree().Root.GetNode<Kiko>("Kiko");
		Warning = GetNode<Label>("Rows/Warning");
		BranchSelector = GetNode<OptionButton>("Rows/BranchSelector");
		VersionNode = GetNode<Label>("Rows/Version");
		if (Kiko.CurrentUpdateChannel == Kiko.UpdateChannel.Stable) BranchSelector.Selected = 0;
		else BranchSelector.Selected = 1;
		Warning.Visible = (Kiko.CurrentUpdateChannel != Kiko.UpdateChannel.Stable);
	}

	private void changedbranch(int value)
	{
		Warning.Visible = (value != 0);
		if (value == 0)
		{
			SettingsOperator.SetSetting("updatebranch", "stable");
			Kiko.CurrentUpdateChannel = Kiko.UpdateChannel.Stable;
		}
		else
		{
			SettingsOperator.SetSetting("updatebranch", "experimental");
			Kiko.CurrentUpdateChannel = Kiko.UpdateChannel.Experimental;
		}
		KikoNode.CheckUpdates(true);
	}

// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		if (Kiko.VersionCode != Kiko.Version)
			VersionNode.Text = $"{Kiko.VersionCode} is availiable!";
		else 
			VersionNode.Text = $"{Kiko.VersionCode} is the latest version.";
	}

	private void Update()
	{
		KikoNode.CheckUpdates(true);
	}
}
