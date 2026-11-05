using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace CreateEditor;

/// <summary>
/// A single mania note. Lane is 0-based. EndTime &gt; Time means a hold note
/// (holds are preserved from imported maps; gameplay currently plays them as taps).
/// </summary>
public class EditorNote
{
	public int Time { get; set; }
	public int Lane { get; set; }
	public int EndTime { get; set; } = -1;
	public bool IsHold => EndTime > Time;
	/// <summary>osu! hitsound bitflags (2 whistle, 4 finish, 8 clap).</summary>
	public int HitSound { get; set; } = 0;

	public EditorNote Clone() => new EditorNote { Time = Time, Lane = Lane, EndTime = EndTime, HitSound = HitSound };
}

/// <summary>
/// Uninherited (red line) timing point.
/// </summary>
public class EditorTimingPoint
{
	public double Offset { get; set; }
	public double Bpm { get; set; } = 120;
	public int Meter { get; set; } = 4;
	public bool Kiai { get; set; }
	public int Volume { get; set; } = 100;
	public double BeatLength => 60000.0 / Math.Max(1, Bpm);

	public EditorTimingPoint Clone() => new EditorTimingPoint { Offset = Offset, Bpm = Bpm, Meter = Meter, Kiai = Kiai, Volume = Volume };
}

/// <summary>
/// In-memory mania beatmap that can be loaded from and written to the .osu format the game reads.
/// </summary>
public class ManiaBeatmap
{
	public const int KeyCount = 4;

	public string Title = "Untitled Title";
	public string TitleUnicode = "";
	public string Artist = "Untitled Artist";
	public string ArtistUnicode = "";
	public string Creator = "Unknown Mapper";
	public string Version = "Untitled Beatmap";
	public string Source = "";
	public string Tags = "";
	public int BeatmapID = -1;
	public int BeatmapSetID = -1;
	public float HPDrainRate = 5f;
	public float OverallDifficulty = 5f;
	public int PreviewTime = -1;
	public string SampleSet = "Soft";

	/// <summary>File name of the audio inside the set folder.</summary>
	public string AudioFilename = "";
	/// <summary>File name of the background inside the set folder.</summary>
	public string BackgroundFilename = "";

	public List<EditorTimingPoint> TimingPoints = new();
	public List<EditorNote> Notes = new();

	/// <summary>Inherited (green line) timing points kept verbatim so they survive a round-trip.</summary>
	public List<string> InheritedTimingLines = new();
	/// <summary>[Events] lines other than the background (videos, breaks, storyboard) kept verbatim.</summary>
	public List<string> ExtraEventLines = new();

	private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

	public static int LaneToX(int lane) => (int)Math.Floor(lane * 512.0 / KeyCount + 256.0 / KeyCount);
	public static int XToLane(int x) => Math.Clamp((int)Math.Floor(x * KeyCount / 512.0), 0, KeyCount - 1);

	public void SortAll()
	{
		Notes.Sort((a, b) => a.Time != b.Time ? a.Time.CompareTo(b.Time) : a.Lane.CompareTo(b.Lane));
		TimingPoints.Sort((a, b) => a.Offset.CompareTo(b.Offset));
	}

	/// <summary>The red line in effect at <paramref name="timeMs"/> (or the first one if the time is before every line).</summary>
	public EditorTimingPoint TimingAt(double timeMs)
	{
		if (TimingPoints.Count == 0) return null;
		EditorTimingPoint current = TimingPoints[0];
		foreach (var tp in TimingPoints)
		{
			if (tp.Offset <= timeMs + 0.5) current = tp;
			else break;
		}
		return current;
	}

	public static ManiaBeatmap CreateDefault()
	{
		var map = new ManiaBeatmap();
		map.TimingPoints.Add(new EditorTimingPoint { Offset = 0, Bpm = 120, Meter = 4 });
		return map;
	}

