using Godot;
using System;

/// <summary>
/// Draws one player's playfield live from a <see cref="LanSpectateFeed"/>, for whichever game mode they're
/// playing: Mania (4 scrolling lanes), Taiko (a drum road) or Tap! (circles, sliders, spinners and the cursor).
/// Key presses, judgement pop-ups and combo are shown too, using the local skin. Self-contained (no gameplay
/// globals), so any number of these can sit in the spectate grid.
/// </summary>
public partial class LanPlayfieldView : Control
{
	private const float LaneWidth = 105f;   // same spacing as NotePool
	private const float FieldWidth = LaneWidth * 4;
	private const float HitFromBottom = 150f; // Gameplay.HitPoint = playfield height - 150
	private const double JudgeShowMs = 500;
	private const double LaneFadeMs = 500;

	// Taiko road (virtual units; 1 px = 1 ms like the player's screen).
	private const float TaikoHeight = 300f, TaikoRoadTop = 90f, TaikoRoadHeight = 140f, TaikoHitX = 200f;
	private const float TaikoRadius = 34f, TaikoBigRadius = 50f;
	private static readonly Color DonColour = new(0.92f, 0.27f, 0.17f);
	private static readonly Color KatColour = new(0.27f, 0.56f, 0.85f);

	// Tap! field: 512x384 playfield inside a 640x480 frame, like TapGameCore.
	private static readonly Vector2 TapField = new(512, 384);
	private static readonly Vector2 TapFrame = new(640, 480);
	private const float TapFadeIn = 400f, TapFadeOut = 150f, SpinnerRadius = 150f;

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
		switch (f.Mode)
		{
			case 1: DrawTaiko(f); break;
			case 3: DrawTap(f); break;
			default: DrawMania(f); break;
		}
		DrawSetTransform(Vector2.Zero, 0, Vector2.One);
	}

	/// <summary>Fits a virtual <paramref name="area"/> into this tile and draws in its units from then on.</summary>
	private void UseVirtualArea(Vector2 area)
	{
		float scale = Mathf.Min(Size.Y / area.Y, Size.X / area.X);
		var offset = new Vector2((Size.X - area.X * scale) / 2f, (Size.Y - area.Y * scale) / 2f);
		DrawSetTransform(offset, 0, new Vector2(scale, scale));
	}

	// ───────────────────────────── Mania ─────────────────────────────

	private void DrawMania(LanSpectateFeed f)
	{
		// Lay the player's field out in their own pixel units (1 px = 1 ms), then scale it into this tile.
		float h = f.ViewHeight;
		UseVirtualArea(new Vector2(FieldWidth, h));

		var lanes = Skin.Element.LaneNotes;
		double now = ShownMs;
		float hitY = h - HitFromBottom;

		// Lanes: dim lane colour, flashing brighter while held / just pressed (like Gameplay.hitnote).
		for (int i = 0; i < 4; i++)
		{
			var baseCol = lanes[i % lanes.Count];
			float glow = Glow(f, i, now);
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

		DrawJudge(f, now, new Vector2(FieldWidth / 2, hitY - 140), FieldWidth * 0.6f);
		DrawCombo(f, new Vector2(FieldWidth / 2, h * 0.3f), 56);
	}

	// ───────────────────────────── Taiko ─────────────────────────────

	private void DrawTaiko(LanSpectateFeed f)
	{
		// The player's screen width is sent as the chart "height"; show the same stretch of road they see.
		float w = Mathf.Max(800f, f.ViewHeight);
		UseVirtualArea(new Vector2(w, TaikoHeight));
		double now = ShownMs;
		float midY = TaikoRoadTop + TaikoRoadHeight / 2f;

		DrawRect(new Rect2(0, TaikoRoadTop, w, TaikoRoadHeight), new Color(0.08f, 0.08f, 0.1f, 0.85f));
		DrawRect(new Rect2(0, TaikoRoadTop - 2, w, 2), new Color(1, 1, 1, 0.5f));
		DrawRect(new Rect2(0, TaikoRoadTop + TaikoRoadHeight, w, 2), new Color(1, 1, 1, 0.5f));

		// Receptor: the inner face lights red for don keys (1, 2), the rim blue for kat keys (0, 3).
		float don = Mathf.Max(Glow(f, 1, now), Glow(f, 2, now));
		float kat = Mathf.Max(Glow(f, 0, now), Glow(f, 3, now));
		var c = new Vector2(TaikoHitX, midY);
		DrawCircle(c, TaikoRadius + 10, new Color(KatColour, 0.15f + 0.6f * kat));
		DrawCircle(c, TaikoRadius - 4, new Color(DonColour, 0.15f + 0.6f * don));
		DrawArc(c, TaikoRadius + 10, 0, Mathf.Tau, 48, new Color(1, 1, 1, 0.7f), 3);

		if (now > double.MinValue)
		{
			// Later notes first so the next one to hit is drawn on top.
			for (int i = f.Times.Length - 1; i >= 0; i--)
			{
				if (f.Judged[i]) continue;
				float x = TaikoHitX + (float)(f.Times[i] - now);
				if (x < -TaikoBigRadius || x > w + TaikoBigRadius) continue;
				int kind = f.Lanes[i];
				bool big = kind >= 2;
				float r = big ? TaikoBigRadius : TaikoRadius;
				var col = (kind & 1) == 0 ? DonColour : KatColour;
				var p = new Vector2(x, midY);
				DrawCircle(p, r, col);
				DrawArc(p, r, 0, Mathf.Tau, 40, Colors.White, 3);
			}
		}

		DrawJudge(f, now, new Vector2(TaikoHitX, TaikoRoadTop - 45), 160);
		DrawCombo(f, new Vector2(TaikoHitX, TaikoRoadTop + TaikoRoadHeight + 50), 40);
	}

	// ───────────────────────────── Tap! ─────────────────────────────

	private void DrawTap(LanSpectateFeed f)
	{
		// Everything below is in osu! playfield units, centred in the frame like TapGameCore.
		float scale = Mathf.Min(Size.Y / TapFrame.Y, Size.X / TapFrame.X);
		var offset = new Vector2((Size.X - TapField.X * scale) / 2f, (Size.Y - TapField.Y * scale) / 2f);
		DrawSetTransform(offset, 0, new Vector2(scale, scale));
		DrawRect(new Rect2(Vector2.Zero, TapField), new Color(1, 1, 1, 0.04f));

		var ch = f.Chart;
		double now = ShownMs;
		var lanes = Skin.Element.LaneNotes;
		float r = ch.Radius > 1 ? ch.Radius : 36f;
		float preempt = ch.Preempt > 1 ? ch.Preempt : 600f;

		if (now > double.MinValue && ch.PtStart.Length == f.Times.Length)
		{
			for (int i = f.Times.Length - 1; i >= 0; i--)
			{
				if (f.Judged[i]) continue;
				int start = f.Times[i];
				int end = i < ch.Ends.Length ? Math.Max(start, ch.Ends[i]) : start;
				if (now < start - preempt || now > end + TapFadeOut) continue;

				float alpha = Mathf.Clamp((float)(now - (start - preempt)) / Mathf.Min(TapFadeIn, preempt), 0, 1);
				int type = f.Lanes[i] & 3;
				int slides = Math.Max(1, f.Lanes[i] >> 2);
				var baseCol = lanes[i % lanes.Count];
				var col = new Color(baseCol.R, baseCol.G, baseCol.B, alpha);
				var head = ch.Point(i, 0);

				if (type == 2)
				{
					// Spinner: shrinking ring showing how long is left.
					float left = end > start ? Mathf.Clamp((float)(end - now) / (end - start), 0, 1) : 0;
					DrawArc(head, SpinnerRadius, 0, Mathf.Tau, 64, new Color(1, 1, 1, 0.35f * alpha), 4);
					DrawArc(head, SpinnerRadius * left, 0, Mathf.Tau, 64, new Color(col, alpha), 6);
					continue;
				}

				if (type == 1)
				{
					int n = ch.PointCount(i);
					if (n >= 2)
					{
						var pts = new Vector2[n];
						for (int k = 0; k < n; k++) pts[k] = ch.Point(i, k);
						var body = new Color(col.R * 0.4f, col.G * 0.4f, col.B * 0.4f, 0.8f * alpha);
						DrawPolyline(pts, new Color(1, 1, 1, 0.7f * alpha), r * 2f);
						DrawPolyline(pts, body, r * 2f - 6f);
						DrawCircle(pts[^1], r, new Color(1, 1, 1, 0.7f * alpha));
						DrawCircle(pts[^1], r - 3f, body);
						if (now >= start)
						{
							// Ball: same back-and-forth as TapObject.SliderBallAt, along the evenly spaced samples.
							float dur = Math.Max(1, end - start);
							float e = Mathf.Clamp((float)(now - start), 0, dur);
							float slideDur = dur / slides;
							int span = Math.Min(slides - 1, (int)(e / slideDur));
							float p = (e - span * slideDur) / slideDur;
							if (span % 2 == 1) p = 1f - p;
							float fi = p * (n - 1);
							int a = Math.Min(n - 2, (int)fi);
							var ball = pts[a].Lerp(pts[a + 1], fi - a);
							DrawCircle(ball, r * 0.8f, new Color(1, 1, 1, alpha));
							DrawArc(ball, r * 1.6f, 0, Mathf.Tau, 40, new Color(1, 1, 1, 0.5f * alpha), 2);
							continue; // head already hit / passed
						}
					}
				}

				// Circle (or slider head before it's reached) + approach circle.
				DrawCircle(head, r, col);
				DrawArc(head, r, 0, Mathf.Tau, 40, new Color(1, 1, 1, alpha), 3);
				if (now < start)
				{
					float approach = 1f + 3f * (float)(start - now) / preempt;
					DrawArc(head, r * approach, 0, Mathf.Tau, 48, new Color(col.R, col.G, col.B, 0.8f * alpha), 2);
				}
			}
		}

		if (f.HasCursor)
		{
			DrawCircle(f.Cursor, 9, new Color(1f, 0.85f, 0.4f, 0.95f));
			DrawArc(f.Cursor, 9, 0, Mathf.Tau, 24, Colors.White, 2);
		}

		// Key / mouse button indicators (Z, X, LMB, RMB).
		for (int k = 0; k < 4; k++)
		{
			float glow = Glow(f, k, now);
			DrawRect(new Rect2(TapField.X + 20, TapField.Y / 2 - 70 + k * 36, 28, 28), new Color(1, 1, 1, 0.15f + 0.7f * glow));
		}

		// Judgement pop-up where the judged object was.
		var popAt = new Vector2(TapField.X / 2, TapField.Y / 2);
		int ji = f.LastJudgeIndex;
		if ((uint)ji < (uint)ch.PtStart.Length)
		{
			int last = (f.Lanes[ji] & 3) == 1 ? ch.PointCount(ji) - 1 : 0;
			popAt = ch.Point(ji, Math.Max(0, last));
		}
		DrawJudge(f, now, popAt, 110);
		DrawCombo(f, new Vector2(40, TapField.Y + 40), 40, center: false);
	}

	// ───────────────────────────── Shared ─────────────────────────────

	/// <summary>1 while the key is held, fading to 0 over <see cref="LaneFadeMs"/> after it was last pressed.</summary>
	private static float Glow(LanSpectateFeed f, int key, double now) =>
		f.LaneDown[key] ? 1f : (float)Math.Clamp(1 - (now - f.LaneDownAt[key]) / LaneFadeMs, 0, 1);

	private void DrawJudge(LanSpectateFeed f, double now, Vector2 center, float width)
	{
		double since = now - f.LastJudgeAt;
		if (f.LastJudge < 0 || since < 0 || since >= JudgeShowMs) return;
		var tex = f.LastJudge switch
		{
			0 => Skin.Element.JudgePerfect,
			1 => Skin.Element.JudgeGreat,
			2 => Skin.Element.JudgeMeh,
			_ => Skin.Element.JudgeMiss,
		};
		if (tex == null) return;
		float a = (float)(1 - since / JudgeShowMs);
		var size = tex.GetSize() * (width / Math.Max(1, tex.GetWidth()));
		DrawTextureRect(tex, new Rect2(center - size / 2 - new Vector2(0, 10 * a), size), false, new Color(1, 1, 1, a));
	}

	private void DrawCombo(LanSpectateFeed f, Vector2 pos, int fontSize, bool center = true)
	{
		int combo = f.LastJudge >= 0 ? f.ShownCombo : f.Combo;
		if (combo <= 0) return;
		var font = ThemeDB.FallbackFont;
		var text = $"{combo}x";
		float w = center ? font.GetStringSize(text, HorizontalAlignment.Left, -1, fontSize).X : 0;
		DrawString(font, new Vector2(pos.X - w / 2, pos.Y), text, HorizontalAlignment.Left, -1, fontSize, new Color(1, 1, 1, 0.85f));
	}
}
