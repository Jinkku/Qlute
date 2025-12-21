using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;


public class DanceCounter {
        public int time {get;set;}
        public bool flash {get;set;}
}

public partial class SettingsOperator : Node
{
    public string[] args { get; set; }
    public static string UpdatedRank = "#0";
    public static string Updatedpp = "0pp";
    public static string homedir = OS.GetUserDataDir().Replace("\\", "/");
    public static string tempdir => homedir + "/temp";
    public static string beatmapsdir => homedir + "/beatmaps";
    public static string exportdir => homedir + "/exports";
    public static readonly float ppbase = 0.035f;
    public static readonly float ppv2base = 0.045f;
    public static string downloadsdir => homedir + "/downloads";
    public static string replaydir => homedir + "/replays";
    public static string screenshotdir => homedir + "/screenshots";
    public static string skinsdir => homedir + "/skins";
    public static string settingsfile => homedir + "/settings.cfg";
    public static string Qlutedb => homedir + "/qlute.db";
    public static float levelweight = 0.0084f;
    public static bool loopaudio = false;
    public static bool jukebox = false;
    public static string GameChecksum { get; set; }
    public int backgrounddim { get; set; }
    public int MasterVol { get; set; }
    public int SampleVol { get; set; }
    public int scrollspeed { get; set; }
    public static bool SpectatorMode { get; set; } = false;
    public static float AllMiliSecondsFromBeatmap { get; set; }
    public static float MiliSecondsFromBeatmap { get; set; }
    public static int MiliSecondsFromBeatmapTimes { get; set; }
    public static List<BeatmapLegend> Beatmaps = new List<BeatmapLegend>();
    public static float AudioOffset { get; set; } = 0;
    public static int LeaderboardType = 1;
    public static bool NoConnectionToGameServer { get; set; }
    public static void ResetRank()
    {
        UpdatedRank = "#0";
        Updatedpp = "0pp";
    }

