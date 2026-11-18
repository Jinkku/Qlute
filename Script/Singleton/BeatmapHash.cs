using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

/// <summary>
/// The hash the server recognises a beatmap by (leaderboards and score submission): SHA-256 of the .osu file's bytes
/// with a leading UTF-8 BOM removed and CRLF / lone CR turned into LF, as lowercase hex. An edited or outdated copy of
/// a map has a different hash, so it gets no leaderboard and can't submit. Must match the server's
/// Chikara/beatmaphash.py exactly.
/// Thread-safe; results are cached per file and worked out again when the file's size or write time changes.
/// </summary>
public static class BeatmapHash
{
	private static readonly Dictionary<string, (long Length, DateTime Written, string Hash)> Cache = new();
	private static readonly object Lock = new();

	/// <summary>The hash of the .osu at this path (a disk path or a Godot path), or "" if it can't be read.</summary>
	public static string Of(string path)
	{
		if (string.IsNullOrEmpty(path)) return "";
		try
		{
			string disk = path.StartsWith("res://") || path.StartsWith("user://") ? Godot.ProjectSettings.GlobalizePath(path) : path;
			var info = new FileInfo(disk);
			if (!info.Exists) return "";
			lock (Lock)
			{
				if (Cache.TryGetValue(disk, out var c) && c.Length == info.Length && c.Written == info.LastWriteTimeUtc)
					return c.Hash;
			}
			string hash = Compute(File.ReadAllBytes(disk));
			lock (Lock) Cache[disk] = (info.Length, info.LastWriteTimeUtc, hash);
			return hash;
		}
		catch (Exception e)
		{
			Godot.GD.PrintErr($"Couldn't hash {path}: {e.Message}");
			return "";
		}
	}

	/// <summary>The hash of a .osu file's contents.</summary>
	public static string Compute(byte[] data)
	{
		int start = data.Length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF ? 3 : 0;
		var normal = new byte[data.Length - start];
		int n = 0;
		for (int i = start; i < data.Length; i++)
		{
			byte b = data[i];
			if (b == (byte)'\r')
			{
				normal[n++] = (byte)'\n';
				if (i + 1 < data.Length && data[i + 1] == (byte)'\n') i++; // CRLF -> LF
			}
			else normal[n++] = b;
		}
		return Convert.ToHexString(SHA256.HashData(normal.AsSpan(0, n))).ToLowerInvariant();
	}
}
