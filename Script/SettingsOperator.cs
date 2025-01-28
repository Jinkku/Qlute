using Godot;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

public partial class SettingsOperator : Node
{	
	public string homedir = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile) + "/.qlute";
	public string beatmapsdir => homedir + "/beatmaps";
	public string downloadsdir => homedir + "/downloads";
	public string replaydir => homedir + "/replays";
	public string screenshotdir => homedir + "/screenshots";
	public string skinsdir => homedir + "/skins";
    public string settingsfile => homedir + "/settings.cfg";
    public string nanashidb => homedir + "/nanashi.db";
	public List<string> Beatmaps = new List<string>();
	public List<string> BeatmapsURLs = new List<string>();
    public Dictionary<string, object> Configuration { get; set; } = new Dictionary<string, object>
    {
        { "scaled", false },
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
    public Dictionary<string, object> Sessioncfg { get; set; } = new Dictionary<string, object>
    {
        { "TopPanelSlidein", false },
        { "Reloadmap", false },
        { "loggedin", false },
        { "showaccountpro", false },
        { "ranknumber", null },
		{ "playercolour", null },
        { "customapi", false},
		{ "client-id", null },
        { "client-secret", null },

    };
	public override void _Ready()
	{
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
