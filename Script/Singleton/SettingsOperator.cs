using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Realms;

public class DanceCounter {
        public int time {get;set;}
        public bool flash {get;set;}
}

public partial class SettingsOperator : Node
{
    public string[] args { get; set; }
    public static int Rank = 0;
    public static int RankScore = 0;
    public static int OldScore = 0;
    public static int ranked_points = 0;
    public static int OldRank = 0;
    public static int Oldpp = 0;
    public static int OldLevel { get; set; } = 0;
    public static int Level { get; set; } = 0;
    public static int OldCombo { get; set; } = 0;
    public static string ProfilePictureURL { get; set; } = null;
    public static string CardBorderURL { get; set; } = null;
    public static int OCombo { get; set; } = 0;
    public static float OldAccuracy { get; set; } = 0;
    public static float OAccuracy { get; set; } = 0;
    public static bool JustPlayedScore {get; set;}
    
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
    public int scrollspeed { get; set; }
    public static bool SpectatorMode { get; set; } = false;
    public static float AllMiliSecondsFromBeatmap { get; set; }
    public static float MiliSecondsFromBeatmap { get; set; }
    public static int MiliSecondsFromBeatmapTimes { get; set; }
    public static List<BeatmapLegend> Beatmaps = new List<BeatmapLegend>();
    public static float AudioOffset { get; set; } = 0;
    /// <summary>
    /// 1 = Online,
    /// 0 = Local
    /// 2 = Don't Reload
    /// </summary>
    public static int LeaderboardType = 1;
    public static bool NoConnectionToGameServer { get; set; }
    public static bool inGameplay { get; set; }
    private int UnfocusedFPS = 15;
    private bool Unfocused { get; set; }

    public void RefreshFPS()
    {
        if (Unfocused) return;
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
        { "hidehud", false },
        { "hidedevintro", false },
        { "discord-rpc", true },
        { "gamepath", "" },
        { "leaderboardtype", 1 },
        { "Key0", "D" },
        { "Key1", "F" },
        { "Key2", "J" },
        { "Key3", "K" },
    };
    public Dictionary<string, object> Configurationbk { get; set; }

    private float QuitSpeed = 0.5f;
    public void Quit()
    {
        toppaneltoggle(false);
        var tween = CreateTween();
        tween.SetParallel(true);
        tween.SetTrans(Tween.TransitionType.Cubic);
        tween.SetEase(Tween.EaseType.Out);
        var tmp = GetTree().CurrentScene.GetNode<Control>(".");
        tmp.PivotOffset = tmp.Size / 2;
        tween.TweenProperty(GetTree().CurrentScene, "modulate:a", 0f, QuitSpeed);
        tween.TweenProperty(GetTree().CurrentScene, "scale", tmp.Scale / 1.5f, QuitSpeed);
        tween.TweenProperty(GetTree().CurrentScene, "rotation", 0.25f, QuitSpeed);
        tween.TweenProperty(AudioPlayer.Instance, "volume_db", -40f, QuitSpeed);
        tween.TweenCallback(Callable.From(() => GetTree().Quit())).SetDelay(QuitSpeed + 0.5f);
    }
    public static Texture2D GetNullImage() => ResourceLoader.Load<CompressedTexture2D>("res://DefaultWallpaper/the_one.png");

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
    public static int PerfectJudge { get; set; } = 120; // Judge Perfect
    public static readonly int PerfectJudgeMin = PerfectJudge;
    public static int GreatJudge { get; set; } = -1; // Judge Great
    public static int MehJudge { get; set; } = -1; // Judge Meh

