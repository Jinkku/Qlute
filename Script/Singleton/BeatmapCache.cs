using Godot;
using System;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// On-disk cache of parsed beatmap metadata (beatmapcache.bin in the game folder) so startup
/// doesn't re-parse every .osu file. An entry is reused only while the file's size and
/// modification time are unchanged. Per-note data (ppv2sets, Dance) is not cached; it is read
/// lazily when a map is played.
/// Bump <see cref="Version"/> whenever SettingsOperator.ParseBeatmapLegend changes what it produces.
/// </summary>
public static class BeatmapCache
{
	private const int Magic = 0x51424331; // "QBC1"
	private const int Version = 2; // 2: pp v3, 3: steeper pp curve, 4: level = f(pp) 1..100, 5: level curve 0.8, 6: pp scale 1.13

	public static string FilePath => SettingsOperator.homedir + "/beatmapcache.bin";

	public record struct Stamp(long Length, long WriteTicks);

	public static Stamp StampOf(FileInfo f) => new(f.Length, f.LastWriteTimeUtc.Ticks);

	public static Dictionary<string, (Stamp stamp, BeatmapLegend legend)> Load()
	{
		var result = new Dictionary<string, (Stamp, BeatmapLegend)>();
		try
		{
			if (!File.Exists(FilePath)) return result;
			using var r = new BinaryReader(File.OpenRead(FilePath));
			if (r.ReadInt32() != Magic || r.ReadInt32() != Version) return result;
			int count = r.ReadInt32();
			for (int i = 0; i < count; i++)
			{
				string path = r.ReadString();
				var stamp = new Stamp(r.ReadInt64(), r.ReadInt64());
				var b = new BeatmapLegend
				{
					Title = Str(r), TitleUnicode = Str(r), Artist = Str(r), ArtistUnicode = Str(r),
					SampleSet = Str(r), Mapper = Str(r), Version = Str(r), Background = Str(r), Audio = Str(r),
					Path = Str(r), Rawurl = path,
					KeyCount = r.ReadInt32(), BeatmapID = r.ReadInt32(), BeatmapSetID = r.ReadInt32(),
					NoteCount = r.ReadInt32(), GameModeID = r.ReadInt32(),
					pp = r.ReadSingle(), Bpm = r.ReadSingle(), PreviewTime = r.ReadSingle(),
					Timetotal = r.ReadSingle(), Accuracy = r.ReadSingle(),
				};
				b.Levelrating = SettingsOperator.GetLevelRating(b.pp);
				result[path] = (stamp, b);
			}
		}
		catch (Exception e)
		{
			GD.PrintErr($"[Qlute] Beatmap cache unreadable, rebuilding: {e.Message}");
			result.Clear();
		}
		return result;
	}

	public static void Save(List<(string path, Stamp stamp, BeatmapLegend legend)> entries)
	{
		string tmp = FilePath + ".tmp";
		try
		{
			using (var w = new BinaryWriter(File.Create(tmp)))
			{
				w.Write(Magic);
				w.Write(Version);
				w.Write(entries.Count);
				foreach (var (path, stamp, b) in entries)
				{
					w.Write(path);
					w.Write(stamp.Length);
					w.Write(stamp.WriteTicks);
					foreach (var s in new[] { b.Title, b.TitleUnicode, b.Artist, b.ArtistUnicode, b.SampleSet, b.Mapper, b.Version, b.Background, b.Audio, b.Path })
					{
						w.Write(s != null);
						w.Write(s ?? "");
					}
					w.Write(b.KeyCount); w.Write(b.BeatmapID); w.Write(b.BeatmapSetID); w.Write(b.NoteCount); w.Write(b.GameModeID);
					w.Write(b.pp); w.Write(b.Bpm); w.Write(b.PreviewTime); w.Write(b.Timetotal); w.Write(b.Accuracy);
				}
			}
			File.Move(tmp, FilePath, overwrite: true);
		}
		catch (Exception e)
		{
			GD.PrintErr($"[Qlute] Couldn't save the beatmap cache: {e.Message}");
		}
	}

	private static string Str(BinaryReader r)
	{
		bool has = r.ReadBoolean();
		string s = r.ReadString();
		return has ? s : null;
	}
}
