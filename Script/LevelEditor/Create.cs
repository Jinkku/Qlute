using Godot;
using System;

public partial class Create : Control
{
	// Called when the node enters the scene tree for the first time.
	private Sprite2D NoteHighlight { get; set; } = null;
	private int NoteID { get; set; } = 0;
	public override void _Ready()
	{
	}

	public override void _ExitTree()
	{
		SettingsOperator.CreateSelectingBeatmap = false;
	}

	private void _FileMenu(int ID)
	{
		if (ID == 1)
		{
			GetNode<SceneTransition>("/root/Transition").Switch("res://Panels/Screens/song_select.tscn");
			SettingsOperator.CreateSelectingBeatmap = true;
		} else if (ID == 5)
		{
			_on_back();
		}
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	private void _on_back(){
		GetNode<SceneTransition>("/root/Transition").Switch("res://Panels/Screens/home_screen.tscn");
	}
	private void _enter(int id)
	{
		id += 1;
		var s = GetNode<Label>("Info/ContextSections/Na");
		s.Text = id.ToString();
		var Notetmp = GD.Load<PackedScene>("res://Panels/GameplayElements/Static/note.tscn").Instantiate().GetNode<Sprite2D>(".");
		if (NoteHighlight == null)
		{
			GetNode<ColorRect>("Ground/Playfield/ChartSections/Section" + id.ToString()).AddChild(Notetmp);
			NoteHighlight = Notetmp;
		}
	}
	private void _off(int id)
	{
		var s = GetNode<Label>("Info/ContextSections/Na");
		s.Text = "off";
		NoteHighlight?.QueueFree();
		NoteHighlight = null;
	}
	public override void _Process(double delta)
	{
		if (NoteHighlight != null) NoteHighlight.Position = new Vector2(0, GetViewport().GetMousePosition().Y - 150);
	}
}
