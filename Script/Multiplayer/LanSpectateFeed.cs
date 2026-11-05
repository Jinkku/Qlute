using Godot;
using System;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// One player's live play, as seen by a spectator: their chart (sent once at the start of the song) plus a
/// stream of small packets with their song time, key presses and judgements. <see cref="LanPlayfieldView"/>
/// draws it. Events carry the player's song time, so playback replays them at the right moment.
/// </summary>
public class LanSpectateFeed
{
	/// <summary>Spectators run this far behind the newest data so events have arrived before they're shown.</summary>
	public const double PlaybackDelayMs = 120;

	public int[] Times = Array.Empty<int>(); // note time in song ms, ascending-ish (chart order)
	public byte[] Lanes = Array.Empty<byte>();
	/// <summary>The player's screen height when the chart was sent (scales how many ms are on screen).</summary>
	public int ViewHeight = 720;
	public bool HasChart => Times.Length > 0;

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
	public double LastJudgeAt = double.MinValue;
	public int ShownCombo;

	private struct Ev { public float T; public byte Kind; public int A; public int B; } // kind 0 key(A lane, B down), 1 judge(A index, B judge)
	private readonly Queue<Ev> _pending = new();
	private double _shownMs = double.MinValue;

	public void SetChart(int[] times, byte[] lanes, int height)
	{
		Times = times ?? Array.Empty<int>();
		Lanes = lanes ?? Array.Empty<byte>();
		ViewHeight = height > 100 ? height : 720;
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
				LastJudgeAt = e.T;
				ShownCombo = e.B >= 3 ? 0 : ShownCombo + 1;
			}
		}
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
			if (_pending.Count > 4096) _pending.Clear(); // way behind (shouldn't happen): drop the backlog
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

		public void Key(float t, int lane, bool down)
		{
			if ((uint)lane < 4 && _keys.Count < 4096) _keys.Add((t, (byte)(lane | (down ? 0x80 : 0))));
		}

		public void Judge(float t, int index, int judge)
		{
			if (_judges.Count < 4096) _judges.Add((t, index, (byte)Math.Clamp(judge, 0, 3)));
		}

		public void Clear() { _keys.Clear(); _judges.Clear(); }

		public byte[] Flush(float songMs, int combo, int score, float accuracy)
		{
			using var ms = new MemoryStream(32 + _keys.Count * 5 + _judges.Count * 9);
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
			}
			Clear();
			return ms.ToArray();
		}
	}
}
