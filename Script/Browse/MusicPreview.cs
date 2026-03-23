using Godot;
using System;
using System.IO;

public partial class MusicPreview : TextureButton
{
	// Use animation for Preview Button
	private Tween _focus_animation;
	private Color Idlecolour = new Color(1f, 1f, 1f, 0.5f); // Colour when idle
	private Color Focuscolour = new Color(1f, 1f, 1f, 0.8f); // Colour when focused
	private Color Highlight = new Color(0.6f, 0.6f, 1f, 1); // Colour when highlight/clicked
	private int PreviewID { get; set; }
	private TextureProgressBar MusicProgress;
	public override void _Ready()
	{
		var Root = GetNode<CardFunctions>("../../");
		if (Root.BeatmapID != -1)
		{
			PreviewID = Root.BeatmapID;
		}
		SelfModulate = Idlecolour;
		if (audioPath == null)
		{
			SetProcess(false);
		}
		MusicProgress = GetNode<TextureProgressBar>("SongProgress");
	}
	private void AnimationButton(Color colour)
	{
		if (_focus_animation != null)
		{
			_focus_animation.Kill();
		}
		_focus_animation = CreateTween();
		_focus_animation.TweenProperty(this, "self_modulate", colour, 0.2f)
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.Out);
		_focus_animation.Play();
	}
	private HttpRequest AudioPreviewData { get; set; }
	HttpRequest downloadRequest { get; set; }
	public string audioPath { get; set; }
	string fileName { get; set; }
	private bool Loaded { get; set; } = false;
	private void _PlayPreview(long result, long responseCode, string[] headers, byte[] body)
	{
		// Remove any existing preview nodes
		Loaded = true;
		var existingPreviewB = GetTree().CurrentScene.GetNodeOrNull("DownloadedMusicPreviewB");
		if (existingPreviewB != null)
		{
			existingPreviewB.QueueFree();
		}
		// Process audio file
		var musicfile = Path.Combine(SettingsOperator.tempdir, $"musicpreview_{fileName}");
		AudioPlayer.Instance.StreamPaused = true;
		AudioStream filestream = null;
		AudioFormat? format = AudioPlayer.GetAudioFormat(musicfile);
		GD.Print($"Format: {format.ToString()}");
		if (format == null)
		{
			Notify.Post("There is no preview for this beatmap.");
			return;
		}

		// continue with the detected format
		switch (format)
		{
			case AudioFormat.WAV: filestream = AudioPlayer.LoadWAV(musicfile); break;
			case AudioFormat.OGG: filestream = AudioPlayer.LoadOGG(musicfile); break;
			case AudioFormat.MP3: filestream = AudioPlayer.LoadMP3(musicfile); break;
		}
		AudioPlayer.PreviewID = PreviewID;
		AudioPlayer.BrowsePreview.Stream = filestream;
		AudioPlayer.BrowsePreview.Play();
		GD.Print($"Now PLAY!"); 
	}
	public override void _ExitTree()
	{
		AudioPlayer.BrowsePreview.Stop();
		if (AudioPlayer.Instance.Stream != null)
		{
			GD.Print("[Qlute] Should continue playing...");
			AudioPlayer.Instance.StreamPaused = false;
		}
	}
	private void _pressed()
	{
		if (string.IsNullOrEmpty(audioPath))
		{
			GD.PrintErr("No audio path set for Music Preview.");
			ButtonPressed = false;
			return;
		}
		else if (ButtonPressed)
		{
			if (AudioPlayer.BrowsePreview != null && AudioPlayer.BrowsePreview.Playing)
			{
				AudioPlayer.BrowsePreview.Stop();
				return;
			}
			else
			{
				var existingPreviewA = GetTree().CurrentScene.GetNodeOrNull("DownloadMusicPreviewA");

				if (existingPreviewA != null)
				{
					existingPreviewA.QueueFree();
				}

				downloadRequest = new HttpRequest();
				downloadRequest.Timeout = 60;
				GetTree().CurrentScene.AddChild(downloadRequest);
				downloadRequest.Name = "DownloadMusicPreviewA";
				downloadRequest.Connect("request_completed", new Callable(this, nameof(_PlayPreview)));
				fileName = Path.GetFileName(audioPath);
				downloadRequest.DownloadFile = Path.Combine(SettingsOperator.tempdir, $"musicpreview_{fileName}");
				downloadRequest.Request(audioPath, null, Godot.HttpClient.Method.Get);
				return;
			}
		}
		else
		{
			if (AudioPlayer.BrowsePreview != null && AudioPlayer.BrowsePreview.Playing)
			{
				AudioPlayer.BrowsePreview.Stop();
				AudioPlayer.Instance.StreamPaused = false;
			}
		}
		AnimationButton(Focuscolour);
	}
	private void _focus()
	{
		AnimationButton(Focuscolour);
	}
	private void _unfocus()
	{
		AnimationButton(Idlecolour);
	}
	private void _highlight()
	{
		AnimationButton(Highlight);
	}

	public override void _Process(double delta)
	{
		ButtonPressed = AudioPlayer.PreviewID == PreviewID && AudioPlayer.BrowsePreview.Playing;
		MusicProgress.Visible = AudioPlayer.PreviewID == PreviewID && AudioPlayer.BrowsePreview.Playing;
		if (AudioPlayer.BrowsePreview.Playing)
		{
			MusicProgress.MaxValue = AudioPlayer.BrowsePreview.Stream?.GetLength() ?? 0;
			MusicProgress.Value = AudioPlayer.BrowsePreview.GetPlaybackPosition();
		}
    }
}
