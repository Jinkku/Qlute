using System;
using System.IO;
using System.Collections.Generic;
using Godot;
using System.Linq;

/// <summary>
/// This is for Leaderboards, ReplayInfo class and ReplayLegend are two different things!
/// This is used from the data in the replay to show the stats from the leaderboards.
/// </summary>
public class ReplayInfo
{
    public string Username { get; set; } = "Unknown";
    public int BeatmapID { get; set; } = 0;
    public int BeatmapSetID { get; set; } = 0;
    public int Score { get; set; } = 0;
    public int Max { get; set; } = 0;
    public int Great { get; set; } = 0;
    public int Meh { get; set; } = 0;
    public int Bad { get; set; } = 0;
    public float Accuracy { get; set; } = 100.00f;
    public int MaxCombo { get; set; } = 0;
    public float Avgms { get; set; } = 0;
    public string Mods { get; set; } = "";
    public double Points { get; set; } = 0;
    /// <summary>
    /// DT/HT Multiplier
    /// </summary>
    public float Multiplier { get; set; }
    public string Filepath { get; set; }
}
/// <summary>
/// This is for Gameplay, ReplayInfo class and ReplayLegend are two different things!
/// This is the core part to simulate the player's input.
/// </summary>
public class ReplayLegend
{
    public int Time;
    public int NoteTap;
}
public static class Replay
{
    public static string FilePath { get; set; }
    /// <summary>Online leaderboard score being viewed; its replay is downloaded on "Watch Replay".</summary>
    public static LeaderboardEntry OnlineEntry { get; set; }
    public static string FileCache { get; set; }
    public static List<ReplayLegend> ReplayCache = new List<ReplayLegend>();
    public static List<ReplayInfo> Replays { get; set; } = new List<ReplayInfo>();

