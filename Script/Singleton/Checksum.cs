using Godot;
using System;
using System.IO;
using System.Security.Cryptography;

public static class ChecksumUtil
{
    const int CHUNK_SIZE = 8192;

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