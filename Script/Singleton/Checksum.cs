using Godot;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

public static class ChecksumUtil
{
    const int CHUNK_SIZE = 8192;

    public static string GetGameChecksum()
    {
        return ComputeSHA256(OS.GetExecutablePath());
    }

    static string ChecksumCacheFile => SettingsOperator.homedir + "/exehash.txt";

    static readonly System.Threading.Tasks.TaskCompletionSource GameChecksumDone =
        new(System.Threading.Tasks.TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Finishes once the exe hash is known (or failed), so login can wait for the real User-Agent.</summary>
    public static System.Threading.Tasks.Task GameChecksumReady => GameChecksumDone.Task;

    /// <summary>
    /// Hashes the game exe (the server checks it on score submit) without blocking startup.
    /// The result is cached against the exe's path, size and write time, so it's only
    /// recomputed after an update. ApiOperator's User-Agent is refreshed when it's ready.
    /// </summary>
    public static void StartGameChecksum()
    {
        string exe = OS.GetExecutablePath();
        string stamp;
        try
        {
            var info = new FileInfo(exe);
            stamp = $"{exe}|{info.Length}|{info.LastWriteTimeUtc.Ticks}";
        }
        catch (Exception e)
        {
            GD.PrintErr("Game checksum: couldn't stat exe: " + e.Message);
            GameChecksumDone.TrySetResult();
            return;
        }

        try
        {
            if (File.Exists(ChecksumCacheFile))
            {
                var lines = File.ReadAllLines(ChecksumCacheFile);
                if (lines.Length >= 2 && lines[0] == stamp && lines[1].Length == 64)
                {
                    SetGameChecksum(lines[1]);
                    return;
                }
            }
        }
        catch (Exception) { /* fall through and rehash */ }

        System.Threading.Tasks.Task.Run(() =>
        {
            try
            {
                string hash = ComputeSHA256(exe);
                SetGameChecksum(hash);
                string tmp = ChecksumCacheFile + ".tmp";
                File.WriteAllLines(tmp, new[] { stamp, hash });
                File.Move(tmp, ChecksumCacheFile, overwrite: true);
            }
            catch (Exception e)
            {
                GD.PrintErr("Game checksum failed: " + e.Message);
                GameChecksumDone.TrySetResult();
            }
        });
    }

    static void SetGameChecksum(string hash)
    {
        SettingsOperator.GameChecksum = hash;
        ApiOperator.RefreshAgentString();
        GameChecksumDone.TrySetResult();
    }
    
    public static string ComputeSHA256(string filePath)
    {
        using (var sha256 = SHA256.Create())
        using (var stream = File.OpenRead(filePath)) 
        {
            byte[] hashBytes = sha256.ComputeHash(stream);
            StringBuilder sb = new StringBuilder();
            foreach (byte b in hashBytes) 
                sb.Append(b.ToString("x2")); 
            return sb.ToString();
        }
    }
    // SHA-256 (recommended)
    public static string GetSha256(string path)
    {
        try
        {
            if (path.StartsWith("res://") || path.StartsWith("user://"))
                return ComputeHashGodotFile(path, SHA256.Create());
            else
                return ComputeHashFileStream(path, SHA256.Create());
        }
        catch (Exception e)
        {
            GD.PrintErr("GetSha256 error: " + e);
            return null;
        }
    }

    // MD5 (only if you really need it)
    public static string GetMd5(string path)
    {
        try
        {
            if (path.StartsWith("res://") || path.StartsWith("user://"))
                return ComputeHashGodotFile(path, MD5.Create());
            else
                return ComputeHashFileStream(path, MD5.Create());
        }
        catch (Exception e)
        {
            GD.PrintErr("GetMd5 error: " + e);
            return null;
        }
    }

    static string ComputeHashFileStream(string path, HashAlgorithm algo)
    {
        using (algo)
        using (var stream = File.OpenRead(path))
        {
            var hash = algo.ComputeHash(stream);
            return BytesToHex(hash);
        }
    }

    // Helper: compute from Godot FileAccess (works for res:// and user://, and packed files)
    static string ComputeHashGodotFile(string path, HashAlgorithm algo)
    {
        using (algo)
        using (var fa = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read))
        {
            long len = (long)fa.GetLength();
            long read = 0;
            // If the file is small you could read all at once, but chunking is safer
            while (read < len)
            {
                int toRead = (int)Math.Min(CHUNK_SIZE, len - read);
                byte[] buffer = fa.GetBuffer(toRead); // returns bytes read
                if (buffer == null || buffer.Length == 0) break;
                algo.TransformBlock(buffer, 0, buffer.Length, null, 0);
                read += buffer.Length;
            }
            // finalize hash
            algo.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            return BytesToHex(algo.Hash);
        }
    }

    static string BytesToHex(byte[] bytes)
    {
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}