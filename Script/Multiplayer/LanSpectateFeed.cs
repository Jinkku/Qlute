using Godot;
using System;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// A player's chart as sent to spectators, for any game mode. Sent once per song as one blob.
/// Mania: Lanes = lane 0..3. Taiko: Lanes = drum (0 don, 1 kat) + 2 when big.
/// Tap!: Lanes = object type (0 circle, 1 slider, 2 spinner) in the low 2 bits + slider repeats &lt;&lt; 2, Ends = end time, and each object's points
/// (osu! playfield units) are Pts[PtStart[i]*2 ..] up to the next object's start: one for circles / spinners,
/// a few samples along the path for sliders.
/// </summary>
public class LanSpecChart
{
	public int Mode;
	public int Height = 720;
	public int[] Times = Array.Empty<int>();
	public byte[] Lanes = Array.Empty<byte>();
	public int[] Ends = Array.Empty<int>();
	public int[] PtStart = Array.Empty<int>();
	public float[] Pts = Array.Empty<float>();
	public float Preempt = 600, Radius = 36;

	public int PointCount(int i) => (i + 1 < PtStart.Length ? PtStart[i + 1] : Pts.Length / 2) - PtStart[i];
	public Vector2 Point(int i, int k) { int p = (PtStart[i] + k) * 2; return new Vector2(Pts[p], Pts[p + 1]); }

	public byte[] Encode()
	{
		using var ms = new MemoryStream(32 + Times.Length * 13 + Pts.Length * 4);
		using (var w = new BinaryWriter(ms))
		{
			w.Write(Mode); w.Write(Height); w.Write(Preempt); w.Write(Radius);
			w.Write(Times.Length);
			for (int i = 0; i < Times.Length; i++) { w.Write(Times[i]); w.Write(Lanes[i]); }
			w.Write(Ends.Length);
			foreach (var e in Ends) w.Write(e);
			w.Write(PtStart.Length);
			foreach (var p in PtStart) w.Write(p);
			w.Write(Pts.Length);
			foreach (var p in Pts) w.Write(p);
		}
		return ms.ToArray();
	}

	/// <summary>Null when the blob is malformed or over <paramref name="maxNotes"/>.</summary>
	public static LanSpecChart Decode(byte[] data, int maxNotes)
	{
		try
		{
			using var r = new BinaryReader(new MemoryStream(data));
			var c = new LanSpecChart { Mode = r.ReadInt32(), Height = r.ReadInt32(), Preempt = r.ReadSingle(), Radius = r.ReadSingle() };
			int n = r.ReadInt32();
			if (n < 0 || n > maxNotes) return null;
			c.Times = new int[n]; c.Lanes = new byte[n];
			for (int i = 0; i < n; i++) { c.Times[i] = r.ReadInt32(); c.Lanes[i] = r.ReadByte(); }
			int ne = r.ReadInt32();
			if (ne != 0 && ne != n) return null;
			c.Ends = new int[ne];
			for (int i = 0; i < ne; i++) c.Ends[i] = r.ReadInt32();
			int ns = r.ReadInt32();
			if (ns != 0 && ns != n) return null;
			c.PtStart = new int[ns];
			for (int i = 0; i < ns; i++) c.PtStart[i] = r.ReadInt32();
			int np = r.ReadInt32();
			if (np < 0 || np > maxNotes * 64 || np % 2 != 0) return null;
			c.Pts = new float[np];
			for (int i = 0; i < np; i++) c.Pts[i] = r.ReadSingle();
			for (int i = 0; i < ns; i++)
				if (c.PtStart[i] < 0 || c.PtStart[i] * 2 >= np || (i > 0 && c.PtStart[i] < c.PtStart[i - 1])) return null;
			if (!float.IsFinite(c.Preempt) || !float.IsFinite(c.Radius)) return null;
			return c;
		}
		catch { return null; }
	}
}

