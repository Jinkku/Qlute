using Godot;
using System;
using System.IO;

public partial class MusicPreview : TextureButton
{
	private Tween? _focus_animation;
	private readonly Color Idlecolour   = new(1f, 1f, 1f, 0.5f);
	private readonly Color Focuscolour  = new(1f, 1f, 1f, 0.8f);
	private readonly Color Highlight    = new(0.6f, 0.6f, 1f, 1f);

	private int PreviewID { get; set; }
	private TextureProgressBar MusicProgress { get; set; } = null!;

	public string? audioPath { get; set; }
	private string? fileName { get; set; }
	private bool Loaded { get; set; } = false;

	private HttpRequest? downloadRequest { get; set; }

	public override void _Ready()
	{
		var root = GetNode<CardFunctions>("../../");
		if (root.BeatmapID != -1)
			PreviewID = root.BeatmapID;

		SelfModulate = Idlecolour;

		if (audioPath == null)
			SetProcess(false);

		MusicProgress = GetNode<TextureProgressBar>("SongProgress");
	}

	private void AnimationButton(Color colour)
	{
		_focus_animation?.Kill();
		_focus_animation = CreateTween();
		_focus_animation.TweenProperty(this, "self_modulate", colour, 0.2f)
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.Out);
		_focus_animation.Play();
	}

	private void _PlayPreview(long result, long responseCode, string[] headers, byte[] body)
	{
		Loaded = true;

		// Clean up any stale preview node
		GetTree().CurrentScene.GetNodeOrNull("DownloadedMusicPreviewB")?.QueueFree();

		if (string.IsNullOrEmpty(fileName))
		{
			GD.PrintErr("[MusicPreview] fileName is null after download — cannot play.");
			return;
		}

		var musicFile = Path.Combine(SettingsOperator.tempdir, $"musicpreview_{fileName}");
		AudioPlayer.Instance.StreamPaused = true;

		AudioFormat? format = AudioPlayer.GetAudioFormat(musicFile);
		if (format == null)
		{
			Notify.Post("There is no preview for this beatmap.");
			return;
		}

		AudioStream? fileStream = format switch
		{
			AudioFormat.WAV => AudioPlayer.LoadWAV(musicFile),
			AudioFormat.OGG => AudioPlayer.LoadOGG(musicFile),
			AudioFormat.MP3 => AudioPlayer.LoadMP3(musicFile),
			_               => null
		};

		if (fileStream == null)
		{
			GD.PrintErr($"[MusicPreview] Unrecognised format: {format}");
			return;
		}

		AudioPlayer.PreviewID = PreviewID;
		AudioPlayer.BrowsePreview.Stream = fileStream;
		AudioPlayer.BrowsePreview.Play();
		GD.Print("[MusicPreview] Now PLAY!");
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

		if (ButtonPressed)
		{
			if (AudioPlayer.BrowsePreview != null && AudioPlayer.BrowsePreview.Playing)
			{
				AudioPlayer.BrowsePreview.Stop();
				return;
			}

			// Clean up any previous download node
			GetTree().CurrentScene.GetNodeOrNull("DownloadMusicPreviewA")?.QueueFree();

			downloadRequest = new HttpRequest { Timeout = 60 };
			GetTree().CurrentScene.AddChild(downloadRequest);
			downloadRequest.Name = "DownloadMusicPreviewA";
			downloadRequest.Connect("request_completed", new Callable(this, nameof(_PlayPreview)));

			fileName = Path.GetFileName(audioPath);
			downloadRequest.DownloadFile = Path.Combine(SettingsOperator.tempdir, $"musicpreview_{fileName}");
			downloadRequest.Request(audioPath, null, Godot.HttpClient.Method.Get);
		}
		else
		{
			if (AudioPlayer.BrowsePreview != null && AudioPlayer.BrowsePreview.Playing)
			{
				AudioPlayer.BrowsePreview.Stop();
				AudioPlayer.Instance.StreamPaused = false;
			}

			AnimationButton(Focuscolour);
		}
	}

	private void _focus()   => AnimationButton(Focuscolour);
	private void _unfocus() => AnimationButton(Idlecolour);
	private void _highlight() => AnimationButton(Highlight);

	public override void _Process(double delta)
	{
		bool isThisPreview = AudioPlayer.PreviewID == PreviewID && AudioPlayer.BrowsePreview.Playing;
		ButtonPressed = isThisPreview;
		MusicProgress.Visible = isThisPreview;

		if (isThisPreview)
		{
			MusicProgress.MaxValue = AudioPlayer.BrowsePreview.Stream?.GetLength() ?? 0;
			MusicProgress.Value    = AudioPlayer.BrowsePreview.GetPlaybackPosition();
		}
	}
}