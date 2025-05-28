using Godot;
using System;
using System.IO;

public partial class MusicPreview : TextureButton
{
    // Use animation for Preview Button
    private  Tween _focus_animation;
    private Color Idlecolour = new Color(1f, 1f, 1f, 0.5f); // Colour when idle
    private Color Focuscolour = new Color(1f, 1f, 1f, 0.8f); // Colour when focused
    private Color Highlight = new Color(0.6f, 0.6f, 1f, 1); // Colour when highlight/clicked
	public override void _Ready()
	{
		SelfModulate = Idlecolour;
		audioPath = GetMeta("preview_url").ToString();
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
	private AudioStreamPlayer AudioPreview { get; set; }
	private HttpRequest AudioPreviewData { get; set; }
    HttpRequest downloadRequest { get; set; }
	string audioPath {get; set; }
	string fileName {get; set; }
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

		AudioPlayer.Instance.Playing = false;
		AudioStream filestream = null;
		AudioPreview = new AudioStreamPlayer();
		GetTree().CurrentScene.AddChild(AudioPreview);
		AudioPreview.Name = "DownloadedMusicPreviewB";

		if (audioPath.EndsWith(".mp3"))
		{
			filestream = AudioPlayer.LoadMP3(Path.Combine(SettingsOperator.tempdir, $"musicpreview_{fileName}"));
		}
		else if (audioPath.EndsWith(".wav"))
		{
			filestream = AudioPlayer.LoadWAV(Path.Combine(SettingsOperator.tempdir, $"musicpreview_{fileName}"));
		}
		else if (audioPath.EndsWith(".ogg"))
		{
			filestream = AudioPlayer.LoadOGG(Path.Combine(SettingsOperator.tempdir, $"musicpreview_{fileName}"));
		}

		AudioPreview.Stream = filestream;
		AudioPreview.Play();
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
			if (AudioPreview != null && AudioPreview.Playing)
			{
				AudioPreview.Stop();
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
			if (AudioPreview != null && AudioPreview.Playing)
			{
				AudioPreview.Stop();
				AudioPlayer.Instance.Playing = false;
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
}
