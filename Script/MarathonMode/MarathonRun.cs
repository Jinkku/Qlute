using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// One marathon: the songs picked on the Marathon screen, played back to back, and how each went.
/// All three game modes call <see cref="SongFinished"/> at the end of a song, so moving to the next song and
/// ending the run live in one place. (It used to skip the last song and could run past the end of the list.)
/// </summary>
public static class MarathonRun
{
	public class SongResult
	{
		public string Title = "";
		public int Score;
		public double Pp;
		public float Accuracy;
		public int Max, Great, Meh, Bad, MaxCombo;
	}

	public class Summary
	{
		public List<SongResult> Songs = new();
		public int Planned;
		public bool Completed;
		public TimeSpan Time;

		public long TotalScore => Songs.Sum(s => (long)s.Score);
		public double TotalPp => Songs.Sum(s => s.Pp);
		public int Misses => Songs.Sum(s => s.Bad);
		/// <summary>Accuracy over every note of the run (not an average of averages).</summary>
		public float Accuracy
		{
			get
			{
				int max = Songs.Sum(s => s.Max), great = Songs.Sum(s => s.Great), meh = Songs.Sum(s => s.Meh), bad = Songs.Sum(s => s.Bad);
				return max + great + meh + bad == 0 ? 0 : Gameplay.ReloadAccuracy(max, great, meh, bad);
			}
		}

		public string OneLine() =>
			$"{Songs.Count}/{Planned} songs in {Time:h\\:mm\\:ss} · {Accuracy * 100:0.00}% · {TotalScore:N0} score · {TotalPp:N0}pp · {Misses:N0} misses";
	}

	/// <summary>The run in progress (null when there's none).</summary>
	public static Summary Current { get; private set; }
	/// <summary>The last finished or stopped run, shown on the Marathon screen.</summary>
	public static Summary Last { get; private set; }
	private static DateTime _startedAt;

	/// <summary>Starts a run over <paramref name="songIds"/> (indices into SettingsOperator.Beatmaps). False if there's nothing to play.</summary>
	public static bool Begin(Node from, List<int> songIds)
	{
		if (songIds == null || songIds.Count == 0) return false;
		SettingsOperator.Marathon = true;
		SettingsOperator.MarathonMapPaths = new List<int>(songIds);
		SettingsOperator.MarathonID = 0;
		Current = new Summary { Planned = songIds.Count };
		_startedAt = DateTime.UtcNow;
		from.GetNode<SettingsOperator>("/root/SettingsOperator").SelectSongID(songIds[0]);
		SettingsOperator.loopaudio = false;
		from.GetNode<SceneTransition>("/root/Transition").Switch("res://Panels/Screens/SongLoadingScreen.tscn");
		return true;
	}

	/// <summary>A marathon song ended: record it, then load the next one or finish the run.</summary>
	public static void SongFinished(Node from, int score)
	{
		if (Current == null) { Current = new Summary { Planned = SettingsOperator.MarathonMapPaths.Count }; _startedAt = DateTime.UtcNow; }
		Current.Songs.Add(new SongResult
		{
			Title = $"{SettingsOperator.SessionConfig.BeatmapArtist} - {SettingsOperator.SessionConfig.BeatmapTitle} [{SettingsOperator.SessionConfig.BeatmapDifficultyName}]",
			Score = score,
			Pp = SettingsOperator.Gameplaycfg.pp,
			Max = SettingsOperator.Gameplaycfg.Max, Great = SettingsOperator.Gameplaycfg.Great, Meh = SettingsOperator.Gameplaycfg.Meh, Bad = SettingsOperator.Gameplaycfg.Bad, MaxCombo = SettingsOperator.Gameplaycfg.MaxCombo,
			Accuracy = SettingsOperator.Gameplaycfg.Max + SettingsOperator.Gameplaycfg.Great + SettingsOperator.Gameplaycfg.Meh + SettingsOperator.Gameplaycfg.Bad == 0 ? 0
				: Gameplay.ReloadAccuracy(SettingsOperator.Gameplaycfg.Max, SettingsOperator.Gameplaycfg.Great, SettingsOperator.Gameplaycfg.Meh, SettingsOperator.Gameplaycfg.Bad),
		});

		SettingsOperator.MarathonID++;
		var transition = from.GetNode<SceneTransition>("/root/Transition");
		if (SettingsOperator.MarathonID >= SettingsOperator.MarathonMapPaths.Count)
		{
			End(completed: true);
			transition.Switch("res://Panels/Screens/ResultScreenv2.tscn"); // the last song's result; the run's totals are in the notification
			return;
		}
		from.GetNode<SettingsOperator>("/root/SettingsOperator").SelectSongID(SettingsOperator.MarathonMapPaths[SettingsOperator.MarathonID]);
		Notify.Post($"Song {SettingsOperator.MarathonID + 1} of {SettingsOperator.MarathonMapPaths.Count}",
			Description: $"{SettingsOperator.SessionConfig.BeatmapArtist} - {SettingsOperator.SessionConfig.BeatmapTitle}");
		transition.Switch("res://Panels/Screens/SongLoadingScreen.tscn");
	}

	/// <summary>Quit from the pause menu: keep what was played, back to the Marathon screen.</summary>
	public static void Stop(Node from)
	{
		End(completed: false);
		from.GetNode<SceneTransition>("/root/Transition").Switch("res://Panels/Screens/MarathonMode.tscn");
	}

	private static void End(bool completed)
	{
		SettingsOperator.Marathon = false;
		SettingsOperator.loopaudio = true;
		if (Current == null) return;
		Current.Completed = completed;
		Current.Time = DateTime.UtcNow - _startedAt;
		Last = Current;
		Current = null;
		if (Last.Songs.Count == 0) return;
		Notify.Post(completed ? "Marathon complete!" : "Marathon stopped", Description: Last.OneLine(),
			Type: completed ? NotificationIcons.NotificationType.Gift : NotificationIcons.NotificationType.Info);
	}
}
