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
    public int osuBeatmapID { get; set; } = 0;
    public int osuBeatmapSetID { get; set; } = 0;
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
    public static List<ReplayLegend> ReplayCache = new List<ReplayLegend>();
    public static List<ReplayInfo> Replays { get; set; } = new List<ReplayInfo>();

    /// <summary>
    /// initialize Replay subsystem. 
    /// </summary>
    public static void Init()
    {
        GD.Print("[Qlute] Init replay subsystem...");
        foreach (string replay in Directory.GetFiles(SettingsOperator.replaydir))
        {
            GD.Print(replay);
            Parse(replay);
        }

    }
    /// <summary>
    /// Finds the replay with the specified ID example: Replay.Search(192834)
    /// </summary>
    public static List<LeaderboardEntry> Search(int ID)
    {
        List<LeaderboardEntry> List = new List<LeaderboardEntry>();
        var selectedBeatmaps = Replays.Where(b => b.osuBeatmapID == ID).ToList();
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
            if (Godot.FileAccess.FileExists(file))
            {
                GD.Print($"[Qlute] Found '{file}' Processing it....");
                using var filedata = Godot.FileAccess.Open(file, Godot.FileAccess.ModeFlags.Read);
                string[] cache = filedata.GetAsText().TrimEnd('\n').Split("\n");
                var Username = "";
                var osuBeatmapID = 0;
                var osuBeatmapSetID = 0;
                var BeatmapID = 0;
                var BeatmapSetID = 0;
                var Score = 0;
                var Max = 0;
                var Great = 0;
                var Meh = 0;
                var Bad = 0;
                var Accuracy = 100.00f;
                var MaxCombo = 0;
                var AverageMs = 0.0f;
                var Mods = "";
                foreach (string data in cache)
                {
                    if (data.StartsWith("#"))
                    {
                        if (data.StartsWith("#Username: "))
                        {
                            Username = data.Replace("#Username: ", "");
                        }
                        else if (data.StartsWith("#osuBeatmapID: "))
                        {
                            osuBeatmapID = Int32.Parse(data.Replace("#osuBeatmapID: ", ""));
                        }
                        else if (data.StartsWith("#osuBeatmapSetID: "))
                        {
                            osuBeatmapSetID = Int32.Parse(data.Replace("#osuBeatmapSetID: ", ""));
                        }
                        else if (data.StartsWith("#QluteBeatmapID: "))
                        {
                            BeatmapID = Int32.Parse(data.Replace("#QluteBeatmapID: ", ""));
                        }
                        else if (data.StartsWith("#QluteBeatmapSetID: "))
                        {
                            BeatmapSetID = Int32.Parse(data.Replace("#QluteBeatmapSetID: ", ""));
                        }
                        else if (data.StartsWith("#Score: "))
                        {
                            Score = Int32.Parse(data.Replace("#Score: ", ""));
                        }
                        else if (data.StartsWith("#Max: "))
                        {
                            Max = Int32.Parse(data.Replace("#Max: ", ""));
                        }
                        else if (data.StartsWith("#Great: "))
                        {
                            Great = Int32.Parse(data.Replace("#Great: ", ""));
                        }
                        else if (data.StartsWith("#Meh: "))
                        {
                            Meh = Int32.Parse(data.Replace("#Meh: ", ""));
                        }
                        else if (data.StartsWith("#Bad: "))
                        {
                            Bad = Int32.Parse(data.Replace("#Bad: ", ""));
                        }
                        else if (data.StartsWith("#Accuracy: "))
                        {
                            Accuracy = float.Parse(data.Replace("#Accuracy: ", ""));
                        }
                        else if (data.StartsWith("#Max Combo: "))
                        {
                            MaxCombo = Int32.Parse(data.Replace("#Max Combo: ", ""));
                        }
                        else if (data.StartsWith("#Average ms: "))
                        {
                            AverageMs = float.Parse(data.Replace("#Average ms: ", ""));
                        }
                        else if (data.StartsWith("#Mods: "))
                        {
                            Mods = data.Replace("#Mods: ", "");
                        }
                    }
                    else
                    {
                        GD.Print("[Qlute] Finished parsing... NEXT! >:3");
                        Replays.Add(new ReplayInfo
                        {
                            Username = Username,
                            osuBeatmapID = osuBeatmapID,
                            osuBeatmapSetID = osuBeatmapSetID,
                            BeatmapID = BeatmapID,
                            BeatmapSetID = BeatmapSetID,
                            Score = Score,
                            Max = Max,
                            Great = Great,
                            Meh = Meh,
                            Bad = Bad,
                            Accuracy = Accuracy,
                            MaxCombo = MaxCombo,
                            Avgms = AverageMs,
                            Mods = Mods,
                            Filepath = file

                        });
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
    /// <summary>
    /// Resets the replay cache and set the file path for the replay file.
    /// </summary>
    public static void ResetReplay(bool quiet = false)
    {
        ReplayCache.Clear();
        GD.Print("[Qlute] Resetting Replay cache...");
        FilePath = $"{SettingsOperator.replaydir}/[{ApiOperator.Username}] [{SettingsOperator.Sessioncfg["beatmapmapper"].ToString()}] {SettingsOperator.Sessioncfg["beatmapartist"].ToString()} - {SettingsOperator.Sessioncfg["beatmaptitle"].ToString()} [{SettingsOperator.Sessioncfg["beatmapdiff"].ToString()}]-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}.qrf";
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
    public static void SaveReplay(int ID)
    {
        if (!SettingsOperator.SpectatorMode)
        {
            GD.Print("[Qlute] Saving Replay...");
            var cache = $"#Qlute Version: {ProjectSettings.GetSetting("application/config/version")}-{ProjectSettings.GetSetting("application/config/branch")}\n";
            cache += $"#Username: {ApiOperator.Username}\n";
            cache += $"#osuBeatmapID: {SettingsOperator.Sessioncfg["osubeatid"]}\n";
            cache += $"#osuBeatmapSetID: {SettingsOperator.Sessioncfg["osubeatidset"]}\n";
            cache += $"#QluteBeatmapID: 0\n";
            cache += $"#QluteBeatmapSetID: 0\n";
            cache += $"#Score: {SettingsOperator.GameplayInfo[ID].Score}\n";
            cache += $"#Max: {SettingsOperator.GameplayInfo[ID].Max}\n";
            cache += $"#Great: {SettingsOperator.GameplayInfo[ID].Great}\n";
            cache += $"#Meh: {SettingsOperator.GameplayInfo[ID].Meh}\n";
            cache += $"#Bad: {SettingsOperator.GameplayInfo[ID].Bad}\n";
            cache += $"#Accuracy: {SettingsOperator.GameplayInfo[ID].Accuracy * 100.00}\n";
            cache += $"#Max Combo: {SettingsOperator.GameplayInfo[ID].MaxCombo}\n";
            cache += $"#Average ms: {SettingsOperator.GameplayInfo[ID].ms}\n";
            cache += $"#Mods: {ModsOperator.GetModAlias()}\n";
            foreach (ReplayLegend entry in ReplayCache)
            {
                cache += $"{entry.Time},{entry.NoteTap}\n";
            }
            using var file = Godot.FileAccess.Open(FilePath, Godot.FileAccess.ModeFlags.Write);
            file.StoreString(cache);
            cache = "";
            GD.Print("[Qlute] Completed Successfully!");
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
        foreach (string entry in cache)
        {
            try
            {
                if (!entry.StartsWith('#'))
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
        GD.Print("[Qlute] Completed Successfully!");
        SettingsOperator.SpectatorMode = true;
	}
}