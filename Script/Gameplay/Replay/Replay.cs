using System;
using System.Collections.Generic;
using Godot;
public class ReplayLegend
{
    public int Time;
    public int NoteTap;
}
public static class Replay
{
	public static string FilePath { get; set; }
    public static List<ReplayLegend> ReplayCache = new List<ReplayLegend>();

    /// <summary>
    /// Resets the replay cache and set the file path for the replay file.
    /// </summary>
    public static void ResetReplay()
    {
        ReplayCache.Clear();
        GD.Print("[Qlute] Resetting Replay cache...");
        //FilePath = $"{SettingsOperator.replaydir}/[{ApiOperator.Username}] [{SettingsOperator.Sessioncfg["beatmapmapper"].ToString()}] {SettingsOperator.Sessioncfg["beatmapartist"].ToString()} - {SettingsOperator.Sessioncfg["beatmaptitle"].ToString()} [{SettingsOperator.Sessioncfg["beatmapdiff"].ToString()}]-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}.qrf";
        FilePath = $"{SettingsOperator.replaydir}/Testreplay.qrf";
        GD.Print($"[Qlute] Set file path to {FilePath}");
        SettingsOperator.ReplayMode = false;
    }
    /// <summary>
    /// Add a timestamp for the replay
    /// </summary>
    public static void AddReplay(int Time, int NoteTap)
    {
        if (!SettingsOperator.ReplayMode)
        {
            ReplayCache.Add(new ReplayLegend
            {
                Time = Time,
                NoteTap = NoteTap
            });
        }
    }
/// <summary>
/// Saves the replay file to it's desired path.
/// </summary>
    public static void SaveReplay()
    {
        GD.Print("[Qlute] Saving Replay...");
        var cache = $"#Qlute Version: {ProjectSettings.GetSetting("application/config/version")}-{ProjectSettings.GetSetting("application/config/branch")}\n";
        cache += $"#Username: {ApiOperator.Username}\n";
        cache += $"#osuBeatmapID: {SettingsOperator.Sessioncfg["osubeatid"]}\n";
        cache += $"#osuBeatmapSetID: {SettingsOperator.Sessioncfg["osubeatidset"]}\n";
        foreach (ReplayLegend entry in ReplayCache)
        {
            cache += $"{entry.Time},{entry.NoteTap}\n";
        }
        using var file = FileAccess.Open(FilePath, FileAccess.ModeFlags.Write);
        file.StoreString(cache);
        cache = "";
        GD.Print("[Qlute] Completed Successfully!");
	}
    /// <summary>
    /// Load the replay file from it's desired path.
    /// </summary>
    public static void ReloadReplay(string File)
    {
        ResetReplay();
        GD.Print("[Qlute] Loading Replay...");
        using var file = FileAccess.Open(File, FileAccess.ModeFlags.Read);
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
                    GD.Print(time,"-", noteTap);
                }
            }
            catch (Exception x)
            {
                GD.Print(x);
            }
        }
        GD.Print("[Qlute] Completed Successfully!");
        SettingsOperator.ReplayMode = true;
	}
}