/// <summary>
/// One player's live play, as seen by a spectator: their chart (sent once at the start of the song) plus a
/// stream of small packets with their song time, key presses and judgements. <see cref="LanPlayfieldView"/>
/// draws it. Events carry the player's song time, so playback replays them at the right moment.
/// </summary>
public class LanSpectateFeed
{
	/// <summary>Spectators run this far behind the newest data so events have arrived before they're shown.</summary>
	public const double PlaybackDelayMs = 120;

	public LanSpecChart Chart = new();
	public int Mode => Chart.Mode;
	public int[] Times => Chart.Times; // note time in song ms, ascending-ish (chart order)
	public byte[] Lanes => Chart.Lanes;
	/// <summary>The player's screen height when the chart was sent (scales how many ms are on screen).</summary>
	public int ViewHeight => Chart.Height > 100 ? Chart.Height : 720;
	public bool HasChart => Times.Length > 0;

	// Tap! cursor, played back between the two samples around the display time.
	public Vector2 Cursor;
	public bool HasCursor;
	private (float T, Vector2 P) _cursorPrev;
	private readonly Queue<(float T, Vector2 P)> _cursorNext = new();

	// Latest sample from the player.
	public double SongMs;
	public ulong SampleAtMs;
	public bool HasSample;
	public int Combo, Score;
	public float Accuracy = 1f;

	// Played back state (what the view shows at the current display time).
	public readonly bool[] LaneDown = new bool[4];
	public readonly double[] LaneDownAt = new double[4];
	public bool[] Judged = Array.Empty<bool>();
	public int LastJudge = -1;
	public int LastJudgeIndex = -1;
	public double LastJudgeAt = double.MinValue;
	public int ShownCombo;

	private struct Ev { public float T; public byte Kind; public int A; public int B; } // kind 0 key(A lane, B down), 1 judge(A index, B judge)
	private readonly Queue<Ev> _pending = new();
	private double _shownMs = double.MinValue;

	public void SetChart(LanSpecChart chart)
	{
		Chart = chart ?? new LanSpecChart();
		Judged = new bool[Times.Length];
	}

	/// <summary>Song time to draw right now: the newest sample moved forward by the time since it arrived.</summary>
	public double DisplayMs(float speed)
	{
		if (!HasSample) return double.MinValue;
		double ms = SongMs + (Time.GetTicksMsec() - SampleAtMs) * speed - PlaybackDelayMs;
		if (ms < _shownMs) ms = _shownMs; // never run backwards (jitter)
		return ms;
	}

	/// <summary>Plays back every event up to <paramref name="displayMs"/>.</summary>
	public void Advance(double displayMs)
	{
		_shownMs = displayMs;
		while (_pending.Count > 0 && _pending.Peek().T <= displayMs)
		{
			var e = _pending.Dequeue();
			if (e.Kind == 0 && (uint)e.A < 4)
			{
				LaneDown[e.A] = e.B != 0;
				if (e.B != 0) LaneDownAt[e.A] = e.T;
			}
			else if (e.Kind == 1)
			{
				if ((uint)e.A < (uint)Judged.Length) Judged[e.A] = true;
				LastJudge = e.B;
				LastJudgeIndex = e.A;
				LastJudgeAt = e.T;
				ShownCombo = e.B >= 3 ? 0 : ShownCombo + 1;
			}
		}

		while (_cursorNext.Count > 0 && _cursorNext.Peek().T <= displayMs) { _cursorPrev = _cursorNext.Dequeue(); HasCursor = true; }
		if (!HasCursor) return;
		if (_cursorNext.Count > 0)
		{
			var next = _cursorNext.Peek();
			float span = next.T - _cursorPrev.T;
			float k = span > 0 ? Mathf.Clamp((float)(displayMs - _cursorPrev.T) / span, 0, 1) : 1;
			Cursor = _cursorPrev.P.Lerp(next.P, k);
		}
		else Cursor = _cursorPrev.P;
	}

