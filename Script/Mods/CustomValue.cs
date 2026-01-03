using Godot;
using System;

public partial class CustomValue : PanelContainer
{
	private PanelContainer Emblem;
	private Label Custom;
	private string id;
	public override void _Ready()
	{
		Emblem = GetNode<PanelContainer>("../../");
		Custom = GetNode<Label>("Value");
		if (Emblem.HasMeta("ModName")) id = Emblem.GetMeta("ModName").ToString();
	}
	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		if (id == "DT" && AudioPlayer.Instance.PitchScale != 1.25)
		{
			Visible = true;
			Custom.Text = $"{AudioPlayer.Instance.PitchScale}x";
		}
		else if (id == "HT" && AudioPlayer.Instance.PitchScale != 0.5)
		{
			Visible = true;
			Custom.Text = $"{AudioPlayer.Instance.PitchScale}x";
		}
		else Visible = false;
	}
}
