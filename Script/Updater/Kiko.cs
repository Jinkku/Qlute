using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.IO.Compression;

public partial class Kiko : Node
{
    public string[] args { get; set; }
    private HttpRequest KikoApi { get; set; }
    public static Version VersionCode { get; set; }
    public static Version Version { get; set; }
    public static string UpdateChannelTagName { get; set; } = "x.x.x-x";
    
    public enum UpdateChannel { Stable, Experimental }
    public static UpdateChannel CurrentUpdateChannel { get; set; } = UpdateChannel.Stable;
    public static bool isUpdating { get; set; }
    public static bool CheckingUpdates = false;
	public static readonly string extractPath = Path.Combine(Path.GetTempPath(), "QluteUpdateFile");
    public static readonly string platformZip = OS.GetName() switch
    {
        "Windows" => "Windows.zip",
        "Linux" => "Linux.zip",
        _ => "unknown.zip"
    };
    public static readonly string executableName = OS.GetName() switch
    {
        "Windows" => "Qlute.exe",
        "Linux" => "Qlute.x86_64",
        _ => ""
    };
	private void CopyDirectory(string sourceDir, string destDir)
		{
			Directory.CreateDirectory(destDir);
			foreach (var file in Directory.GetFiles(sourceDir))
			{
				string destFile = Path.Combine(destDir, Path.GetFileName(file));
				File.Copy(file, destFile, true);
			}
			foreach (var dir in Directory.GetDirectories(sourceDir))
			{
				string destSubDir = Path.Combine(destDir, Path.GetFileName(dir));
				CopyDirectory(dir, destSubDir);
			}
		}

    private void InitUpdateProcess()
    {
        if (Directory.Exists(extractPath))
        {
            GD.Print("Waiting for other process to close");
            System.Threading.Thread.Sleep(3000);
            GD.Print("Start.... NOW!");
            GD.Print("Open file....");
            var filepath = Path.Combine(Path.GetTempPath(), platformZip);
            GD.Print("set file end path....");
            string executablePath = Path.Combine(SettingsOperator.GetSetting("gamepath").ToString(), executableName);
            GD.Print("extracting....");
            using (var archive = ZipFile.OpenRead(filepath))
            {
                GD.Print($"Extracting from '{extractPath}' to '{SettingsOperator.GetSetting("gamepath").ToString()}'");
                archive.ExtractToDirectory(SettingsOperator.GetSetting("gamepath").ToString(), true);
            }
		    GD.Print($"Extracted to: {SettingsOperator.GetSetting("gamepath").ToString()}");
            System.Threading.Thread.Sleep(1000);
            if (File.Exists(executablePath))
            {
                try
                {
                    System.Diagnostics.Process.Start(executablePath);
                    GetTree().Quit();
                }
                catch (Exception ex)
                {
                    GD.PrintErr($"Failed to start the new executable: {ex.Message}");
                }
            }
            else
            {
                GD.PrintErr($"Executable not found: {executablePath}");
            }
        }
        else
        {
            GD.PrintErr("[Kiko] Update failed... I can't find the directory :/");
        }
    }

    public override void _Ready()
    {
        if (SettingsOperator.GetSetting("updatebranch") != "stable") CurrentUpdateChannel = UpdateChannel.Experimental;
        CheckUpdates();
    }

    /// <summary>
    /// Checks for updates for the game.
    /// </summary>
    public void CheckUpdates(bool isinSettings = false)
    {
        CheckingUpdates = true;
        args = OS.GetCmdlineArgs();
        if (args.Contains("--update"))
        {
            GD.Print("Updating...");
            InitUpdateProcess();
        }
        else if (!args.Contains("--ignore-update"))
        {
//#if !DEBUG
            if (isinSettings) Notify.Post("Checking for updates...");
            GD.Print("Checking for updates....");
            isUpdating = true;
            // Remove extractPath directory if it exists
            if (Directory.Exists(extractPath))
            {
                Directory.Delete(extractPath, true);
            }
            if (File.Exists(Path.Combine(Path.GetTempPath(), platformZip)))
            {
                File.Delete(Path.Combine(Path.GetTempPath(), platformZip));
            }
            Version = new Version(ProjectSettings.GetSetting("application/config/version").ToString());
            KikoApi = new HttpRequest();
            AddChild(KikoApi);
            KikoApi.Timeout = 30;
            KikoApi.Connect("request_completed", new Callable(this, nameof(_KikoApiDone)));
            KikoApi.Request(
                "https://api.github.com/repos/Jinkku/Qlute/releases",
                new string[] { $"User-Agent: QluteUpdater/{Version}" }
            );
//#endif
//#if DEBUG
//            if (isinSettings) Notify.Post("Skipping updates because your on debug build.");
//            GD.Print("Skipping checking for updates because debug tick is enabled.");
//#endif
        }
        else if (args.Contains("--ignore-update"))
        {
            GD.Print("Ignored updates for now...");
        }
    }

