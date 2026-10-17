using Godot;
using System;
using System.Collections.Generic;
using System.Numerics;
using Vector2 = Godot.Vector2;

public partial class NotePool : Control
{
	[Export] public Gameplay Gameplay { get; set; }
	public override void _Draw()
	{
		if (Back == null || Fore == null)
			return;

		var notes = Gameplay.Notes;
		var lanes = Skin.Element.LaneNotes;
		int laneCount = lanes.Count;

		// Hoist everything that doesn't change per note
		bool blackOut = ModsOperator.Mods["black-out"];
		bool slice    = !blackOut && ModsOperator.Mods["slice"];   // black-out wins, matching your else-if

		if (blackOut)
			return; // every note would be fully transparent, so skip the whole loop

		float hitPoint   = Gameplay.HitPoint;
		float invHit     = 1f / hitPoint;          // multiply instead of divide per note
		float scroll     = Gameplay.notex;
		float fieldX     = Gameplay.PlayFieldPOS.X;
		float viewHeight = GetViewportRect().Size.Y;
		float backH      = Back.GetHeight();

		int maxNotes = Gameplay.MaxNotes;
		int start = Math.Min(maxNotes, Gameplay.NoteTick);
		int end   = Math.Min(Math.Min(maxNotes, Gameplay.NoteTick + 256), notes.Count);

		for (int i = start; i < end; i++)
		{
			var note = notes[i];
			if (note == null || note.hit) continue;

			int part = note.NotePart;
			if ((uint)part >= (uint)laneCount) continue;   // catches -1 and out of range in one compare

			float y = note.timing + scroll;

			// Cull notes that are off-screen
			if (y > viewHeight || y + backH < 0) continue;

			// Slice mod: fade based on position (was Note.Node.Modulate)
			float alpha = 1f;
			if (slice)
			{
				alpha = Math.Min(hitPoint, y - 350) * invHit;
				if (alpha <= 0f) continue;                 // invisible, don't draw
				if (alpha > 1f) alpha = 1f;
			}

			Color c = lanes[part] * 3;
			var pos = new Vector2(fieldX + 105 * part, y);

			DrawTexture(Back, pos, new Color(c.R, c.G, c.B, alpha));
			DrawTexture(Fore, pos, new Color(1f, 1f, 1f, alpha));
		}
	}
	private Texture2D Back { get; set; }
	private Texture2D Fore { get; set; }
	private Vector2 BackSize { get; set; }
	private Vector2 ForeSize { get; set; }
	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{		
		if (Skin.Element.NoteBack != Back)
		{
			Back = Skin.Element.NoteBack;
		}
		if (Skin.Element.NoteFore != Fore)
		{
			Fore = Skin.Element.NoteFore;
			BackSize = Back.GetSize();
			ForeSize = Fore.GetSize();
		}
		QueueRedraw();
	}
}
