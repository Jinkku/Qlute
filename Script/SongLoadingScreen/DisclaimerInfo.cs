using Godot;
using System;
[Tool]
public partial class DisclaimerInfo : PanelContainer
{
	private string title = "Title is here";
	private string description = "Description is here";
	private Color Cs = new Color("bec243");

	[Export]
	public string Title
	{
		get => title;
		set
		{
			title = value;
			ReloadInfo();
		}
	}

	[Export]
	public string Description
	{
		get => description;
		set
		{
			description = value;
			ReloadInfo();
		}
	}
	[Export]
	public Color ColorScheme
	{
		get => Cs;
		set
		{
			Cs = value;
			ReloadInfo();
		}
	}
	private Label TitleNode { get; set; }
	private Label Desc { get; set; }
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		TitleNode = GetNode<Label>("DisclaimerSort/title");
		Desc = GetNode<Label>("DisclaimerSort/desc");
		ReloadInfo();
	}

	private void ReloadInfo()
	{
		TitleNode.Text = Title;
		Desc.Text = Description;
		SelfModulate = ColorScheme;
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}
}