    /// <summary>
    /// initialize Replay subsystem. 
    /// </summary>
    public static void Init()
    {
        GD.Print("[Qlute] Init replay subsystem...");
        if (!Directory.Exists(SettingsOperator.replaydir)) return;
        foreach (string replay in Directory.GetFiles(SettingsOperator.replaydir))
        {
            Parse(replay);
        }
    }
    /// <summary>
    /// Finds the replay with the specified ID example: Replay.Search(192834)
    /// </summary>
    public static List<LeaderboardEntry> Search(int ID)
    {
        List<LeaderboardEntry> List = new List<LeaderboardEntry>();
        var selectedBeatmaps = Replays.Where(b => b.BeatmapID == ID).ToList();
		selectedBeatmaps = selectedBeatmaps.OrderByDescending(entry => entry.Score).ToList();
        foreach (ReplayInfo Entry in selectedBeatmaps)
        {
            DateTime lastWrite = System.IO.File.GetLastWriteTime(Entry.Filepath).ToLocalTime();
            long Time = new DateTimeOffset(lastWrite).ToUnixTimeSeconds();
            List.Add(new LeaderboardEntry
            {
                username = Entry.Username,
                score = Entry.Score,
                combo = Entry.MaxCombo,
                Accuracy = Entry.Accuracy,
                MAX = Entry.Max,
                GOOD = Entry.Great,
                MEH = Entry.Meh,
                BAD = Entry.Bad,
                mods = Entry.Mods,
                points = Entry.Points,
                speed_multi = Entry.Multiplier,
                time = Time,
                FilePath = Entry.Filepath
            });
        }
        return List;
    }
    /// <summary>
    /// Parses to Replay Storage.
    /// </summary>
    public static void Parse(string file)
    {
        try
        {
            if (File.Exists(file))
            {
                GD.Print($"[Qlute] Found '{file}' Processing it....");
                ReplayInfo Replayman = new ReplayInfo {Filepath = file};
                // Only the "#" header is needed here, so stop at the first input line instead of reading the whole file
                using var reader = new StreamReader(file);
                string data;
                while ((data = reader.ReadLine()) != null)
                {
                    data = data.TrimEnd('\r');
                    if (data.StartsWith("#"))
                    {
                        if (data.StartsWith("#Username: "))
                        {
                            Replayman.Username = data.Replace("#Username: ", "");
                        }
                        else if (data.StartsWith("#BeatmapID: "))
                        {
                            Replayman.BeatmapID = Int32.Parse(data.Replace("#BeatmapID: ", ""));
                        }
                        else if (data.StartsWith("#BeatmapSetID: "))
                        {
                            Replayman.BeatmapSetID = Int32.Parse(data.Replace("#BeatmapSetID: ", ""));
                        }
                        else if (data.StartsWith("#Score: "))
                        {
                            Replayman.Score = Int32.Parse(data.Replace("#Score: ", ""));
                        }
                        else if (data.StartsWith("#Max: "))
                        {
                            Replayman.Max = Int32.Parse(data.Replace("#Max: ", ""));
                        }
                        else if (data.StartsWith("#Great: "))
                        {
                            Replayman.Great = Int32.Parse(data.Replace("#Great: ", ""));
                        }
                        else if (data.StartsWith("#Meh: "))
                        {
                            Replayman.Meh = Int32.Parse(data.Replace("#Meh: ", ""));
                        }
                        else if (data.StartsWith("#Bad: "))
                        {
                            Replayman.Bad = Int32.Parse(data.Replace("#Bad: ", ""));
                        }
                        else if (data.StartsWith("#Accuracy: "))
                        {
                            Replayman.Accuracy = Num.Float(data.Replace("#Accuracy: ", ""), commaFallback: true);
                        }
                        else if (data.StartsWith("#Max Combo: "))
                        {
                            Replayman.MaxCombo = Int32.Parse(data.Replace("#Max Combo: ", ""));
                        }
                        else if (data.StartsWith("#Average ms: "))
                        {
                            Replayman.Avgms = Num.Float(data.Replace("#Average ms: ", ""), commaFallback: true);
                        }
                        else if (data.StartsWith("#Mods: "))
                        {
                            Replayman.Mods = data.Replace("#Mods: ", "");
                        }
                        else if (data.StartsWith("#Points: "))
                        {
                            Replayman.Points = Num.Double(data.Replace("#Points: ", ""), commaFallback: true);
                        }
                        else if (data.StartsWith("#Multiplier: "))
                        {
                            Replayman.Multiplier = Num.Float(data.Replace("#Multiplier: ", ""), 1, commaFallback: true); // replays from comma-decimal PCs say "1,5"
                        }
                    }
                    else
                    {
                        GD.Print("[Qlute] Finished parsing... NEXT! >:3");
                        Replays.Add(Replayman);
                        break;
                    }
                }
            }
            else
            {
                GD.PrintErr($"[Qlute] File '{file}' not found, are you sure you didn't miss placed it?? :<");
            }
        }
        catch (Exception err)
        {
            GD.PrintErr("[Qlute] ", err);
        }
    }
    /// <summary>Drops characters that can't be in a file name (beatmap titles can have / : ? etc.).</summary>
    public static string CleanName(object s) => string.Concat((s?.ToString() ?? "").Split(Path.GetInvalidFileNameChars()));

    /// <summary>
    /// Resets the replay cache and set the file path for the replay file.
    /// </summary>
    /// <summary>Seed of the replay being watched ("#Seed"), so Random puts the notes where they were. Null = not watching one.</summary>
    public static int? WatchSeed { get; private set; }
    /// <summary>The watched replay is from before Random shuffled columns (no "#Random: columns" line).</summary>
    public static bool WatchLegacyRandom { get; private set; }
    /// <summary>Header line new replays carry: Random shuffles whole columns (Gameplay.ShuffledColumns).</summary>
    public const string RandomColumnsLine = "#Random: columns";

    /// <summary>Watching a replay that has a seed (its Random columns must match the recording).</summary>
    public static bool Watching(out int seed)
    {
        seed = WatchSeed ?? 0;
        return SettingsOperator.SpectatorMode && WatchSeed != null;
    }

