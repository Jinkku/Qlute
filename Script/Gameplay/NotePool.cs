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
		float songMs     = Gameplay.gametime;
		float pxPerMs    = Gameplay.ScrollPxPerMs; // the player's scroll speed
		const float k    = UiScale.Factor;       // skin note images are sized for the old 1280x720 layout
		float fieldX     = Gameplay.PlayFieldPOS.X;
		// Lane step from the real lane sections (their gap is rounded to whole pixels), so notes stay on their lanes.
		var lanesBox     = Gameplay.Chart;
		float laneStep   = lanesBox != null && lanesBox.GetChildCount() > 1
			? ((Control)lanesBox.GetChild(1)).Position.X - ((Control)lanesBox.GetChild(0)).Position.X
			: 105 * UiScale.Factor;
		float viewHeight = GetViewportRect().Size.Y;
		var backSize     = Back.GetSize() * k;
		var foreSize     = Fore.GetSize() * k;
		float backH      = backSize.Y;

		int maxNotes = Gameplay.MaxNotes;
		int start = Math.Min(maxNotes, Gameplay.NoteTick);
		// enough notes for a slow scroll speed on a dense map; the loop stops at the first one above the screen
		int end   = Math.Min(Math.Min(maxNotes, Gameplay.NoteTick + 4096), notes.Count);

		for (int i = start; i < end; i++)
		{
			var note = notes[i];
			if (note == null || note.hit) continue;

			int part = note.NotePart;
			if ((uint)part >= (uint)laneCount) continue;   // catches -1 and out of range in one compare

			float y = hitPoint + (note.timing + songMs) * pxPerMs;

			// Cull notes that are off-screen (notes are in time order: the rest are higher still)
			if (y + backH < 0) break;
			if (y > viewHeight) continue;

			// Slice mod: fade based on position (was Note.Node.Modulate)
			float alpha = 1f;
			if (slice)
			{
				alpha = y / (viewHeight / 2);
				if (alpha <= 0f) continue;                 // invisible, don't draw
				if (alpha > 1f) alpha = 1f;
			}

			Color c = lanes[part] * 3;
			var pos = new Vector2(fieldX + laneStep * part, y);

			DrawTextureRect(Back, new Rect2(pos, backSize), false, new Color(c.R, c.G, c.B, alpha));
			DrawTextureRect(Fore, new Rect2(pos, foreSize), false, new Color(1f, 1f, 1f, alpha));
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