    public void RefreshFPS()
    {
        var fps = int.TryParse(GetSetting("fpsmode").ToString(), out int fpsm) ? (int)fpsm : 1;
        if (fps == 0)
        {
            DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Enabled);
        }
        else if (fps == 1)
        {
            DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);
            Engine.MaxFps = (int)DisplayServer.ScreenGetRefreshRate() * 2;
        }
        else if (fps == 2)
        {
            DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);
            Engine.MaxFps = (int)DisplayServer.ScreenGetRefreshRate() * 4;
        }
        else if (fps == 3)
        {
            DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);
            Engine.MaxFps = (int)DisplayServer.ScreenGetRefreshRate() * 8;
        }
        else if (fps == 4)
        {
            DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);
            Engine.MaxFps = 0; // Unlimited FPS
        }
    }
    public static Dictionary<string, object> Configuration { get; set; } = new Dictionary<string, object>
    {
        { "scaled", false },
        { "windowmode", 0 },
        { "master", 80 },
        { "sample", 80 },
        { "backgrounddim", 70 },
        { "audiooffset", 0 },
        { "skin", null },
        { "username", null },
        { "password", null },
        { "stayloggedin", true },
        { "api", "https://qlute.jinkku.moe/" },
        { "client-id", null },
        { "client-secret", null },
        { "scrollspeed", (int)1346 }, // 11485 ms max
        { "fpsmode", 1 },
        { "showfps", false },
        { "showunicode", false },
        { "hidedevintro", false },
        { "gamepath", "" },
        { "leaderboardtype", 1 },
    };
    public Dictionary<string, object> Configurationbk { get; set; }

    public static Texture2D GetNullImage() => ResourceLoader.Load<CompressedTexture2D>("res://Resources/System/SongSelect/NoBG.png");

    public static Texture2D LoadImage(string path)
    {
        if (!FileAccess.FileExists(path))
        {
            Notify.Post("Image could not be loaded, because it doesn't exist!");
            return GetNullImage();
        }

        try
        {
            using var image = Image.LoadFromFile(path);
            if (image == null)
            {
                return GetNullImage();
            }
            else
            {
                return ImageTexture.CreateFromImage(image);
            }
        }
        catch (Exception)
        {
            Notify.Post("Failed to load image!");
            return GetNullImage();
        }
    }
    public static int RndSongID() {
        int id = new Random().Next(0, Beatmaps.Count);
        return id;
    }
    public static bool CreateSelectingBeatmap { get; set; }
    public static bool MultiSelectingBeatmap { get; set; }
    public static bool Start_reloadLeaderboard { get; set; } = false;
    public static List<int> MarathonMapPaths { get; set; } = new List<int>();
    public static bool Marathon { get; set; } = false; // Marathon mode flag
    public static int MarathonID { get; set; } = -1; // ID of the current marathon song
    public static int PerfectJudge { get; set; } = 105; // Judge Perfect
    public static readonly int PerfectJudgeMin = PerfectJudge;
    public static int GreatJudge { get; set; } = -1; // Judge Great
    public static int MehJudge { get; set; } = -1; // Judge Meh

    public static void ReloadInfo()
    {
        if (Beatmaps.Count > 0 && SongID != -1)
        {
            var beatmap = Beatmaps[SongID];
            if (Check.CheckBoolValue(SettingsOperator.GetSetting("showunicode").ToString()) && beatmap.TitleUnicode != null && beatmap.ArtistUnicode != null)
            {
                Sessioncfg["beatmaptitle"] = beatmap.TitleUnicode;
                Sessioncfg["beatmapartist"] = beatmap.ArtistUnicode;
            } else
            {
                Sessioncfg["beatmaptitle"] = beatmap.Title;
                Sessioncfg["beatmapartist"] = beatmap.Artist;
            }
        }
    }
    public void SelectSongID(int id, float seek = -1)
    {
        if (Beatmaps.ElementAt(id) != null)
        {
            ApiOperator.LeaderboardStatus = 0; // Reset leaderboard status
            ApiOperator.LeaderboardList.Clear(); // Clear the leaderboard list
            if (!Marathon) MarathonMapPaths.Clear(); // Clear marathon map paths if not in marathon mode
            Start_reloadLeaderboard = true;
            var beatmap = Beatmaps[id];
            SongID = id;
            Sessioncfg["beatmapurl"] = beatmap.Rawurl;
            if (beatmap.TitleUnicode != null && beatmap.ArtistUnicode != null)
            {
                Sessioncfg["beatmaptitle"] = beatmap.TitleUnicode;
                Sessioncfg["beatmapartist"] = beatmap.ArtistUnicode;
            }else if (!Check.CheckBoolValue(SettingsOperator.GetSetting("showunicode").ToString()) || ( beatmap.TitleUnicode == null && beatmap.ArtistUnicode == null ) )
            {
                Sessioncfg["beatmaptitle"] = beatmap.Title;
                Sessioncfg["beatmapartist"] = beatmap.Artist;
            }
            Sessioncfg["beatmapdiff"] = beatmap.Version;
            Sessioncfg["beatmapbpm"] = (int)beatmap.Bpm;
            Gameplaycfg.TimeTotalGame = beatmap.Timetotal * 0.001f;
            Sessioncfg["beatmapmapper"] = beatmap.Mapper;
            Gameplaycfg.Accuracy = (int)beatmap.Accuracy;
            //Gameplaycfg.SampleSet = beatmap.SampleSet;
            Gameplaycfg.SampleSet = SampleSet.Type[1];
            LevelRating = beatmap.Levelrating;
            Sessioncfg["osubeatid"] = (int)beatmap.Osubeatid;
            Sessioncfg["osubeatidset"] = (int)beatmap.Osubeatidset;
            Sessioncfg["background"] = LoadImage(beatmap.Path.ToString() + beatmap.Background.ToString());
            if (beatmap.Path != null && beatmap.Background != null)
                Sessioncfg["background"] = LoadImage(beatmap.Path.ToString() + beatmap.Background.ToString());
            else
                Sessioncfg["background"] = null;
            if (seek == -1)
            {
                seek = beatmap.PreviewTime;
            }
            Gameplaycfg.maxpp = beatmap.pp;

            string audioPath = beatmap.Path + "" + beatmap.Audio;
            string chk = ChecksumUtil.GetSha256(audioPath);
            if (System.IO.File.Exists(audioPath))
            {
                AudioStream filestream = null;
                if (audioPath.EndsWith(".mp3"))
                {
                    filestream = AudioPlayer.LoadMP3(audioPath);
                }
                else if (audioPath.EndsWith(".wav"))
                {
                    filestream = AudioPlayer.LoadWAV(audioPath);
                }
                else if (audioPath.EndsWith(".ogg"))
                {
                    filestream = AudioPlayer.LoadOGG(audioPath);
                }
                if (AudioPlayer.checksum != chk)
                {
                    AudioPlayer.checksum = chk;
                    AudioPlayer.Instance.Stream = filestream;
                    AudioPlayer.Instance.Play(seek);
                }
            }
            else
            {
                GD.PrintErr("Audio file not found: " + audioPath);
            }
            
            ApiOperator.CheckBeatmapRankStatus();
            
        }
        else { GD.PrintErr("Can't select a song that don't exist :/"); }
    }

    public static float TimeCap = 120;


    public static float GetLevelRating(int Objects, float TimeTotal) => (Objects * levelweight) / (TimeTotal / TimeCap);
    public static double Get_ppvalue(int max, int great, int meh, int bad, float multiplier = 1, int combo = 0, double TimeTotal = 0)
    {
        //bad = Math.Max(1,bad);
        var ppvalue = 0.0;
        ppvalue = max * ppbase;
        ppvalue -= ppbase / 2 * great;
        ppvalue -= ppbase / 3 * meh;
        ppvalue -= ppbase * bad;
        ppvalue += combo * ppbase;
        ppvalue *= multiplier;
        //ppvalue /= 1 + (int)(TimeTotal / TimeCap); FutureRelease
        return Math.Max(0, ppvalue);
    }
    public static string Parse_Beatmapfile(string filename, int SetID = 0)
    {
        var legend = new BeatmapLegend {ID = Beatmaps.Count , SetID = SetID };
        using var file = FileAccess.Open(filename, FileAccess.ModeFlags.Read);
        var lines = file.GetAsText().Split("\n");
        bool inHitObjects = false;
        bool inTimingPoints = false;
        int hitCount = 0;
        float lastNoteTime = 0;
        int ppv2multi = 1;
        int ppv2time = -1;
        foreach (var lineRaw in lines)
        {
            var line = lineRaw.Trim();
            if (string.IsNullOrWhiteSpace(line)) continue;

            // Handle section switches
            if (line == "[TimingPoints]") { inTimingPoints = true; continue; }
            if (line == "[HitObjects]") { inTimingPoints = false; inHitObjects = true; continue; }
            if (line.StartsWith("[")) { inTimingPoints = false; inHitObjects = false; continue; }

            // --- key: value pairs ---
            if (!inTimingPoints && !inHitObjects && line.Contains(":"))
            {
                var parts = line.Split(":", 2);
                var key = parts[0].Trim();
                var value = parts[1].Trim();

                switch (key)
                {
                    case "Title": legend.Title = value; break;
                    case "TitleUnicode": legend.TitleUnicode = value; break;
                    case "Artist": legend.Artist = value; break;
                    case "ArtistUnicode": legend.ArtistUnicode = value; break;
                    case "SampleSet": legend.SampleSet = value; break;
                    case "Creator": legend.Mapper = value; break;
                    case "Version": legend.Version = value; break;
                    case "CircleSize": legend.KeyCount = (int)(float.TryParse(value, out var cs) ? cs : 4); break;
                    case "OverallDifficulty": legend.Accuracy = float.TryParse(value, out var od) ? od : 0; break;
                    case "AudioFilename": legend.Audio = value; break;
                    case "BeatmapID": legend.Osubeatid = int.TryParse(value, out var bid) ? bid : -1; break;
                    case "BeatmapSetID": legend.Osubeatidset = int.TryParse(value, out var bset) ? bset : -1; break;
                    case "QluteBeatID": legend.Beatid = int.TryParse(value, out var qbid) ? qbid : -1; break;
                    case "QluteBeatIDSet": legend.Beatidset = int.TryParse(value, out var qbset) ? qbset : -1; break;
                    case "PreviewTime": legend.PreviewTime = (float.TryParse(value, out var pt) ? pt : 0) * 0.001f; break;
                }
            }

            // --- background ---
            if (line.StartsWith("0,0,\"") && line.Contains("\""))
            {
                var parts = line.Split("\"");
                if (parts.Length > 1) legend.Background = parts[1].Trim();
            }

            // --- timing points ---
            if (inTimingPoints)
            {
                var parts = line.Split(",");
                if (parts.Length < 2) continue;

                if (float.TryParse(parts[1], out var mpb) && legend.Bpm == 0) // first BPM
                    legend.Bpm = 60000f / mpb;

                if (int.TryParse(parts[0], out var time) && int.TryParse(parts.Last(), out var flash))
                    legend.Dance.Add(new DanceCounter { time = time, flash = flash == 1 });
            }

            // --- hitobjects ---
            if (inHitObjects)
            {
                var parts = line.Split(new[] { ',', ':' });
                if (parts.Length < 3) continue;

                if (int.TryParse(parts[2], out var noteTime))
                {
                    if (noteTime < ppv2time + (60000 / legend.Bpm))
                    {
                        ppv2multi++;
                    }
                    else
                    {
                        ppv2time = noteTime;
                        ppv2multi = 1;
                    }
                    var ppv2value = SettingsOperator.ppv2base * ppv2multi;
                    legend.ppv2sets.Add(ppv2value);
                    legend.pp += ppv2value;
                    lastNoteTime = noteTime;
                    hitCount++;

                }
            }
        }
        if (!SampleSet.Type.Contains(legend.SampleSet))
        {
            legend.SampleSet = SampleSet.Type.First();
        }

        legend.Timetotal = lastNoteTime;
        legend.Levelrating = GetLevelRating(hitCount, lastNoteTime * 0.001f);
        legend.Path = filename.Replace(filename.Split("/").Last(), "");
        legend.Rawurl = filename;
        
        Beatmaps.Add(legend);
        
        return $"{legend.Artist} - {legend.Title} from {legend.Mapper}";
    }

    public static void Addms(float ms)
    {
        AllMiliSecondsFromBeatmap += ms;
        MiliSecondsFromBeatmapTimes++;
        UnstableRate.Rate.Add(ms);
    }
    public static float Getms() {
        return AllMiliSecondsFromBeatmap / MiliSecondsFromBeatmapTimes;
    }
    public static void Resetms() {
        AllMiliSecondsFromBeatmap = 0;
        MiliSecondsFromBeatmapTimes = 0;
    }

    public static List<Color> LevelColours = new List<Color>([new Color("43F9FF"), new Color("329928"), new Color("FFC300"), new Color("995028"), new Color("E32929"), new Color("FF5151"), new Color("000000")]);

    public static Color ReturnLevelColour(int level)
    {
        if (level > 100)
        {
            return LevelColours.Last();
        }
        else if (level > 70)
        {
            return LevelColours[5];
        }
        else if (level >50)
        {
            return LevelColours[4];
        }
        else if (level > 36)
        {
            return LevelColours[3];
        }
        else if (level > 20)
        {
            return LevelColours[2];
        }
        else if (level > 10)
        {
            return LevelColours[1];
        }
        else
        {
            return LevelColours.First();
        }
    }
    public static void ResetScore()
    {
        Gameplaycfg.Score = 0;
        Gameplaycfg.pp = 0;
        Gameplaycfg.Max = 0;
        Gameplaycfg.Great = 0;
        Gameplaycfg.Meh = 0;
        Gameplaycfg.Bad = 0;
        Gameplaycfg.Accuracy = 100;
        Gameplaycfg.Time = 0;
        Gameplaycfg.Combo = 0;
        Gameplaycfg.MaxCombo = 0;
        Gameplaycfg.Avgms = 0;
        Gameplaycfg.ms = 0;
    }
    public static class Gameplaycfg
    {
        public static int Score { get; set; }
        public static double pp { get; set; }
        public static double maxpp { get; set; }
        public static double Time { get; set; }
        public static double TimeTotal { get; set; }
        public static double TimeTotalGame { get; set; }
        public static float Accuracy { get; set; }
        public static string SampleSet { get; set; }
        public static int Combo { get; set; }
        public static int MaxCombo { get; set; }
        public static int Max { get; set; }
        public static int Great { get; set; }
        public static int Meh { get; set; }
        public static int Bad { get; set; }
        public static float ms { get; set; }
        public static int Avgms { get; set; }
        public static float BeatmapAccuracy { get; set; } = 1;
        public static int Rank { get; set; }
    }
    public static int ranked_points { get; set; }

    public static int SongID { get; set; } = -1;
    public static int SongIDHighlighted { get; set; } = -1; // Highlighted song ID for the song select screen
    public static double LevelRating { get; set; } = -1;
    public static Dictionary<string, object> Sessioncfg { get; set; } = new Dictionary<string, object>
    {
        { "TopPanelSlideip", false },
        { "toppanelhide", false },
        { "Reloadmap", false },
        { "reloaddb", false },
        { "localpp", (double)0 },
        { "loggedin", false },
        { "loggingin", false },
        { "showaccountpro", false },
        { "settingspanelv", false },
        { "chatboxv", false },
        { "notificationpanelv", false },
        { "ranknumber", 0 },
        { "playercolour", null },
        { "totalbeatmaps", 0 },
        { "beatmapurl", null },
        { "beatmaptitle", null },
        { "beatmapartist", "" },
        { "beatmapmapper", "" },
        { "beatmapbpm", (int)160 },
        { "osubeatid", 0 },
        { "osubeatidset", 0 },
        { "beatmapaccuracy", (int)1 },
        { "beatmapdiff", "" },
        { "customapi", false},
        { "multiplier" , 1.0f},
        { "fps" , 0},
        { "ms" , 0.0f},
        { "background" , null},
        { "client-id", null },
        { "client-secret", null },
    };
    

    private Tween TopPanelAnimation { get; set; }
    public void toppaneltoggle(bool value, bool noani = false)
    {
        ColorRect TopPanel = GetTree().Root.GetNode<ColorRect>("TopPanelOnTop/InfoBar");
        TopPanelAnimation?.Kill();
        TopPanelAnimation = CreateTween();
        Sessioncfg["toppanelhide"] = !value;
        if (value && noani)
            TopPanel.Position = new Vector2(TopPanel.Position.X, 0);
        else if (!value && noani)
            TopPanel.Position = new Vector2(TopPanel.Position.X, -TopPanel.Size.Y);
        else if (!value && !noani)
            TopPanelAnimation.TweenProperty(TopPanel, "position", new Vector2(TopPanel.Position.X, -TopPanel.Size.Y), 0.3).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
        else if (!noani)
            TopPanelAnimation.TweenProperty(TopPanel, "position", new Vector2(TopPanel.Position.X, 0), 0.3).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
        TopPanelAnimation.Play();
    }
    public static float TopPanelPosition { get; set; } = 0.0f;
    private double oldtime = 0.0f;

    public override void _Process(double _delta)
    {
        var aud = GetSetting("audiooffset").ToString();
        if (aud == null) AudioOffset = 0; // AudioOffset Subsystem
        else AudioOffset = float.Parse(aud);


        TopPanelPosition = GetTree().Root.GetNode<ColorRect>("TopPanelOnTop/InfoBar").Position.Y + 50;
        if (Input.IsActionJustPressed("Hide Panel"))
        {
            toppaneltoggle((bool)Sessioncfg["toppanelhide"]);
        }
    }

    private void CheckOldSiteUrl()
    {
        GD.Print("Checking if your using the old api...");
        if (GetSetting("api").ToString().Contains("qlute.pxki.us.to"))
        {
            Notify.Post("We changed the game servers so that you will not have interuptions <3");
            SetSetting("api", Configurationbk["api"]);
        }
    }


    public override void _Ready()
    {
        args = OS.GetCmdlineArgs();
        Configurationbk = new Dictionary<string, object>(Configuration);
        GD.Print("Please wait...");
        GD.Print("Checking if config file is saved...");
        if (System.IO.File.Exists(settingsfile))
        {
            GD.Print("Config file found... Loading it up :)");
            using var saveFile = FileAccess.Open(settingsfile, FileAccess.ModeFlags.Read);
            var json = saveFile.GetAsText();
            Configuration = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
            foreach (string s in Configurationbk.Keys)
            {
                if (!Configuration.ContainsKey(s))
                {
                    Configuration[s] = Configurationbk[s];
                }
            }
            saveFile.Close();
        }
        else
        {
            GD.Print("Creating config...");
            SaveSettings();
        }


        // Check if temp folder is not empty, then delete its contents
        if (System.IO.Directory.Exists(tempdir))
        {
            var files = System.IO.Directory.GetFiles(tempdir);
            var dirs = System.IO.Directory.GetDirectories(tempdir);
            if (files.Length > 0 || dirs.Length > 0)
            {
                foreach (var file in files)
                {
                    try { System.IO.File.Delete(file); } catch { }
                }
                foreach (var dir in dirs)
                {
                    try { System.IO.Directory.Delete(dir, true); } catch { }
                }
            }
        }


        backgrounddim = int.TryParse(GetSetting("backgrounddim").ToString(), out int bkd) ? bkd : 70;
        SampleVol = int.TryParse(GetSetting("sample").ToString(), out int smp) ? smp : 80;
        MasterVol = int.TryParse(GetSetting("master").ToString(), out int mtr) ? mtr : 80;
        ResetVol();
        var resolutionIndex = int.TryParse(GetSetting("windowmode")?.ToString(), out int mode) ? mode : 0;
        changeres(resolutionIndex);
        RefreshFPS();
        Replay.Init();
        LeaderboardType = int.TryParse(GetSetting("leaderboardtype").ToString(), out int lbtm) ? (int)lbtm : 1;
        string exePath = OS.GetExecutablePath();
        string folderPath = System.IO.Path.GetDirectoryName(exePath);
        if (!args.Contains("--update"))
        {
            SetSetting("gamepath", folderPath);
        }
        if (LeaderboardType < 0 && LeaderboardType > 2) LeaderboardType = 1;
        CheckOldSiteUrl();
        GameChecksum = ChecksumUtil.GetGameChecksum(); 
    }
    public void ResetVol()
    {
        if (AudioPlayer.Instance != null) AudioPlayer.Instance.VolumeDb = (int)(Math.Log10(MasterVol / 100.0) * 20) - 5; // -5 to adjust the volume to a more NOT loud level and cap it
        if (Sample.Instance != null) Sample.Instance.VolumeDb = (int)(Math.Log10(SampleVol / 100.0) * 20) - 5;
    }
    public void changeres(int index)
    {
        GD.Print(string.Format("Resolution changed to {0}", index));
        if (index == 0)
        {
            DisplayServer.WindowSetMode(DisplayServer.WindowMode.ExclusiveFullscreen);
        }
        else if (index == 1)
        {
            DisplayServer.WindowSetMode(DisplayServer.WindowMode.Fullscreen);
        }
        else if (index == 2)
        {
            DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
        }
        SetSetting("windowmode", index);
    }
    // Set a setting
    public static void SetSetting(string key, object value)
    {
        if (Configuration.ContainsKey(key))
            Configuration[key] = value;
            SaveSettings();
    }

    // Save settings to file
    public static void SaveSettings()
    {
        using var saveFile = FileAccess.Open(settingsfile, FileAccess.ModeFlags.Write);
        var json = JsonSerializer.Serialize(Configuration);
        saveFile.StoreString(json);
        saveFile.Close();
    }

    // Get a setting
    public static object GetSetting(string key)
    {
        return Configuration.ContainsKey(key) ? Configuration[key] : null;
    }
}