    public static void ResetReplay(bool quiet = false)
    {
        ReplayCache.Clear();
        WatchSeed = null;
        WatchLegacyRandom = false;
        GD.Print("[Qlute] Resetting Replay cache...");
        FilePath = $"{SettingsOperator.replaydir}/[{CleanName(ApiOperator.Username)}] [{CleanName(SettingsOperator.SessionConfig.BeatmapMapper)}] {CleanName(SettingsOperator.SessionConfig.BeatmapArtist)} - {CleanName(SettingsOperator.SessionConfig.BeatmapTitle)} [{CleanName(SettingsOperator.SessionConfig.BeatmapDifficultyName)}]-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}.qrf";
        if (!quiet) GD.Print($"[Qlute] Set file path to {FilePath}");
        SettingsOperator.SpectatorMode = false;
    }
    /// <summary>
    /// Add a timestamp for the replay
    /// </summary>
    public static void AddReplay(int Time, int NoteTap)
    {
        ReplayCache.Add(new ReplayLegend
        {
            Time = Time,
            NoteTap = NoteTap
        });
    }
    /// <summary>
    /// Saves the replay file to it's desired path.
    /// </summary>
    public static void SaveReplay()
    {
        if (!SettingsOperator.SpectatorMode)
        {
            try
            {
                GD.Print("[Qlute] Saving Replay...");
                var sb = new System.Text.StringBuilder(512 + ReplayCache.Count * 12);
                sb.Append($"#Qlute Version: {ProjectSettings.GetSetting("application/config/version")}-{ProjectSettings.GetSetting("application/config/branch")}\n");
                sb.Append($"#Username: {ApiOperator.Username}\n");
                sb.Append($"#BeatmapID: {SettingsOperator.SessionConfig.BeatmapID}\n");
                sb.Append($"#BeatmapSetID: {SettingsOperator.SessionConfig.BeatmapSetID}\n");
                sb.Append($"#Score: {Gameplay.score}\n");
                sb.Append($"#Max: {SettingsOperator.Gameplaycfg.Max}\n");
                sb.Append($"#Great: {SettingsOperator.Gameplaycfg.Great}\n");
                sb.Append($"#Meh: {SettingsOperator.Gameplaycfg.Meh}\n");
                sb.Append($"#Bad: {SettingsOperator.Gameplaycfg.Bad}\n");
                sb.Append($"#Accuracy: {Num.Str(SettingsOperator.Gameplaycfg.Accuracy)}\n");
                sb.Append($"#Max Combo: {SettingsOperator.Gameplaycfg.MaxCombo}\n");
                sb.Append($"#Average ms: {Num.Str(SettingsOperator.Gameplaycfg.ms)}\n");
                sb.Append($"#Mods: {ModsOperator.GetModAlias()}\n");
                sb.Append($"#Points: {Num.Str(SettingsOperator.Gameplaycfg.pp)}\n");
                sb.Append($"#Seed: {Gameplay.seed}\n");
                if (ModsOperator.Mods["random"]) sb.Append(RandomColumnsLine).Append('\n');
                sb.Append($"#Multiplier: {Num.Str(AudioPlayer.Instance.PitchScale)}\n");
                foreach (ReplayLegend entry in ReplayCache)
                {
                    sb.Append(entry.Time).Append(',').Append(entry.NoteTap).Append('\n');
                }
                FileCache = sb.ToString();

                using var file = Godot.FileAccess.Open(FilePath, Godot.FileAccess.ModeFlags.Write);
                file.StoreString(FileCache);
                file.Close();
                Parse(FilePath);
                if (SettingsOperator.LeaderboardType == 0)
                    ApiOperator.ReloadLeaderboard(SettingsOperator.SessionConfig.BeatmapID);
                GD.Print("[Qlute] Completed Successfully!");
            }
            catch (Exception err)
            {
                GD.PrintErr(err);
                GD.Print("[Qlute] Replay failed to save.");
            }
        }
    }
    /// <summary>
    /// Load the replay file from it's desired path.
    /// </summary>
    public static void ReloadReplay(string File)
    {
        ResetReplay(quiet: true);
        GD.Print("[Qlute] Loading Replay...");
        using var file = Godot.FileAccess.Open(File, Godot.FileAccess.ModeFlags.Read);
        string[] cache = file.GetAsText().TrimEnd('\n').Split("\n");
        bool columns = false;
        foreach (string entry in cache)
        {
            try
            {
                if (entry.StartsWith("#Seed: ") && int.TryParse(entry["#Seed: ".Length..].Trim(), out var seed)) WatchSeed = seed;
                else if (entry.TrimEnd('\r') == RandomColumnsLine) columns = true;
                else if (!entry.StartsWith('#'))
                {
                    var note = entry.Split(',');
                    int time = int.Parse(note[0].Trim());
                    int noteTap = int.Parse(note[1].Trim());
                    AddReplay(time, noteTap);
                }
            }
            catch (Exception x)
            {
                GD.Print(x);
            }
        }
        WatchLegacyRandom = !columns;
        GD.Print("[Qlute] Completed Successfully!");
        SettingsOperator.SpectatorMode = true;
	}
}