	public static ManiaBeatmap Load(string path)
	{
		var map = new ManiaBeatmap();
		string section = "";
		var seen = new HashSet<(int, int)>();
		foreach (var raw in File.ReadAllLines(path))
		{
			var line = raw.Trim();
			if (line.Length == 0 || line.StartsWith("//")) continue;
			if (line.StartsWith("[") && line.EndsWith("]")) { section = line; continue; }

			switch (section)
			{
				case "[General]":
				case "[Metadata]":
				case "[Difficulty]":
					ParseKeyValue(map, line);
					break;
				case "[Events]":
					if (line.StartsWith("0,0,\""))
					{
						var parts = line.Split('"');
						if (parts.Length > 1 && string.IsNullOrEmpty(map.BackgroundFilename))
						{
							map.BackgroundFilename = parts[1].Trim();
							break;
						}
					}
					map.ExtraEventLines.Add(line);
					break;
				case "[TimingPoints]":
				{
					var p = line.Split(',');
					if (p.Length < 2) break;
					if (!double.TryParse(p[0], NumberStyles.Float, Inv, out var t)) break;
					if (!double.TryParse(p[1], NumberStyles.Float, Inv, out var beatLen)) break;
					bool uninherited = p.Length < 7 || p[6].Trim() == "1";
					if (!uninherited || beatLen <= 0) { map.InheritedTimingLines.Add(line); break; }
					var tp = new EditorTimingPoint { Offset = t, Bpm = Math.Round(60000.0 / beatLen, 3) };
					if (p.Length > 2 && int.TryParse(p[2], out var meter) && meter > 0) tp.Meter = meter;
					if (p.Length > 5 && int.TryParse(p[5], out var vol)) tp.Volume = vol;
					if (p.Length > 7 && int.TryParse(p[7], out var fx)) tp.Kiai = (fx & 1) == 1;
					map.TimingPoints.Add(tp);
					break;
				}
				case "[HitObjects]":
				{
					var p = line.Split(',');
					if (p.Length < 4) break;
					if (!int.TryParse(p[0], out var x) || !int.TryParse(p[2], out var time)) break;
					int.TryParse(p[3], out var type);
					int hs = 0;
					if (p.Length > 4) int.TryParse(p[4], out hs);
					var note = new EditorNote { Time = time, Lane = XToLane(x), HitSound = hs };
					// Mania hold: type bit 128, end time is the first field of the extras ("end:sampleset:...").
					if ((type & 128) != 0 && p.Length > 5 && int.TryParse(p[5].Split(':')[0], out var end) && end > time)
						note.EndTime = end;
					if (!seen.Add((note.Time, note.Lane))) break; // same as gameplay: duplicates are ignored
					map.Notes.Add(note);
					break;
				}
			}
		}
		map.SortAll();
		return map;
	}

	private static void ParseKeyValue(ManiaBeatmap map, string line)
	{
		var idx = line.IndexOf(':');
		if (idx < 0) return;
		var key = line[..idx].Trim();
		var value = line[(idx + 1)..].Trim();
		switch (key)
		{
			case "AudioFilename": map.AudioFilename = value; break;
			case "PreviewTime": map.PreviewTime = int.TryParse(value, out var pt) ? pt : -1; break;
			case "SampleSet": map.SampleSet = value; break;
			case "Title": map.Title = value; break;
			case "TitleUnicode": map.TitleUnicode = value; break;
			case "Artist": map.Artist = value; break;
			case "ArtistUnicode": map.ArtistUnicode = value; break;
			case "Creator": map.Creator = value; break;
			case "Version": map.Version = value; break;
			case "Source": map.Source = value; break;
			case "Tags": map.Tags = value; break;
			case "BeatmapID": map.BeatmapID = int.TryParse(value, out var bid) ? bid : -1; break;
			case "BeatmapSetID": map.BeatmapSetID = int.TryParse(value, out var sid) ? sid : -1; break;
			case "HPDrainRate": map.HPDrainRate = float.TryParse(value, NumberStyles.Float, Inv, out var hp) ? hp : 5; break;
			case "OverallDifficulty": map.OverallDifficulty = float.TryParse(value, NumberStyles.Float, Inv, out var od) ? od : 5; break;
		}
	}

