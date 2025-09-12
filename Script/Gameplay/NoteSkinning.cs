using Godot;
using System;

public partial class NoteSkinning : Sprite2D
{
	private Texture2D Back { get; set; }
	private Texture2D Fore { get; set; }
	private Color NoteColour { get; set; }
	private Sprite2D ForeNote { get; set; }
	
	/// <summary>
	/// Check if skin is changed.
	/// </summary>
	private void CheckSkinNote()
	{
		if (Skin.Element.LaneNotes[(int)GetMeta("part")] * 2 != NoteColour)
		{
			NoteColour = Skin.Element.LaneNotes[(int)GetMeta("part")] * 3;
			SelfModulate = new Color(NoteColour.R, NoteColour.G, NoteColour.B, SelfModulate.A);
		}
		if (Skin.Element.NoteBack != Back)
		{
			Back = Skin.Element.NoteBack;
			Texture = Back;
		}
		if (Skin.Element.NoteFore != Fore)
		{
			Fore = Skin.Element.NoteFore;
			ForeNote.Texture = Fore;
			var backsize = Back.GetSize();
			var foresize = Fore.GetSize();
			ForeNote.Position = new Vector2(Math.Max(0, backsize.X - foresize.X), Math.Max(0, backsize.Y - foresize.Y));
		}
	}
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		ForeNote = GetNode<Sprite2D>("NoteFront");
		CheckSkinNote();
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		CheckSkinNote();
	}
}
