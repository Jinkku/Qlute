using Godot;
using System;
using System.IO;
using System.IO.Compression;

public partial class UpdateScreen : Control
{
	private HttpRequest Request { get; set; }
	private ProgressBar ProgressBar { get; set; }
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

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		GetTree().Paused = true;
		GetTree().Root.GetNode<Control>("TopPanelOnTop/InfoBar").QueueFree();
		Request = GetNode<HttpRequest>("Updater");
		ProgressBar = GetNode<ProgressBar>("ProgressBar");
		// If the update file directory exists, copy its contents (not the folder itself) to Xeos
		Request.DownloadFile = Path.Combine(Path.GetTempPath(), platformZip);
		Request.Request($"https://github.com/Jinkkuu/Qlute/releases/latest/download/{platformZip}");
		Request.RequestCompleted += OnRequestCompleted;
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		if (Request.GetBodySize() != -1)
		{
			ProgressBar.Value = Request.GetDownloadedBytes();
			ProgressBar.MaxValue = Request.GetBodySize();			
		}
	}

	private void OnRequestCompleted(long result, long responseCode, string[] headers, byte[] body)
	{
		GD.Print("Download completed!");
		// Extract the downloaded zip file
		string zipPath = Request.DownloadFile;
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
}
