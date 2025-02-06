using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

public partial class SettingsOperator : Node
{	
	//public string homedir = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile) + "/.qlute";
    public string homedir = OS.GetUserDataDir();
	public string beatmapsdir => homedir + "/beatmaps";
    public float ppbase = 0.072f;
	public string downloadsdir => homedir + "/downloads";
	public string replaydir => homedir + "/replays";
	public string screenshotdir => homedir + "/screenshots";
	public string skinsdir => homedir + "/skins";
    public string settingsfile => homedir + "/settings.cfg";
    public string nanashidb => homedir + "/nanashi.db";
    //public Dictionary<int, object> Beatmaps { get; set; } = new Dictionary<int, object>{

	public List<Dictionary<string,object>> Beatmaps = new List<Dictionary<string,object>>();
    public Dictionary<string, object> Configuration { get; set; } = new Dictionary<string, object>
    {
        { "scaled", false },
        { "windowmode", "fullscreen" },
		{ "volume", 1 },
		{ "skin", null },
		{ "username", null },
		{ "password", null },
		{ "stayloggedin", true },
		{ "api", "https://qlute.pxki.us.to/" },
		{ "client-id", null },
        { "client-secret", null },
		{ "teststrip", "Ya" },

    };

    public Dictionary<string, object> Configurationbk {get; set;}

    public float Get_ppvalue(string filename){
        using var file = FileAccess.Open(filename, FileAccess.ModeFlags.Read);
        var text = file.GetAsText();
        var lines = text.Split("\n");
        var ppvalue = 0.0f;
        var hitob = 0;
        var isHitObjectSection = false;
        foreach (string line in lines)
        {
            if (line.Trim() == "[HitObjects]")
            {
                isHitObjectSection = true;
                continue;
            }

            if (isHitObjectSection)
            {
                // Break if we reach an empty line or another section
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("["))
                    break;
                hitob++;
            }
        }
        ppvalue = ppbase * hitob;
        return ppvalue;
        
        }
    // Imaging processing part of the game because godot don't have a way to load images externally i suppose-
    public Texture2D LoadImage(string path)
    {
        var image = new Image();
        image.Load(path);

        var imageTexture = new ImageTexture();
        imageTexture.SetImage(image);

        // Free the image from memory
        image.Dispose();

        return imageTexture;
    }
    public void Parse_Beatmapfile(string filename){
        GD.Print("Parsing beatmap file...");
        using var file = FileAccess.Open(filename, FileAccess.ModeFlags.Read);
        var text = file.GetAsText();
        var lines = text.Split("\n");
        float ppvalue = Get_ppvalue(filename);
        string songtitle = "";
        string artist = "";
        string version = "";
        string mapper = "";
        int levelrating = 0;
        string background = "";
        string audio = "";
        string rawurl = filename;
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
            keycount = (int)float.Parse(line.Split(":")[1].Trim());
            }
            if (line.StartsWith("AudioFilename:"))
            {
            audio = line.Split(":")[1].Trim();
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
        }
		Beatmaps.Add(new Dictionary<string, object>{
            { "Title", songtitle },
            { "Artist", artist },
            { "Mapper", mapper },
            { "KeyCount", keycount },
            { "Version", version },
            { "pp", ppvalue },
            { "levelrating", ppvalue*0.05 },
            { "background", background },
            { "audio", audio },
            { "rawurl", rawurl },
            { "path", path },
        });
    }
    public Dictionary<string, int> Gameplaycfg { get; set; } = new Dictionary<string, int>
    {
        {"score", 0},
        {"pp", 0},
        {"time", 0},
        {"timetotal", 0},
        {"max", 0},
        {"great", 0},
        {"meh", 0},
        {"bad", 0},
    };
    public Dictionary<string, object> Sessioncfg { get; set; } = new Dictionary<string, object>
    {
        { "TopPanelSlidein", false },
        { "Reloadmap", false },
        { "reloadbdb", false },
        { "loggedin", false },
        { "showaccountpro", false },
        { "ranknumber", null },
		{ "playercolour", null },
        { "SongID", 0 },
        { "totalbeatmaps", 0 },
        { "beatmapurl", null },
        { "beatmapname", null },
        { "beatmapartist", null },
        { "beatmapdiff", null },
        { "customapi", false},
        { "fps" , 0},
        { "ms" , 0.0f},
        { "scrollspeed", (int)1346 },
        { "background" , null},
		{ "client-id", null },
        { "client-secret", null },

    };

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
            saveFile.Close();
        }
        else
        {
            GD.Print("Creating config...");
            SaveSettings();
        }
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
