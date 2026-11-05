using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CreateEditor;

public enum EditorTool
{
	Select = 0,
	Place = 1,
	Remove = 2,
}

/// <summary>
/// Scrolling 4K mania playfield used by the Compose tab. Time runs upwards; the judgement line
/// sits near the bottom and is the editor's current time.
/// </summary>
public partial class ManiaEditorField : Control
{
	public Create Editor { get; set; }

	private const float BasePxPerMs = 0.45f;
	private const int MaxLaneWidth = 100;

	// interaction state
	private Vector2 _mouse;
	private bool _mouseInside;
	private bool _boxSelecting;
	private Vector2 _boxStart;
	private bool _moving;
	private int _moveStartTime;
	private int _moveStartLane;
	private int _moveDt;
	private int _moveDl;
	private bool _boxAdditive;

	private static readonly Color MeasureColor = new(1f, 1f, 1f, 0.85f);
	private static readonly Color BeatColor = new(1f, 1f, 1f, 0.45f);
	private static readonly Color HalfColor = new(1f, 0.25f, 0.25f, 0.55f);
	private static readonly Color ThirdColor = new(0.75f, 0.35f, 1f, 0.55f);
	private static readonly Color QuarterColor = new(0.3f, 0.55f, 1f, 0.55f);
	private static readonly Color SixthColor = new(1f, 0.45f, 0.85f, 0.45f);
	private static readonly Color EighthColor = new(1f, 0.85f, 0.25f, 0.45f);
	private static readonly Color FineColor = new(0.7f, 0.7f, 0.7f, 0.3f);

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Stop;
		ClipContents = true;
		FocusMode = FocusModeEnum.Click;
		MouseEntered += () => _mouseInside = true;
		MouseExited += () => { _mouseInside = false; Editor?.OnFieldHover(-1, -1); };
	}

	public override void _Process(double delta) => QueueRedraw();

	// ---------- geometry ----------
	private float LaneWidth => Mathf.Min(MaxLaneWidth, Size.X / (ManiaBeatmap.KeyCount + 2));
	private float FieldWidth => LaneWidth * ManiaBeatmap.KeyCount;
	private float FieldLeft => (Size.X - FieldWidth) / 2f;
	private float JudgeY => Size.Y * 0.85f;
	private float PxPerMs => BasePxPerMs * (Editor?.Zoom ?? 1f);
	private double Now => Editor?.CurrentMs ?? 0;

	private float TimeToY(double t) => JudgeY - (float)((t - Now) * PxPerMs);
	private double YToTime(float y) => Now + (JudgeY - y) / PxPerMs;

	private int LaneAt(float x)
	{
		var lane = (int)Mathf.Floor((x - FieldLeft) / LaneWidth);
		return lane is >= 0 and < ManiaBeatmap.KeyCount ? lane : -1;
	}

	private float NoteHeight
	{
		get
		{
			var tex = Skin.Element?.NoteBack;
			if (tex == null) return 20;
			var s = tex.GetSize();
			return Mathf.Clamp(LaneWidth * s.Y / Mathf.Max(1, s.X), 12, 40);
		}
	}

	private Rect2 NoteRect(EditorNote n, int dt = 0, int dl = 0)
	{
		var h = NoteHeight;
		var x = FieldLeft + (n.Lane + dl) * LaneWidth;
		var y = TimeToY(n.Time + dt) - h / 2f;
		return new Rect2(x + 2, y, LaneWidth - 4, h);
	}

	private EditorNote NoteAt(Vector2 pos)
	{
		var map = Editor?.Map;
		if (map == null) return null;
		var lane = LaneAt(pos.X);
		if (lane < 0) return null;
		EditorNote best = null;
		float bestDist = float.MaxValue;
		foreach (var n in map.Notes)
		{
			if (n.Lane != lane) continue;
			var r = NoteRect(n).Grow(3);
			if (n.IsHold) r = r.Expand(new Vector2(r.Position.X, TimeToY(n.EndTime)));
			if (!r.HasPoint(pos)) continue;
			var d = Mathf.Abs(TimeToY(n.Time) - pos.Y);
			if (d < bestDist) { bestDist = d; best = n; }
		}
		return best;
	}

	// ---------- drawing ----------
	public override void _Draw()
	{
		if (Editor?.Map == null) return;
		var map = Editor.Map;
		var font = GetThemeDefaultFont();
		var left = FieldLeft;
		var width = FieldWidth;
		var tTop = YToTime(0);
		var tBottom = YToTime(Size.Y);

		// playfield backing + lanes
		DrawRect(new Rect2(left, 0, width, Size.Y), new Color(0, 0, 0, 0.72f));
		for (int i = 0; i < ManiaBeatmap.KeyCount; i++)
		{
			var lane = Skin.Element?.LaneNotes != null && i < Skin.Element.LaneNotes.Count ? Skin.Element.LaneNotes[i] : Colors.White;
			DrawRect(new Rect2(left + i * LaneWidth, 0, LaneWidth, Size.Y), new Color(lane.R, lane.G, lane.B, 0.05f));
			if (i > 0) DrawLine(new Vector2(left + i * LaneWidth, 0), new Vector2(left + i * LaneWidth, Size.Y), new Color(1, 1, 1, 0.08f));
		}
		DrawLine(new Vector2(left, 0), new Vector2(left, Size.Y), new Color(1, 1, 1, 0.3f), 2);
		DrawLine(new Vector2(left + width, 0), new Vector2(left + width, Size.Y), new Color(1, 1, 1, 0.3f), 2);

		// song start/end
		DrawRect(new Rect2(left, TimeToY(0), width, Size.Y), new Color(0, 0, 0, 0.4f));
		if (Editor.SongLengthMs > 0)
		{
			var endY = TimeToY(Editor.SongLengthMs);
			if (endY > 0) DrawRect(new Rect2(left, 0, width, endY), new Color(0, 0, 0, 0.4f));
		}

		DrawBeatGrid(map, tTop, tBottom, left, width, font);

		// timing point markers
		foreach (var tp in map.TimingPoints)
		{
			var y = TimeToY(tp.Offset);
			if (y < -20 || y > Size.Y + 20) continue;
			DrawLine(new Vector2(left - 70, y), new Vector2(left + width, y), new Color(1f, 0.2f, 0.2f, 0.9f), 2);
			DrawString(font, new Vector2(left - 70, y - 4), $"{tp.Bpm:0.##} BPM", HorizontalAlignment.Left, -1, 12, new Color(1f, 0.5f, 0.5f));
		}
		if (map.PreviewTime >= 0)
		{
			var y = TimeToY(map.PreviewTime);
			if (y > -20 && y < Size.Y + 20)
			{
				DrawLine(new Vector2(left + width, y), new Vector2(left + width + 70, y), new Color(0.3f, 1f, 0.5f, 0.9f), 2);
				DrawString(font, new Vector2(left + width + 4, y - 4), "Preview", HorizontalAlignment.Left, -1, 12, new Color(0.5f, 1f, 0.6f));
			}
		}

		// notes
		var sel = Editor.Selection;
		var margin = 60 / PxPerMs;
		foreach (var n in map.Notes)
		{
			var end = n.IsHold ? n.EndTime : n.Time;
			if (end < tBottom - margin || n.Time > tTop + margin) continue;
			bool selected = sel.Contains(n);
			if (selected && _moving) continue; // drawn at the dragged position below
			DrawNote(n, 0, 0, 1f, selected);
		}
		if (_moving)
			foreach (var n in sel)
				DrawNote(n, _moveDt, _moveDl, 0.85f, true);

		// placement ghost
		if (_mouseInside && Editor.Tool == EditorTool.Place && !_moving && !_boxSelecting)
		{
			var lane = LaneAt(_mouse.X);
			if (lane >= 0)
			{
				var ghost = new EditorNote { Lane = lane, Time = Editor.SnapTime(YToTime(_mouse.Y)) };
				DrawNote(ghost, 0, 0, 0.4f, false);
			}
		}

		// judgement line (= current time)
		DrawRect(new Rect2(left, JudgeY - 1.5f, width, 3), new Color(1f, 1f, 1f, 0.9f));
		DrawString(font, new Vector2(left + width + 6, JudgeY + 5), Editor.FormatTime(Now), HorizontalAlignment.Left, -1, 13, Colors.White);

		if (_boxSelecting)
		{
			var r = new Rect2(_boxStart, _mouse - _boxStart).Abs();
			DrawRect(r, new Color(0.4f, 0.7f, 1f, 0.15f));
			DrawRect(r, new Color(0.4f, 0.7f, 1f, 0.8f), false, 1.5f);
		}
	}

	private void DrawNote(EditorNote n, int dt, int dl, float alpha, bool selected)
	{
		var lane = Math.Clamp(n.Lane + dl, 0, ManiaBeatmap.KeyCount - 1);
		var laneColor = Skin.Element?.LaneNotes != null && lane < Skin.Element.LaneNotes.Count ? Skin.Element.LaneNotes[lane] * 3 : Colors.White;
		laneColor.A = alpha;
		var r = NoteRect(n, dt, dl);

		if (n.IsHold)
		{
			var top = TimeToY(n.EndTime + dt);
			var body = new Rect2(r.Position.X + r.Size.X * 0.15f, top, r.Size.X * 0.7f, r.Position.Y + r.Size.Y / 2 - top);
			DrawRect(body, new Color(laneColor.R, laneColor.G, laneColor.B, 0.45f * alpha));
		}

		var back = Skin.Element?.NoteBack;
		var fore = Skin.Element?.NoteFore;
		if (back != null)
		{
			DrawTextureRect(back, r, false, laneColor);
			if (fore != null) DrawTextureRect(fore, r, false, new Color(1, 1, 1, alpha));
		}
		else DrawRect(r, laneColor);

		if (selected)
			DrawRect(r.Grow(2), new Color(1f, 0.85f, 0.2f, 1f), false, 2.5f);
	}

	private void DrawBeatGrid(ManiaBeatmap map, double tTop, double tBottom, float left, float width, Font font)
	{
		var tps = map.TimingPoints;
		if (tps.Count == 0) return;
		int div = Math.Max(1, Editor.Divisor);
		int lines = 0;
		for (int i = 0; i < tps.Count && lines < 3000; i++)
		{
			var tp = tps[i];
			double sectionStart = i == 0 ? Math.Min(tp.Offset, tBottom) : tp.Offset;
			double sectionEnd = i + 1 < tps.Count ? tps[i + 1].Offset : double.MaxValue;
			double from = Math.Max(sectionStart, tBottom);
			double to = Math.Min(sectionEnd, tTop);
			if (from >= to) continue;

			double step = tp.BeatLength / div;
			if (step * PxPerMs < 3) step = tp.BeatLength; // too dense: only whole beats
			int subdiv = (int)Math.Round(tp.BeatLength / step);
			long kStart = (long)Math.Ceiling((from - tp.Offset) / step);
			long kEnd = (long)Math.Floor((to - tp.Offset) / step);
			for (long k = kStart; k <= kEnd && lines < 3000; k++, lines++)
			{
				double t = tp.Offset + k * step;
				if (t >= sectionEnd - 0.5) break;
				var y = TimeToY(t);
				var c = TickColor(k, subdiv, tp.Meter, out var thick);
				DrawLine(new Vector2(left, y), new Vector2(left + width, y), c, thick);
				if (thick > 1.5f && k % ((long)subdiv * tp.Meter) == 0)
				{
					long measure = k / ((long)subdiv * tp.Meter);
					DrawString(font, new Vector2(left + width + 6, y + 4), measure.ToString(), HorizontalAlignment.Left, -1, 11, new Color(1, 1, 1, 0.45f));
				}
			}
		}
	}

	private static Color TickColor(long k, int div, int meter, out float thick)
	{
		thick = 1f;
		long r = ((k % div) + div) % div;
		if (r == 0)
		{
			long beat = k / div;
			if (((beat % meter) + meter) % meter == 0) { thick = 2.5f; return MeasureColor; }
			thick = 1.5f;
			return BeatColor;
		}
		int denom = (int)(div / Gcd(r, div));
		return denom switch
		{
			2 => HalfColor,
			3 => ThirdColor,
			4 => QuarterColor,
			6 => SixthColor,
			8 => EighthColor,
			_ => FineColor,
		};
	}

	private static long Gcd(long a, long b) { while (b != 0) (a, b) = (b, a % b); return Math.Abs(a); }

	// ---------- input ----------
	public override void _GuiInput(InputEvent @event)
	{
		if (Editor?.Map == null) return;

		if (@event is InputEventMouseMotion mm)
		{
			_mouse = mm.Position;
			if (_moving) UpdateMove();
			var lane = LaneAt(_mouse.X);
			Editor.OnFieldHover(lane >= 0 ? Editor.SnapTime(YToTime(_mouse.Y)) : -1, lane);
			return;
		}

		if (@event is not InputEventMouseButton mb) return;
		_mouse = mb.Position;

		if (mb.Pressed && (mb.ButtonIndex == MouseButton.WheelUp || mb.ButtonIndex == MouseButton.WheelDown))
		{
			int dir = mb.ButtonIndex == MouseButton.WheelUp ? 1 : -1;
			if (mb.CtrlPressed) Editor.ChangeZoom(dir);
			else if (mb.AltPressed) Editor.ChangeDivisor(dir);
			else Editor.StepSnap(mb.ShiftPressed ? dir * 4 : dir);
			AcceptEvent();
			return;
		}

		if (mb.ButtonIndex == MouseButton.Right && mb.Pressed)
		{
			RemoveAt(mb.Position);
			AcceptEvent();
			return;
		}

		if (mb.ButtonIndex != MouseButton.Left) return;
		AcceptEvent();
		GrabFocus();

		if (mb.Pressed)
		{
			switch (Editor.Tool)
			{
				case EditorTool.Place: PlaceAt(mb.Position); break;
				case EditorTool.Remove: RemoveAt(mb.Position); break;
				case EditorTool.Select: BeginSelect(mb.Position, mb.CtrlPressed || mb.ShiftPressed); break;
			}
		}
		else
		{
			if (_moving) EndMove();
			if (_boxSelecting) EndBox();
		}
	}

	private void PlaceAt(Vector2 pos)
	{
		var lane = LaneAt(pos.X);
		if (lane < 0) return;
		var existing = NoteAt(pos);
		if (existing != null)
		{
			// Clicking an existing note in place mode selects it instead of stacking another.
			Editor.Selection.Clear();
			Editor.Selection.Add(existing);
			return;
		}
		var time = Editor.SnapTime(YToTime(pos.Y));
		if (time < 0) return;
		if (Editor.Map.Notes.Any(n => n.Lane == lane && n.Time == time)) return;
		Editor.PushUndo();
		var note = new EditorNote { Time = time, Lane = lane };
		Editor.Map.Notes.Add(note);
		Editor.Map.SortAll();
		Editor.Selection.Clear();
		Editor.MarkDirty();
		Editor.PlayHitsound();
	}

	private void RemoveAt(Vector2 pos)
	{
		var n = NoteAt(pos);
		if (n == null) return;
		Editor.PushUndo();
		Editor.Map.Notes.Remove(n);
		Editor.Selection.Remove(n);
		Editor.MarkDirty();
	}

	private void BeginSelect(Vector2 pos, bool additive)
	{
		var hit = NoteAt(pos);
		if (hit != null)
		{
			if (additive)
			{
				if (!Editor.Selection.Remove(hit)) Editor.Selection.Add(hit);
				return;
			}
			if (!Editor.Selection.Contains(hit))
			{
				Editor.Selection.Clear();
				Editor.Selection.Add(hit);
			}
			_moving = true;
			_moveStartTime = Editor.SnapTime(YToTime(pos.Y));
			_moveStartLane = LaneAt(pos.X);
			_moveDt = 0;
			_moveDl = 0;
			return;
		}
		_boxSelecting = true;
		_boxAdditive = additive;
		_boxStart = pos;
		if (!additive) Editor.Selection.Clear();
	}

	private void UpdateMove()
	{
		var sel = Editor.Selection;
		if (sel.Count == 0) return;
		_moveDt = Editor.SnapTime(YToTime(_mouse.Y)) - _moveStartTime;
		// Keep the whole group inside the song start and the lanes.
		int minTime = sel.Min(n => n.Time);
		if (minTime + _moveDt < 0) _moveDt = -minTime;
		var lane = LaneAt(_mouse.X);
		if (lane >= 0 && _moveStartLane >= 0)
		{
			int dl = lane - _moveStartLane;
			int minLane = sel.Min(n => n.Lane), maxLane = sel.Max(n => n.Lane);
			_moveDl = Math.Clamp(dl, -minLane, ManiaBeatmap.KeyCount - 1 - maxLane);
		}
	}

	private void EndMove()
	{
		_moving = false;
		if (_moveDt == 0 && _moveDl == 0) return;
		Editor.PushUndo();
		var sel = Editor.Selection;
		foreach (var n in sel)
		{
			n.Time += _moveDt;
			if (n.IsHold) n.EndTime += _moveDt;
			n.Lane += _moveDl;
		}
		// Moved notes replace any unselected note they land on.
		var occupied = sel.Select(n => (n.Time, n.Lane)).ToHashSet();
		Editor.Map.Notes.RemoveAll(n => !sel.Contains(n) && occupied.Contains((n.Time, n.Lane)));
		Editor.Map.SortAll();
		Editor.MarkDirty();
		_moveDt = 0;
		_moveDl = 0;
	}

	private void EndBox()
	{
		_boxSelecting = false;
		var r = new Rect2(_boxStart, _mouse - _boxStart).Abs();
		if (r.Size.Length() < 3) return;
		var t1 = YToTime(r.End.Y);
		var t2 = YToTime(r.Position.Y);
		foreach (var n in Editor.Map.Notes)
		{
			var nx = FieldLeft + (n.Lane + 0.5f) * LaneWidth;
			if (nx < r.Position.X || nx > r.End.X) continue;
			if (n.Time >= t1 && n.Time <= t2) Editor.Selection.Add(n);
		}
	}
}
