using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Threading.Tasks;

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
    /// <summary>A verified update zip is sitting in <see cref="zipPath"/>.</summary>
    public static bool UpdateReady { get; private set; }
    public static bool CheckingUpdates = false;
    /// <summary>Updater's own temp folder; everything the updater writes lives here.</summary>
    public static readonly string updateDir = Path.Combine(Path.GetTempPath(), "Qlute");
	public static readonly string extractPath = Path.Combine(updateDir, "QluteUpdateFile");
    public static readonly string platformZip = OS.GetName() switch
    {
        "Windows" => "Windows.zip",
        "Linux" => "Linux.zip",
        _ => "unknown.zip"
    };
    public static string zipPath => Path.Combine(updateDir, platformZip);
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

    /// <summary>
    /// Runs in the freshly extracted build (started with --update): waits for the old game to exit,
    /// installs the zip over gamepath, launches the installed game and quits. It must never keep
    /// running as a normal game from the temp folder, so every failure path also quits.
    /// </summary>
    private void InitUpdateProcess()
    {
        string gamePath = SettingsOperator.GetSetting("gamepath").ToString();
        string executablePath = Path.Combine(gamePath, executableName);
        try
        {
            if (!File.Exists(zipPath))
                throw new FileNotFoundException($"Update file not found: {zipPath}");

            WaitForOldGame();

            // Files can stay locked for a moment after the old process exits (Windows), so retry.
            Exception last = null;
            for (int attempt = 1; attempt <= 10; attempt++)
            {
                try
                {
                    GD.Print($"[Kiko] Installing to '{gamePath}' (attempt {attempt})");
                    using (var archive = ZipFile.OpenRead(zipPath))
                        archive.ExtractToDirectory(gamePath, true);
                    last = null;
                    break;
                }
                catch (IOException ex) { last = ex; }
                catch (UnauthorizedAccessException ex) { last = ex; }
                System.Threading.Thread.Sleep(1000);
            }
            if (last != null) throw last;
            GD.Print($"[Kiko] Installed to: {gamePath}");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[Kiko] Update failed: {ex}");
            OS.Alert($"The update couldn't be installed:\n{ex.Message}\n\nThe game will start without updating.", "Qlute update failed");
        }

        // Start whatever is installed now (updated, or the old build if the install failed).
        try
        {
            if (File.Exists(executablePath)) System.Diagnostics.Process.Start(executablePath);
            else GD.PrintErr($"[Kiko] Executable not found: {executablePath}");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[Kiko] Failed to start the game: {ex.Message}");
        }
        GetTree().Quit();
    }

    /// <summary>Waits (up to 30s) for the game that launched us, identified by --pid=N.</summary>
    private void WaitForOldGame()
    {
        string pidArg = args.FirstOrDefault(a => a.StartsWith("--pid="));
        if (pidArg != null && int.TryParse(pidArg.Substring("--pid=".Length), out int pid))
        {
            try
            {
                using var old = System.Diagnostics.Process.GetProcessById(pid);
                GD.Print($"[Kiko] Waiting for old game (pid {pid}) to close...");
                if (!old.WaitForExit(30000)) GD.PrintErr("[Kiko] Old game still running after 30s, trying anyway.");
            }
            catch (ArgumentException) { /* already exited */ }
        }
        else
        {
            System.Threading.Thread.Sleep(3000);
        }
    }

    public override void _Ready()
    {
        if (SettingsOperator.GetSetting("updatebranch").ToString() != "stable") CurrentUpdateChannel = UpdateChannel.Experimental;
        
        CheckUpdates();
    }

    /// <summary>
    /// Checks for updates for the game.
    /// </summary>
    public void CheckUpdates(bool isinSettings = false)
    {
        args = OS.GetCmdlineArgs();
        Version ??= new Version(ProjectSettings.GetSetting("application/config/version").ToString());
        if (args.Contains("--update"))
        {
            GD.Print("Updating...");
            InitUpdateProcess();
        }
        else if (!args.Contains("--ignore-update"))
        {
#if !DEBUG
            if (UpdateReady)
            {
                // Already downloaded and verified; don't delete it by checking again.
                PostReadyNotification();
                return;
            }
            if (isUpdating)
            {
                if (isinSettings) Notify.Post("Already checking for updates...");
                return;
            }
            if (isinSettings) Notify.Post("Checking for updates...");
            GD.Print("Checking for updates....");
            isUpdating = true;
            // Clear leftovers from a previous update (only ever inside our own temp folder)
            try
            {
                if (Directory.Exists(extractPath)) Directory.Delete(extractPath, true);
                if (File.Exists(zipPath)) File.Delete(zipPath);
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[Kiko] Couldn't clean old update files: {ex.Message}");
            }
            VersionCode = null;
            KikoApi?.QueueFree();
            KikoApi = new HttpRequest();
            AddChild(KikoApi);
            KikoApi.Timeout = 30;
            KikoApi.Connect("request_completed", new Callable(this, nameof(_KikoApiDone)));
            KikoApi.Request(
                "https://api.github.com/repos/Jinkku/Qlute/releases",
                new string[] { $"User-Agent: QluteUpdater/{Version}" }
            );
#endif
#if DEBUG
            if (isinSettings) Notify.Post("Skipping updates because your on debug build.");
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
        string executablePath = Path.Combine(extractPath, executableName);
        try
        {
            if (Directory.Exists(extractPath)) Directory.Delete(extractPath, true);
            Directory.CreateDirectory(extractPath);
            using (var archive = ZipFile.OpenRead(zipPath))
                archive.ExtractToDirectory(extractPath, true);
            GD.Print($"[Kiko] Extracted to: {extractPath}");

            if (!File.Exists(executablePath))
                throw new FileNotFoundException($"Executable not found: {executablePath}");
            System.Diagnostics.Process.Start(executablePath, new[] { "--update", $"--pid={OS.GetProcessId()}" });
        }
        catch (Exception ex)
        {
            // Stay open instead of quitting into nothing.
            GD.PrintErr($"[Kiko] Couldn't start the update: {ex}");
            Notify.Post($"Couldn't install the update:\n{ex.Message}");
            return;
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

        if (!(VersionCode > Version))
        {
            GD.Print($"Already up to date. (installed {Version}, latest on channel {VersionCode})");
            isUpdating = false;
            return;
        }

        string downloadUrl = null;
        string expectedSha256 = null;
        foreach (var a in chosen["assets"].AsGodotArray())
        {
            var asset = a.AsGodotDictionary();
            if (asset["name"].AsString() == platformZip)
            {
                downloadUrl = asset["browser_download_url"].AsString();
                // GitHub reports "sha256:<hex>" per asset
                if (asset.ContainsKey("digest") && asset["digest"].VariantType == Variant.Type.String)
                {
                    string digest = asset["digest"].AsString();
                    if (digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
                        expectedSha256 = digest.Substring("sha256:".Length).Trim().ToLowerInvariant();
                }
                break;
            }
        }

        if (downloadUrl == null)
        {
            GD.PrintErr($"No matching asset '{platformZip}' found in release {tagName}");
            isUpdating = false;
            return;
        }

        if (expectedSha256 == null)
        {
            GD.PrintErr($"[Kiko] Release {tagName} has no sha256 digest for '{platformZip}', refusing to download it.");
            isUpdating = false;
            return;
        }

        Directory.CreateDirectory(updateDir);
        KikoApi.Timeout = 0;
        KikoApi.DownloadFile = zipPath;
        KikoApi.RequestCompleted += (res, code, hdrs, bdy) =>
        {
            if (res != (long)HttpRequest.Result.Success || code != 200)
            {
                GD.PrintErr($"[Kiko] Update download failed (result {res}, HTTP {code})");
                isUpdating = false;
                return;
            }
            string file = KikoApi.DownloadFile;
            Task.Run(() => Sha256Of(file)).ContinueWith(t =>
            {
                Callable.From(() => OnDownloadHashed(file, t.IsCompletedSuccessfully ? t.Result : null, expectedSha256)).CallDeferred();
            });
        };
        KikoApi.Request(downloadUrl, new string[] { "User-Agent: Qlute-Updater" });
        Notify.Post("Downloading update...",
            ProgressGetter: () => KikoApi.GetDownloadedBytes(),
            Max: () => KikoApi.GetBodySize(),
            Type: NotificationIcons.NotificationType.Download);
    }

    private static string Sha256Of(string file)
    {
        using var stream = File.OpenRead(file);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private void OnDownloadHashed(string file, string actual, string expected)
    {
        isUpdating = false;
        if (actual == null || actual != expected)
        {
            GD.PrintErr($"[Kiko] Update checksum mismatch (expected {expected}, got {actual ?? "error"}), discarding download.");
            try { File.Delete(file); } catch { }
            return;
        }
        GD.Print("[Kiko] Update checksum verified.");
        UpdateReady = true;
        PostReadyNotification();
    }

    private static void PostReadyNotification()
    {
        Notify.Post("Update is ready to install!\nClick to install the update.",
            uri: $"updatefile:/{zipPath}",
            Type: NotificationIcons.NotificationType.Download);
    }
}