	/// <summary>Decodes a packet from <see cref="Writer.Flush"/>.</summary>
	public void Apply(byte[] packet)
	{
		try
		{
			using var r = new BinaryReader(new MemoryStream(packet));
			SongMs = r.ReadSingle();
			Combo = r.ReadInt32();
			Score = r.ReadInt32();
			Accuracy = r.ReadSingle();
			SampleAtMs = Time.GetTicksMsec();
			HasSample = true;
			int keys = r.ReadUInt16();
			for (int i = 0; i < keys; i++)
			{
				float t = r.ReadSingle();
				byte b = r.ReadByte();
				_pending.Enqueue(new Ev { T = t, Kind = 0, A = b & 0x7f, B = b >> 7 });
			}
			int judges = r.ReadUInt16();
			for (int i = 0; i < judges; i++)
			{
				float t = r.ReadSingle();
				int index = r.ReadInt32();
				byte judge = r.ReadByte();
				_pending.Enqueue(new Ev { T = t, Kind = 1, A = index, B = judge });
			}
			int cursors = r.ReadUInt16();
			for (int i = 0; i < cursors; i++)
			{
				float t = r.ReadSingle();
				var p = new Vector2(r.ReadSingle(), r.ReadSingle());
				if (float.IsFinite(p.X) && float.IsFinite(p.Y)) _cursorNext.Enqueue((t, p));
			}
			if (_pending.Count > 4096) _pending.Clear(); // way behind (shouldn't happen): drop the backlog
			while (_cursorNext.Count > 512) _cursorNext.Dequeue();
		}
		catch (Exception e)
		{
			GD.PrintErr($"[LAN] Bad spectator packet: {e.Message}");
		}
	}

	/// <summary>Player side: collects inputs between ticks and packs them.</summary>
	public class Writer
	{
		private readonly List<(float T, byte B)> _keys = new();
		private readonly List<(float T, int Index, byte Judge)> _judges = new();
		private readonly List<(float T, Vector2 P)> _cursor = new();
		private const float CursorStepMs = 16; // ~60 samples a second is plenty to interpolate between

		/// <summary>Tap!: the cursor position (playfield units) at song time <paramref name="t"/>.</summary>
		public void Cursor(float t, Vector2 p)
		{
			if (_cursor.Count > 0 && Math.Abs(t - _cursor[^1].T) < CursorStepMs) return;
			if (_cursor.Count < 1024) _cursor.Add((t, p));
		}

		public void Key(float t, int lane, bool down)
		{
			if ((uint)lane < 4 && _keys.Count < 4096) _keys.Add((t, (byte)(lane | (down ? 0x80 : 0))));
		}

		public void Judge(float t, int index, int judge)
		{
			if (_judges.Count < 4096) _judges.Add((t, index, (byte)Math.Clamp(judge, 0, 3)));
		}

		public void Clear() { _keys.Clear(); _judges.Clear(); _cursor.Clear(); }

		public byte[] Flush(float songMs, int combo, int score, float accuracy)
		{
			using var ms = new MemoryStream(34 + _keys.Count * 5 + _judges.Count * 9 + _cursor.Count * 12);
			using (var w = new BinaryWriter(ms))
			{
				w.Write(songMs);
				w.Write(combo);
				w.Write(score);
				w.Write(accuracy);
				w.Write((ushort)_keys.Count);
				foreach (var k in _keys) { w.Write(k.T); w.Write(k.B); }
				w.Write((ushort)_judges.Count);
				foreach (var j in _judges) { w.Write(j.T); w.Write(j.Index); w.Write(j.Judge); }
				w.Write((ushort)_cursor.Count);
				foreach (var c in _cursor) { w.Write(c.T); w.Write(c.P.X); w.Write(c.P.Y); }
			}
			Clear();
			return ms.ToArray();
		}
	}
}