    public static void ReloadInfo()
    {
        if (Beatmaps.Count > 0 && SessionConfig.SongID != -1)
        {
            var beatmap = Beatmaps[SessionConfig.SongID];
            if (Check.CheckBoolValue(GetSetting("showunicode").ToString()) && beatmap.TitleUnicode != null && beatmap.ArtistUnicode != null)
            {
                SessionConfig.BeatmapTitle = beatmap.TitleUnicode;
                SessionConfig.BeatmapArtist = beatmap.ArtistUnicode;
            } else
            {
                SessionConfig.BeatmapTitle = beatmap.Title;
                SessionConfig.BeatmapArtist = beatmap.Artist;
            }
        }
    }
    [Signal]
    public delegate void BackgroundChangedEventHandler();
    public void SelectSongID(int id, float seek = -1)
    {
        if (Beatmaps.ElementAt(id) != null && SessionConfig.SongID != id)
        {
            ApiOperator.LeaderboardStatus = 0; // Reset leaderboard status
            ApiOperator.LeaderboardList.Clear(); // Clear the leaderboard list
            if (!Marathon) MarathonMapPaths.Clear(); // Clear marathon map paths if not in marathon mode
            Start_reloadLeaderboard = true;
            var beatmap = Beatmaps[id];
            SessionConfig.BeatmapURL = beatmap.Rawurl;
            if (beatmap.TitleUnicode != null && beatmap.ArtistUnicode != null)
            {
                SessionConfig.BeatmapTitle = beatmap.TitleUnicode;
                SessionConfig.BeatmapArtist = beatmap.ArtistUnicode;
            }else if (!Check.CheckBoolValue(SettingsOperator.GetSetting("showunicode").ToString()) || ( beatmap.TitleUnicode == null && beatmap.ArtistUnicode == null ) )
            {
                SessionConfig.BeatmapTitle = beatmap.Title;
                SessionConfig.BeatmapArtist = beatmap.Artist;
            }
            SessionConfig.BeatmapDifficultyName = beatmap.Version;
            SessionConfig.bpm = (int)beatmap.Bpm;
            Gameplaycfg.TimeTotalGame = beatmap.Timetotal * 0.001f;
            SessionConfig.BeatmapMapper = beatmap.Mapper;
            Gameplaycfg.BeatmapAccuracy = (int)beatmap.Accuracy;
            Gameplaycfg.NoteCount = (int)beatmap.NoteCount;
            //Gameplaycfg.SampleSet = beatmap.SampleSet;
            Gameplaycfg.SampleSet = SampleSet.Type[1];
            SessionConfig.LevelRating = beatmap.Levelrating;
            SessionConfig.BeatmapID =  beatmap.BeatmapID;
            SessionConfig.BeatmapSetID = beatmap.BeatmapSetID;
            SessionConfig.SongID = id;
            string bgpath = beatmap.Path.PathJoin(beatmap.Background.ToString());
            string checksumbg = null;
            if (System.IO.File.Exists(bgpath))
                checksumbg = ChecksumUtil.GetSha256(bgpath);
            if (SessionConfig.BackgroundChecksum != checksumbg && checksumbg != null)
                SessionConfig.Background = null;
            if (beatmap.Path != null && beatmap.Background != null && SessionConfig.BackgroundChecksum != checksumbg)
                SessionConfig.Background = LoadImage(bgpath);
            SessionConfig.BackgroundChecksum = checksumbg;
            EmitSignal(SignalName.BackgroundChanged);
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
        else if (SessionConfig.SongID != id) { GD.PrintErr("Can't select a song that don't exist :/"); }
        else if (SessionConfig.SongID == id)
        {
            GD.PrintErr("The song is already picked :^");
        }
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

    public static float MaxPPCap = 4000f; // don't make this static and PLEASE FIX THIS AFTERWARDS
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
                    case "BeatmapID": legend.BeatmapID = int.TryParse(value, out var bid) ? bid : -1; break;
                    case "BeatmapSetID": legend.BeatmapSetID = int.TryParse(value, out var bset) ? bset : -1; break;
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
        legend.Levelrating = Mathf.Pow(legend.pp / MaxPPCap, 0.7f) * 100f;
        legend.Path = filename.Replace(filename.Split("/").Last(), "");
        legend.Rawurl = filename;
        legend.NoteCount = hitCount;
        
        Beatmaps.Add(legend);
        
        return $"{legend.Artist} - {legend.Title} from {legend.Mapper}";
    }

    /// <summary>
    /// Parse Epoch time to string ex. "Dec 12th, 2025"
    /// </summary>
    /// <param name="time"></param>
    /// <returns></returns>
    public static string ParseTimeEpoch(long time)
    {
        double lastUpdatedSeconds = time;
        DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime date = epoch.AddSeconds(lastUpdatedSeconds);
        string formatted = $"{Extras.GetDayWithSuffix(date.Day)} {date:MMM yyyy}";
        return formatted;
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

    private static readonly (float pos, Color col)[] Stops = new[]
    {
        (0.00f, new Color("#5ec4ff")), // 0   Easy
        (0.10f, new Color("#4abf3f")), // 10  Normal
        (0.20f, new Color("#f7cf4a")), // 20  Hard
        (0.35f, new Color("#db7740")), // 35  Insane
        (0.50f, new Color("#E32929")), // 50  Expert
        (0.70f, new Color("#FF5151")), // 70  Expert+
        (1.00f, new Color("#000000"))  // 100 black
    };

    public static Color ReturnLevelColour(int level)
    {
        // Snap if you want hard cap
        if (level > 100)
            return new Color("#000000");

        float t = Mathf.Clamp(level / 100.0f, 0f, 1f);

        // Find which segment we're in
        for (int i = 0; i < Stops.Length - 1; i++)
        {
            if (t >= Stops[i].pos && t <= Stops[i + 1].pos)
            {
                float segmentT = (t - Stops[i].pos) / (Stops[i + 1].pos - Stops[i].pos);
                return Stops[i].col.Lerp(Stops[i + 1].col, segmentT);
            }
        }

        return Stops[^1].col; // fallback
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
        public static int NoteCount { get; set; }
        public static int Max { get; set; }
        public static int Great { get; set; }
        public static int Meh { get; set; }
        public static int Bad { get; set; }
        public static float ms { get; set; }
        public static int Avgms { get; set; }
        public static float BeatmapAccuracy { get; set; } = 1;
        public static int Rank { get; set; }
        public static double EpochTime { get; set; }
        public static string Username { get; set; } = "Guest";
    }

    public static class SessionConfig
    {
        /// <summary>
        /// Get the ID of the song in the beatmap list.
        /// </summary>
        public static int SongID { get; set; } = -1;
        /// <summary>
        /// The highlighted ID of the song in the beatmap list.
        /// </summary>
        public static int SongIDHighlighted { get; set; } = -1;
        /// <summary>
        /// The current Level Rating for the selected beatmap.
        /// </summary>
        public static double LevelRating { get; set; } = -1;
        /// <summary>
        /// Get the Selected Beatmap ID for the server to recognise.
        /// </summary>
        public static int BeatmapID { get; set; } = -1;
        /// <summary>
        /// The current Beatmap Set ID for the server to recognise.
        /// </summary>
        public static int BeatmapSetID { get; set; } = -1;
        /// <summary>
        /// The current BPM of the selected beatmap.
        /// </summary>
        public static int bpm { get; set; } = 180;
        /// <summary>
        /// The cached background of the beatmap.
        /// </summary>
        public static Texture2D Background { get; set; }
        /// <summary>
        /// Get the checksum of the background so that u don't need to load the beatmap background again.
        /// </summary>
        public static string BackgroundChecksum { get; set; }
        /// <summary>
        /// Toggling the visiblility of the top panel
        /// </summary>
        public static bool TopPanelSlideip { get; set; } = false;
        /// <summary>
        /// Toggling the visiblility of the top panel
        /// guessing this is the same too :/
        /// </summary>
        public static bool TopPanelHide { get; set; } = false;
        /// <summary>
        /// Reloading the current beatmap.
        /// </summary>
        public static bool Reloadmap { get; set; } = false;
        /// <summary>
        /// Reload the DB when there is a new beatmap waiting to import.
        /// </summary>
        public static bool ReloadDB { get; set; } = false;
        /// <summary>
        /// Local pp
        /// </summary>
        public static double Localpp { get; set; } = 0;
        /// <summary>
        /// Status of being logged in.
        /// </summary>
        public static bool Loggedin { get; set; } = false;
        /// <summary>
        /// Status of logging in.
        /// </summary>
        public static bool Loggingin { get; set; } = false;
        /// <summary>
        /// Profile visibility status.
        /// </summary>
        public static bool ShowAccountProfile { get; set; } = false;
        /// <summary>
        /// Settings visibility status.
        /// </summary>
        public static bool SettingsPanelVisible { get; set; } = false;
        /// <summary>
        /// Chat box visibility status.
        /// </summary>
        public static bool ChatBoxVisible { get; set; } = false;
        /// <summary>
        /// Notification visibility status.
        /// </summary>
        public static bool NotificationPanelVisible { get; set; } = false;
        /// <summary>
        /// Player colour hue.
        /// </summary>
        public static Color PlayerColour { get; set; }
        /// <summary>
        /// Total beatmaps inside the DB
        /// </summary>
        public static int TotalBeatmaps { get; set; } = 0;
        /// <summary>
        /// Beatmap URL for reference
        /// </summary>
        public static string BeatmapURL { get; set; }
        /// <summary>
        /// Curent beatmap title
        /// </summary>
        public static string BeatmapTitle { get; set; }
        /// <summary>
        /// Current beatmap artist
        /// </summary>
        public static string BeatmapArtist { get; set; }
        /// <summary>
        /// Current beatmap mapper
        /// </summary>
        public static string BeatmapMapper { get; set; }
        /// <summary>
        /// Current Difficulty name of the beatmap.
        /// </summary>
        public static string BeatmapDifficultyName { get; set; }
        /// <summary>
        /// Background dim percentile
        /// </summary>
        public static int Backgrounddim { get; set; }
        /// <summary>
        /// Ticks if it detects using a custom API.
        /// </summary>
        public static bool CustomAPI { get; set; }
        /// <summary>
        /// Multiplier for pp & level rating
        /// </summary>
        public static float Multiplier { get; set; } = 1.0f;
        /// <summary>
        /// Frames per second
        /// </summary>
        public static int FPS { get; set; } = 0;
        /// <summary>
        /// Miliseconds
        /// </summary>
        public static float ms { get; set; } = 0.0f;
        /// <summary>
        /// Client ID
        /// </summary>
        public static string ClientID { get; set; }
        /// <summary>
        /// Client Secret
        /// </summary>
        public static string ClientSecret { get; set; }
        /// <summary>
        /// Beatmap Accuracy for stricter inputs.
        /// </summary>
        public static int BeatmapAccuracy { get; set; } = 1;



    }

    private Tween TopPanelAnimation { get; set; }
    public void toppaneltoggle(bool value, bool noani = false)
    {
        ColorRect TopPanel = GetTree().Root.GetNode<ColorRect>("TopPanelOnTop/InfoBar");
        TopPanelAnimation?.Kill();
        TopPanelAnimation = CreateTween();
        SessionConfig.TopPanelHide = !value;
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
    public static Vector2 MouseMovement { get; set; }
    public override void _Process(double _delta)
    {
        MouseMovement = GetViewport().GetMousePosition(); 
        var aud = GetSetting("audiooffset").ToString();
        if (aud == null) AudioOffset = 0; // AudioOffset Subsystem
        else AudioOffset = float.Parse(aud);

        TopPanelPosition = GetTree().Root.GetNode<ColorRect>("TopPanelOnTop/InfoBar").Position.Y + 50;
        if (Input.IsActionJustPressed("Hide Panel"))
        {
            toppaneltoggle(SessionConfig.TopPanelHide);
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

        SessionConfig.Backgrounddim = int.TryParse(GetSetting("backgrounddim").ToString(), out int bkd) ? bkd : 70;
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

    public override void _Notification(int what)
    {
        switch (what)
        {
            case (int)NotificationApplicationFocusIn:
                GD.Print($"Detected focused going back to normal fps :D");
                Unfocused = false;
                RefreshFPS();
                break;

            case (int)NotificationApplicationFocusOut:
                GD.Print($"Detected not focused going to {UnfocusedFPS} FPS");
                Unfocused = true;
                Engine.MaxFps = UnfocusedFPS;
                break;
        }
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

    private Variant ConvertJsonElement(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                return element.GetString();

            case JsonValueKind.Number:
                if (element.TryGetInt64(out long l))
                    return l;
                return element.GetDouble();

            case JsonValueKind.True:
            case JsonValueKind.False:
                return element.GetBoolean();

            case JsonValueKind.Object:
                var dict = new Godot.Collections.Dictionary();
                foreach (var prop in element.EnumerateObject())
                    dict[prop.Name] = ConvertJsonElement(prop.Value);
                return dict;

            case JsonValueKind.Array:
                var array = new Godot.Collections.Array();
                foreach (var item in element.EnumerateArray())
                    array.Add(ConvertJsonElement(item));
                return array;

            case JsonValueKind.Null:
            default:
                return default;
        }
    }
    
    // Get a setting
    public static object GetSetting(string key)
    {
        return Configuration.ContainsKey(key) ? Configuration[key] : null;
    }
}
