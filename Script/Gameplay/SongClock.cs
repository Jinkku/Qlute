using Godot;
using System;

/// <summary>
/// Smooth, monotonic song clock shared by every gamemode.
///
/// The raw audio position (playback position + time since last mix) only updates once per mix
/// chunk and wobbles by a few ms each frame. The old clock advanced by frame delta and hard-snapped
/// to the raw position whenever they drifted 50ms apart, which made notes jump backwards one frame
/// and then forward again. This clock still advances by frame delta, but pulls itself toward the
/// audio position a little every frame, so it never visibly jumps or runs backwards. It only snaps
/// on a real discontinuity (seek, restart, long hitch).
/// </summary>
public sealed class SongClock
{
	// Larger than this and we assume a seek/restart/hitch and jump straight to the audio position.
	private const double HardResyncSeconds = 0.2;
	// How quickly drift is corrected (per second). 2 => ~0.5s time constant, invisible to the eye.
	private const double CorrectionRate = 2.0;

	private double _time;
	private bool _synced;

	public double Time => _time;

	public void Reset()
	{
		_time = 0;
		_synced = false;
	}

	/// <summary>Advance the clock. Pass delta = 0 to read the current value without advancing.</summary>
	public double Update(AudioStreamPlayer player, double delta)
	{
		if (player == null || !player.Playing)
		{
			// Headless replay self-test has no audio: run the clock on frame time instead.
			if (ReplaySelfTest.Enabled && Gameplay.Instance is { songstarted: true } && delta > 0)
				_time += delta * (player?.PitchScale ?? 1f);
			return _time;
		}

		double pitch = player.PitchScale;
		double audioPos = player.GetPlaybackPosition() + AudioServer.GetTimeSinceLastMix() * pitch;

		if (!_synced)
		{
			_time = audioPos;
			_synced = true;
			return _time;
		}

		if (delta <= 0) return _time;

		double predicted = _time + delta * pitch;
		double error = audioPos - predicted;

		if (Math.Abs(error) > HardResyncSeconds)
		{
			_time = audioPos;
			return _time;
		}

		// Correction is capped well below delta*pitch, so time always moves forward.
		_time = predicted + error * Math.Min(1.0, delta * CorrectionRate);
		return _time;
	}
}
