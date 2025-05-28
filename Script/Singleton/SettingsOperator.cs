using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;



public class DanceCounter {
        public int time {get;set;}
        public bool flash {get;set;}
}

public partial class SettingsOperator : Node
{	
	//public string homedir = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile) + "/.qlute";
    public static string homedir = OS.GetUserDataDir();
	public static string tempdir => homedir + "/temp";
	public static string beatmapsdir => homedir + "/beatmaps";
    public static float ppbase = 0.035f;
	public static string downloadsdir => homedir + "/downloads";
	public static string replaydir => homedir + "/replays";
	public static string screenshotdir => homedir + "/screenshots";
	public static string skinsdir => homedir + "/skins";
    public static string settingsfile => homedir + "/settings.cfg";
    public static string Qlutedb => homedir + "/qlute.db";
    public static bool loopaudio = false;
    public static bool jukebox = false;
    public int backgrounddim {get;set;}
    public int scrollspeed {get;set;}
    public static double AllMiliSecondsFromBeatmap {get;set;}
    public static double MiliSecondsFromBeatmap {get;set;}
    public static int MiliSecondsFromBeatmapTimes {get;set;}
    public static List<Dictionary<string,object>> Beatmaps = new List<Dictionary<string,object>>();
    public Dictionary<string, object> Configuration { get; set; } = new Dictionary<string, object>
    {
        { "scaled", false },
        { "windowmode", 0 },
		{ "volume", 1 },
		{ "backgrounddim", 70 },
		{ "audiooffset", 0 },
		{ "skin", null },
		{ "username", null },
		{ "password", null },
		{ "stayloggedin", true },
		{ "api", "https://qlute.pxki.us.to/" },
		{ "client-id", null },
        { "client-secret", null },
        { "scrollspeed", (int)1346 }, // 11485 ms max
		{ "teststrip", "Ya" },

    };
    public Dictionary<string, object> Configurationbk {get; set;}


