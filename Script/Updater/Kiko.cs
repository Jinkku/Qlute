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
    private Version Version { get; set; }
    public static bool isUpdating { get; set; }
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
        args = OS.GetCmdlineArgs();
        if (args.Contains("--update"))
        {
            GD.Print("Updating...");
            InitUpdateProcess();
        }
        else if (!args.Contains("--ignore-update"))
        {
#if !DEBUG
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
            KikoApi.Request("https://github.com/Jinkkuu/Qlute/releases/latest/download/RELEASE");
#endif
#if DEBUG
            GD.Print("Skipping checking for updates because debug tick is enabled.");
#endif
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
        KikoApi.RequestCompleted += (result, code, headers, body) =>
        {
            if (code == 200)
            {
                Notify.Post("Update is ready to install!\nClick to install the update.", uri:$"updatefile:/{KikoApi.DownloadFile}");
            }
        };
        if (responseCode == 200)
        {
            isUpdating = false;
            VersionCode = new Version(Encoding.UTF8.GetString(body).TrimEnd(System.Environment.NewLine.ToCharArray()));
            if (VersionCode > Version)
            {
                KikoApi.DownloadFile = Path.Combine(Path.GetTempPath(), platformZip);
                KikoApi.Timeout = 0;
                KikoApi.Request($"https://github.com/Jinkkuu/Qlute/releases/latest/download/{platformZip}");
                Notify.Post($"Downloading update...", ProgressGetter: () => KikoApi.GetDownloadedBytes(), Max: () => KikoApi.GetBodySize());
            }
        }
        else
        {
            GD.PrintErr("Kiko failed with response code: " + responseCode);
        }
    }
}