    public void PrepareUpdateProcess()
    {
        GD.Print("Download completed!");
        // Extract the downloaded zip file
        string zipPath = KikoApi.DownloadFile;
        Directory.CreateDirectory(Kiko.extractPath);

        using (var archive = ZipFile.OpenRead(zipPath))
        {
            archive.ExtractToDirectory(Kiko.extractPath, true);
        }

        GD.Print($"Extracted to: {Kiko.extractPath}");
        string executablePath = Path.Combine(Kiko.extractPath, executableName);

        if (File.Exists(executablePath))
        {
            try
            {
                System.Diagnostics.Process.Start(executablePath, "--update");
            }
            catch (Exception ex)
            {
                GD.PrintErr($"Failed to start the new executable: {ex.Message}");
            }
        }
        else
        {
            GD.PrintErr($"Executable not found: {executablePath}");
        }

        GetTree().Quit();
    }
    
    private void _KikoApiDone(long result, long responseCode, string[] headers, byte[] body)
    {
        KikoApi.Disconnect("request_completed", new Callable(this, nameof(_KikoApiDone)));

        if (responseCode != 200)
        {
            GD.PrintErr("Kiko failed with response code: " + responseCode);
            isUpdating = false;
            return;
        }

        string jsonText = Encoding.UTF8.GetString(body);
        var json = new Json();
        if (json.Parse(jsonText) != Error.Ok)
        {
            GD.PrintErr("Failed to parse release JSON: " + json.GetErrorMessage());
            isUpdating = false;
            return;
        }

        var releases = json.Data.AsGodotArray();

        Godot.Collections.Dictionary chosen = null;
        foreach (var item in releases)
        {
            var release = item.AsGodotDictionary();
            bool isDraft = release["draft"].AsBool();
            bool isPrerelease = release["prerelease"].AsBool();
            if (isDraft) continue;

            // Stable channel: skip prereleases, take the first (newest) match.
            // Experimental channel: take the very first entry regardless (GitHub lists newest first).
            if (CurrentUpdateChannel == UpdateChannel.Stable && isPrerelease) continue;

            chosen = release;
            break;
        }

        if (chosen == null)
        {
            GD.Print("No matching release found for channel: " + CurrentUpdateChannel);
            isUpdating = false;
            return;
        }

        string tagName = chosen["tag_name"].AsString();
        string versionPart = tagName.Split('-')[0];
        string suffix = tagName.Contains('-') ? tagName.Substring(tagName.IndexOf('-') + 1) : null;
        VersionCode = new Version(versionPart);

        if (VersionCode == Version)
        {
            GD.Print("Already up to date.");
            isUpdating = false;
            return;
        }

        string downloadUrl = null;
        foreach (var a in chosen["assets"].AsGodotArray())
        {
            var asset = a.AsGodotDictionary();
            if (asset["name"].AsString() == platformZip)
            {
                downloadUrl = asset["browser_download_url"].AsString();
                break;
            }
        }

        if (downloadUrl == null)
        {
            GD.PrintErr($"No matching asset '{platformZip}' found in release {tagName}");
            isUpdating = false;
            return;
        }

        KikoApi.Timeout = 0;
        KikoApi.DownloadFile = Path.Combine(Path.GetTempPath(), platformZip);
        KikoApi.RequestCompleted += (res, code, hdrs, bdy) =>
        {
            if (code == 200)
            {
                Notify.Post("Update is ready to install!\nClick to install the update.",
                    uri: $"updatefile:/{KikoApi.DownloadFile}",
                    Type: NotificationIcons.NotificationType.Download);
            }
        };
        KikoApi.Request(downloadUrl, new string[] { "User-Agent: Qlute-Updater" });
        Notify.Post("Downloading update...",
            ProgressGetter: () => KikoApi.GetDownloadedBytes(),
            Max: () => KikoApi.GetBodySize(),
            Type: NotificationIcons.NotificationType.Download);
        CheckingUpdates = false;
    }
}