	public string Serialize()
	{
		SortAll();
		var sb = new StringBuilder();
		sb.Append("osu file format v14\n\n");

		sb.Append("[General]\n");
		sb.Append($"AudioFilename: {AudioFilename}\n");
		sb.Append("AudioLeadIn: 0\n");
		sb.Append($"PreviewTime: {PreviewTime}\n");
		sb.Append("Countdown: 0\n");
		sb.Append($"SampleSet: {SampleSet}\n");
		sb.Append("StackLeniency: 0.7\n");
		sb.Append("Mode: 3\n");
		sb.Append("LetterboxInBreaks: 0\n");
		sb.Append("SpecialStyle: 0\n");
		sb.Append("WidescreenStoryboard: 1\n\n");

		sb.Append("[Editor]\n");
		sb.Append("DistanceSpacing: 1\n");
		sb.Append("BeatDivisor: 4\n");
		sb.Append("GridSize: 4\n");
		sb.Append("TimelineZoom: 1\n\n");

		sb.Append("[Metadata]\n");
		sb.Append($"Title:{Clean(Title)}\n");
		sb.Append($"TitleUnicode:{Clean(string.IsNullOrWhiteSpace(TitleUnicode) ? Title : TitleUnicode)}\n");
		sb.Append($"Artist:{Clean(Artist)}\n");
		sb.Append($"ArtistUnicode:{Clean(string.IsNullOrWhiteSpace(ArtistUnicode) ? Artist : ArtistUnicode)}\n");
		sb.Append($"Creator:{Clean(Creator)}\n");
		sb.Append($"Version:{Clean(Version)}\n");
		sb.Append($"Source:{Clean(Source)}\n");
		sb.Append($"Tags:{Clean(Tags)}\n");
		sb.Append($"BeatmapID:{BeatmapID}\n");
		sb.Append($"BeatmapSetID:{BeatmapSetID}\n\n");

		sb.Append("[Difficulty]\n");
		sb.Append($"HPDrainRate:{HPDrainRate.ToString("0.#", Inv)}\n");
		sb.Append($"CircleSize:{KeyCount}\n");
		sb.Append($"OverallDifficulty:{OverallDifficulty.ToString("0.#", Inv)}\n");
		sb.Append("ApproachRate:5\n");
		sb.Append("SliderMultiplier:1.4\n");
		sb.Append("SliderTickRate:1\n\n");

		sb.Append("[Events]\n");
		sb.Append("//Background and Video events\n");
		if (!string.IsNullOrEmpty(BackgroundFilename))
			sb.Append($"0,0,\"{BackgroundFilename}\",0,0\n");
		foreach (var l in ExtraEventLines) sb.Append(l).Append('\n');
		sb.Append('\n');

		sb.Append("[TimingPoints]\n");
		var timingLines = new List<(double t, int order, string line)>();
		foreach (var tp in TimingPoints)
		{
			var line = string.Join(",",
				tp.Offset.ToString("0.###", Inv),
				tp.BeatLength.ToString("0.############", Inv),
				tp.Meter.ToString(Inv),
				"1", "0",
				tp.Volume.ToString(Inv),
				"1",
				tp.Kiai ? "1" : "0");
			timingLines.Add((tp.Offset, 0, line));
		}
		foreach (var l in InheritedTimingLines)
		{
			double.TryParse(l.Split(',')[0], NumberStyles.Float, Inv, out var t);
			timingLines.Add((t, 1, l));
		}
		foreach (var tl in timingLines.OrderBy(x => x.t).ThenBy(x => x.order))
			sb.Append(tl.line).Append('\n');
		sb.Append('\n');

		sb.Append("[HitObjects]\n");
		foreach (var n in Notes)
		{
			int x = LaneToX(n.Lane);
			if (n.IsHold)
				sb.Append($"{x},192,{n.Time},128,{n.HitSound},{n.EndTime}:0:0:0:0:\n");
			else
				sb.Append($"{x},192,{n.Time},1,{n.HitSound},0:0:0:0:\n");
		}
		return sb.ToString();
	}

	/// <summary>Metadata values must stay on one line.</summary>
	private static string Clean(string s) => (s ?? "").Replace("\r", " ").Replace("\n", " ").Trim();

	/// <summary>Make a string safe to use as a file or folder name on every OS.</summary>
	public static string SafeFileName(string s)
	{
		var invalid = Path.GetInvalidFileNameChars().Concat(new[] { '/', '\\', ':', '*', '?', '"', '<', '>', '|' }).ToHashSet();
		var sb = new StringBuilder();
		foreach (var c in s ?? "")
			sb.Append(invalid.Contains(c) || char.IsControl(c) ? '_' : c);
		var result = sb.ToString().Trim().TrimEnd('.');
		return string.IsNullOrEmpty(result) ? "Untitled" : result;
	}

	public string DifficultyFileName() =>
		SafeFileName($"{Artist} - {Title} ({Creator}) [{Version}]") + ".osu";

	public string SetFolderName() => SafeFileName($"{Artist} - {Title}");
}
