using Godot;
using System;

/// <summary>
/// Draws one player's playfield live from a <see cref="LanSpectateFeed"/>: 4 lanes, scrolling notes, lanes
/// lighting up on key presses, judgement pop-ups and combo, using the local skin. Self-contained (no gameplay
/// globals), so any number of these can sit in the spectate grid.
/// </summary>
public partial class LanPlayfieldView : Control
{
	private const float LaneWidth = 105f;   // same spacing as NotePool
	private const float FieldWidth = LaneWidth * 4;
	private const float HitFromBottom = 150f; // Gameplay.HitPoint = playfield height - 150
	private const double JudgeShowMs = 500;
	private const double LaneFadeMs = 500;

	public LanSpectateFeed Feed { get; set; }
	/// <summary>Song speed (DT / HT) so the playfield moves at the player's pace between packets.</summary>
	public float Speed { get; set; } = 1f;
	/// <summary>The song time last drawn (used to keep music in sync).</summary>
	public double ShownMs { get; private set; } = double.MinValue;
	public bool Finished { get; set; }

	public override void _Ready()
	{
		ClipContents = true;
		MouseFilter = MouseFilterEnum.Pass;
	}

	public override void _Process(double delta)
	{
		if (Feed != null && !Finished)
		{
			ShownMs = Feed.DisplayMs(Speed);
			if (ShownMs > double.MinValue) Feed.Advance(ShownMs);
		}
		QueueRedraw();
	}

	public override void _Draw()
	{
		var f = Feed;
		DrawRect(new Rect2(Vector2.Zero, Size), new Color(0, 0, 0, 0.55f));
		if (f == null || !f.HasChart) return;

		// Lay the player's field out in their own pixel units (1 px = 1 ms), then scale it into this tile.
		float h = f.ViewHeight;
		float scale = Mathf.Min(Size.Y / h, Size.X / FieldWidth);
		var offset = new Vector2((Size.X - FieldWidth * scale) / 2f, (Size.Y - h * scale) / 2f);
		DrawSetTransform(offset, 0, new Vector2(scale, scale));

		var lanes = Skin.Element.LaneNotes;
		double now = ShownMs;
		float hitY = h - HitFromBottom;

		// Lanes: dim lane colour, flashing brighter while held / just pressed (like Gameplay.hitnote).
		for (int i = 0; i < 4; i++)
		{
			var baseCol = lanes[i % lanes.Count];
			float glow = f.LaneDown[i] ? 1f : (float)Math.Clamp(1 - (now - f.LaneDownAt[i]) / LaneFadeMs, 0, 1);
			var col = baseCol / 2;
			col = col.Lerp(baseCol, glow);
			DrawRect(new Rect2(i * LaneWidth, 0, LaneWidth - 4, h), new Color(col.R, col.G, col.B, 0.35f + 0.25f * glow));
		}
		DrawRect(new Rect2(0, hitY - 2, FieldWidth, 4), new Color(1, 1, 1, 0.8f));

		// Notes: y = songTime - noteTime + HitPoint (top of the note), same as NotePool. Black-out is shown
		// anyway so the spectator can still follow the play.
		var back = Skin.Element.NoteBack;
		var fore = Skin.Element.NoteFore;
		if (back != null && fore != null && now > double.MinValue)
		{
			float noteH = back.GetHeight() * (LaneWidth / Math.Max(1, back.GetWidth()));
			for (int i = 0; i < f.Times.Length; i++)
			{
				if (f.Judged[i]) continue;
				float y = (float)(now - f.Times[i]) + hitY; // exactly NotePool's position, so it matches the player's screen
				if (y > h + 50) continue;         // already past (missed notes get judged soon)
				if (y + noteH < -50) continue;
				int lane = f.Lanes[i];
				var c = lanes[lane % lanes.Count] * 3;
				var rect = new Rect2(lane * LaneWidth, y, LaneWidth, noteH);
				DrawTextureRect(back, rect, false, new Color(c.R, c.G, c.B, 1));
				DrawTextureRect(fore, rect, false, Colors.White);
			}
		}

		// Judgement pop-up + combo.
		double since = now - f.LastJudgeAt;
		if (f.LastJudge >= 0 && since >= 0 && since < JudgeShowMs)
		{
			var tex = f.LastJudge switch
			{
				0 => Skin.Element.JudgePerfect,
				1 => Skin.Element.JudgeGreat,
				2 => Skin.Element.JudgeMeh,
				_ => Skin.Element.JudgeMiss,
			};
			if (tex != null)
			{
				float a = (float)(1 - since / JudgeShowMs);
				var size = tex.GetSize() * (FieldWidth * 0.6f / Math.Max(1, tex.GetWidth()));
				DrawTextureRect(tex, new Rect2(new Vector2((FieldWidth - size.X) / 2, hitY - 140 - size.Y / 2 - 10 * a), size), false, new Color(1, 1, 1, a));
			}
		}
		int combo = f.LastJudge >= 0 ? f.ShownCombo : f.Combo;
		if (combo > 0)
		{
			var font = ThemeDB.FallbackFont;
			const int fontSize = 56;
			var text = $"{combo}x";
			var w = font.GetStringSize(text, HorizontalAlignment.Left, -1, fontSize).X;
			DrawString(font, new Vector2((FieldWidth - w) / 2, h * 0.3f), text, HorizontalAlignment.Left, -1, fontSize, new Color(1, 1, 1, 0.85f));
		}
		DrawSetTransform(Vector2.Zero, 0, Vector2.One);
	}
}