    public static Texture2D LoadImage(string path)
    {
        if (!FileAccess.FileExists(path))
        {
            Notify.Post("Image could not be loaded, because it doesn't exist!");
            return null;
        }

        try
        {
            using var image = Image.LoadFromFile(path);
            return ImageTexture.CreateFromImage(image);
        }
        catch (Exception)
        {
            Notify.Post("Failed to load image!");
            return null;
        }
    }
    public static int RndSongID(){
        int id = new Random().Next(0, Beatmaps.Count);
        return id;
    }
    public void SelectSongID(int id){
        if(Beatmaps.ElementAt(id) != null)
        {
            var beatmap = Beatmaps[id];
            Sessioncfg["SongID"] = id;
            Sessioncfg["beatmapurl"] = beatmap["rawurl"];
            Sessioncfg["beatmaptitle"] = beatmap["Title"];
            Sessioncfg["beatmapartist"] = beatmap["Artist"];
            Sessioncfg["beatmapdiff"] = beatmap["Version"];
            Sessioncfg["beatmapbpm"] = beatmap["bpm"];
            Gameplaycfg["timetotal"] = (int)beatmap["timetotal"];
            Sessioncfg["beatmapmapper"] = beatmap["Mapper"];
            Sessioncfg["beatmapaccuracy"] = (int)beatmap["accuracy"];
            Sessioncfg["levelrating"] = (double)beatmap["levelrating"];
            Sessioncfg["osubeatid"] = (int)beatmap["osubeatid"];
            Sessioncfg["osubeatidset"] = (int)beatmap["osubeatidset"];
		    var Texture = LoadImage(beatmap["path"].ToString()+beatmap["background"].ToString());
		    Sessioncfg["background"] = (Texture2D)Texture;
            Gameplaycfg["maxpp"] = Convert.ToInt32(beatmap["pp"]);
		    string audioPath = beatmap["path"]+ "" +beatmap["audio"];
            if (System.IO.File.Exists(audioPath))
            {
                AudioStream filestream = null;
                if (audioPath.EndsWith(".mp3")){
                    filestream = AudioPlayer.LoadMP3(audioPath);
                } else if (audioPath.EndsWith(".wav")){
                    filestream = AudioPlayer.LoadWAV(audioPath);
                } else if (audioPath.EndsWith(".ogg")){
                    filestream = AudioPlayer.LoadOGG(audioPath);
                }
                AudioPlayer.Instance.Stream = filestream;
                AudioPlayer.Instance.Play();
            }
            else
            {
                GD.PrintErr("Audio file not found: " + audioPath);
            }
        } else{ GD.PrintErr("Can't select a song that don't exist :/");}
    }
    public static double Get_ppvalue(int max, int great, int meh, int bad, double multiplier = 1,int combo = 0){
        //bad = Math.Max(1,bad);
        var ppvalue = 0.0;
        ppvalue = max * ppbase;
        ppvalue -= ppbase/2 * great;
        ppvalue -= ppbase/3 * meh;
        ppvalue -= ppbase * bad;
        ppvalue += combo * ppbase;
        ppvalue *=(float)multiplier;
        return Math.Max(0,ppvalue);
        }
    public static void Parse_Beatmapfile(string filename){
        using var file = FileAccess.Open(filename, FileAccess.ModeFlags.Read);
        var text = file.GetAsText();
        var lines = text.Split("\n");
        string songtitle = "";
        string artist = "";
        string version = "";
        int timetotal = 0;
        int bpm = 0;
        int osubeatid = 0;
        int accuracy = 0;
        int osubeatidset = 0;
        int qlbeatid = 0;
        int qlbeatidset = 0;
        double ppvalue = 0;
        string mapper = "";
        double levelrating = 0;
        int notetime = 0;
        string background = "";
        string audio = "";
        string rawurl = filename;
        var hitob = 0;
        List<DanceCounter> dance = [];
        var isHitObjectSection = false;
        string path = filename.Replace(filename.Split("/").Last(),"");
        int keycount = 4;
        foreach (var line in lines)
        {
            if (line.StartsWith("Title:"))
            {
            songtitle = line.Split(":")[1].Trim();
            }
            if (line.StartsWith("Artist:"))
            {
            artist = line.Split(":")[1].Trim();
            }
            if (line.StartsWith("CircleSize:"))
            {
            //keycount = (int)float.Parse(line.Split(":")[1].Trim());
            keycount = float.TryParse(line.Split(":")[1].Trim(), out float keycountv) ? (int)keycountv : 4;
            }if (line.StartsWith("OverallDifficulty:"))
            {
            //keycount = (int)float.Parse(line.Split(":")[1].Trim());
            accuracy = float.TryParse(line.Split(":")[1].Trim(), out float accuracyv ) ? (int)accuracyv : 0;
            }
            if (line.StartsWith("AudioFilename:"))
            {
            audio = line.Split(":")[1].Trim();
            }
            if (line.StartsWith("BeatmapID:"))
            {
            osubeatid = int.TryParse(line.Split(":")[1].Trim(), out int osubeatidv) ? (int)osubeatidv : 0;
            }
            if (line.StartsWith("BeatmapSetID:"))
            {
            osubeatidset = int.TryParse(line.Split(":")[1].Trim(), out int osubeatidsetv) ? (int)osubeatidsetv : 0;
            }
            if (line.StartsWith("QluteBeatID:"))
            {
            qlbeatid = int.TryParse(line.Split(":")[1].Trim(), out int qlbeatidv) ? (int)qlbeatidv : 0;
            }
            if (line.StartsWith("QluteBeatIDSet:"))
            {
            qlbeatidset = int.TryParse(line.Split(":")[1].Trim(), out int qlbeatidsetv) ? (int)qlbeatidsetv : 0;
            }
            if (line.StartsWith("Creator:"))
            {
            mapper = line.Split(":")[1].Trim();
            }
            if (line.StartsWith("Version:"))
            {
            version = line.Split(":")[1].Trim();
            }
            if (line.StartsWith("0,0,\"") && line.Contains("\""))
            {
            var parts = line.Split("\"");
            if (parts.Length > 1)
            {
                background = parts[1].Trim();
            }
            }
            if (line.StartsWith("[TimingPoints]"))
            {
            var timingPointLines = lines.SkipWhile(l => !l.StartsWith("[TimingPoints]")).Skip(1);
            bool foundbpm = false;
            foreach (var timingLine in timingPointLines)
            {
                if (string.IsNullOrWhiteSpace(timingLine) || timingLine.StartsWith("["))
                break;

                var timingParts = timingLine.Split(",");
                if (timingParts.Length > 1 && float.TryParse(timingParts[1], out float bpmValue) && !foundbpm)
                {
                bpmValue = 60000 / bpmValue;
                bpm = (int)bpmValue;
                foundbpm = true;
                }
                if (timingParts.Length > 1 && int.TryParse(timingParts.First(), out int timecount) && int.TryParse(timingParts.Last(), out int flashtime)){
                    dance.Add(new DanceCounter { time = timecount, flash = flashtime == 1 });
                }
            }
            }
            if (line.Trim() == "[HitObjects]")
            {
                isHitObjectSection = true;
                continue;
            }

            if (isHitObjectSection)
            {
                // Break if we reach an empty line or another section
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("["))
                {
                    ppvalue = Get_ppvalue(hitob,0,0,0,combo: hitob);
                    isHitObjectSection = !isHitObjectSection;
                    levelrating = hitob * 0.005;
                    timetotal = notetime;
                    break;
                }
                var notecfg = line.Split(new[] { ',', ':' });
                notetime = float.TryParse(notecfg[2], out float notetimev) ? (int)notetimev : 0;
                var notesect = notecfg[0];
                hitob++;
            }
        }
		Beatmaps.Add(new Dictionary<string, object>{
            { "Title", songtitle },
            { "Artist", artist },
            { "Mapper", mapper },
            { "KeyCount", keycount },
            { "Version", version },
            { "pp", ppvalue },
            { "osubeatid", osubeatid },
            { "osubeatidset", osubeatidset },
            { "beatid", qlbeatid },
            { "beatidset", qlbeatidset },
            { "bpm", bpm },
            { "dance", dance },
            { "timetotal", (int)timetotal },
            { "levelrating", levelrating },
            { "accuracy", accuracy},
            { "background", background },
            { "audio", audio },
            { "rawurl", rawurl },
            { "path", path },
        });
    }
    public static void ResetScore(){
        Gameplaycfg["score"] = 0;
        Gameplaycfg["pp"] = 0;
        Gameplaycfg["max"] = 0;
        Gameplaycfg["great"] = 0;
        Gameplaycfg["meh"] = 0;
        Gameplaycfg["bad"] = 0;
        Gameplaycfg["accuracy"] = 100;
        Gameplaycfg["time"] = 0;
        Gameplaycfg["combo"] = 0;
        Gameplaycfg["maxcombo"] = 0;
        Gameplaycfg["avgms"] = 0;
    }
    public static void Addms(double ms){
        AllMiliSecondsFromBeatmap += ms;
        MiliSecondsFromBeatmapTimes++;
    }
    public static double Getms(){
        return AllMiliSecondsFromBeatmap/MiliSecondsFromBeatmapTimes;
    }
    public static void Resetms(){
        AllMiliSecondsFromBeatmap = 0;
        MiliSecondsFromBeatmapTimes = 0;
    }
    public static Dictionary<string, double> Gameplaycfg { get; set; } = new Dictionary<string, double>
    {
        {"score", 0},
        {"pp", 0},
        { "maxpp", 0 },
        {"time", 0},
        {"timetotal", 0},
        {"accuracy", 100},
        {"combo", 0},
        {"maxcombo",0},
        {"max", 0},
        {"avgms", 0},
        {"great", 0},
        {"meh", 0},
        {"bad", 0},
    };
    public static int ranked_points {get;set;}
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
        { "notificationpanelv", false },
        { "ranknumber", null },
		{ "playercolour", null },
        { "SongID", -1 },
        { "totalbeatmaps", 0 },
        { "beatmapurl", null },
        { "beatmaptitle", null },
        { "beatmapartist", null },
        { "beatmapmapper", null },
        { "beatmapbpm", (int)160 },
        { "osubeatid", 0 },
        { "osubeatidset", 0 },
        { "beatmapaccuracy", (int)0 },
        { "beatmapdiff", null },
        { "customapi", false},
        { "multiplier" , 1.0f},
        { "fps" , 0},
        { "ms" , 0.0f},
        { "background" , null},
		{ "client-id", null },
        { "client-secret", null },

    };
    public void toppaneltoggle(){
		Sessioncfg["toppanelhide"] = !(bool)Sessioncfg["toppanelhide"];
		AnimationPlayer Ana = GetTree().Root.GetNode<AnimationPlayer>("TopPanelOnTop/TopPanel/Wabamp");
		if (((bool)Sessioncfg["toppanelhide"] == true)) Ana.PlayBackwards("Bootup"); else Ana.Play("Bootup");}

    public override void _Process(double _delta)
	{

		if (Input.IsActionJustPressed("Hide Panel")){
            toppaneltoggle();
        }
    }
    public override void _Ready()
	{
        Configurationbk = new Dictionary<string, object>(Configuration);
        GD.Print("Please wait...");
        GD.Print("Checking if config file is saved...");
        if (System.IO.File.Exists(settingsfile))
        {
            GD.Print("Config file found... Loading it up :)");
            using var saveFile = FileAccess.Open(settingsfile, FileAccess.ModeFlags.Read);
            var json = saveFile.GetAsText();
            Configuration = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
            foreach (string s in Configurationbk.Keys){
                if (!Configuration.ContainsKey(s)){
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
        backgrounddim = int.TryParse(GetSetting("backgrounddim").ToString(), out int bkd) ? bkd : 70;
		var resolutionIndex = int.TryParse(GetSetting("windowmode")?.ToString(), out int mode) ? mode : 0;
		changeres(resolutionIndex);
    }
    public void changeres(int index) {
		GD.Print(string.Format("Resolution changed to {0}", index));
		if (index == 0){
			DisplayServer.WindowSetMode(DisplayServer.WindowMode.ExclusiveFullscreen);
		} else if (index == 1){
			DisplayServer.WindowSetMode(DisplayServer.WindowMode.Fullscreen);
		} else if (index == 2){
			DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
		}
		SetSetting("windowmode",index);
    }
    // Set a setting
    public void SetSetting(string key, object value)
    {
        if (Configuration.ContainsKey(key))
            Configuration[key] = value;
            SaveSettings();
    }

    // Save settings to file
    public void SaveSettings()
    {
    using var saveFile = FileAccess.Open(settingsfile, FileAccess.ModeFlags.Write);
    var json = JsonSerializer.Serialize(Configuration);
    saveFile.StoreString(json);
    saveFile.Close();
    }

    // Get a setting
    public object GetSetting(string key)
    {
        return Configuration.ContainsKey(key) ? Configuration[key] : null;
    }

}
