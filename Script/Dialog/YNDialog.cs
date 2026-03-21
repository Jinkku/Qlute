using Godot;
using System;

public partial class YNDialog : Control
{
	[Export] public string Title { get; set; } = "Text Here";

	[Export] public string YesText { get; set; } = "Y";
	[Export] public string NoText { get; set; } = "N";
	private Label TitleNode { get; set; }
	private ButtonFade YesNode { get; set; }
	private ButtonFade NoNode { get; set; }
	
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		TitleNode = GetNode<Label>("Dialog/VBoxContainer/Title");
		YesNode = GetNode<ButtonFade>("Dialog/VBoxContainer/VBox/Y");
		NoNode = GetNode<ButtonFade>("Dialog/VBoxContainer/VBox/N");
		NoNode.Text = NoText;
		TitleNode.Text = Title;
		YesNode.Text = YesText;
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}
}
