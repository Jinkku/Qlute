using Godot;
using System;
using System.IO;
using System.Threading.Tasks;

/// <summary>
/// Plays a set's preview clip on AudioPlayer.BrowsePreview (the same player the Browse cards use, so only one
/// preview plays at a time), pausing the menu music meanwhile.
/// </summary>
public static class PreviewPlayer
{
	private static readonly System.Net.Http.HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };
	private static int _loading = -1;

	public static bool IsPlaying(int setId) => AudioPlayer.PreviewID == setId && AudioPlayer.BrowsePreview?.Playing == true;
	public static bool IsLoading(int setId) => _loading == setId;

	public static float Progress(int setId)
	{
		if (!IsPlaying(setId)) return 0;
		var len = AudioPlayer.BrowsePreview.Stream?.GetLength() ?? 0;
		return len > 0 ? (float)(AudioPlayer.BrowsePreview.GetPlaybackPosition() / len) : 0;
	}

	public static void Stop()
	{
		if (AudioPlayer.BrowsePreview?.Playing == true) AudioPlayer.BrowsePreview.Stop();
		if (AudioPlayer.Instance?.Stream != null) AudioPlayer.Instance.StreamPaused = false;
	}

	public static async void Toggle(string url, int setId)
	{
		if (IsPlaying(setId)) { Stop(); return; }
		if (string.IsNullOrEmpty(url) || _loading == setId) return;
		_loading = setId;
		try
		{
			var bytes = await Http.GetByteArrayAsync(url);
			if (_loading != setId) return; // another preview was asked for meanwhile
			var file = Path.Combine(SettingsOperator.tempdir, $"musicpreview_{setId}{Path.GetExtension(new Uri(url).AbsolutePath)}");
			await File.WriteAllBytesAsync(file, bytes);
			AudioStream stream = AudioPlayer.GetAudioFormat(file) switch
			{
				AudioFormat.WAV => AudioPlayer.LoadWAV(file),
				AudioFormat.OGG => AudioPlayer.LoadOGG(file),
				AudioFormat.MP3 => AudioPlayer.LoadMP3(file),
				_ => null,
			};
			if (stream == null)
			{
				Notify.Post("There is no preview for this beatmap.");
				return;
			}
			AudioPlayer.Instance.StreamPaused = true;
			AudioPlayer.PreviewID = setId;
			AudioPlayer.BrowsePreview.Stream = stream;
			AudioPlayer.BrowsePreview.Play();
		}
		catch (Exception e)
		{
			GD.PrintErr($"[PreviewPlayer] {e.Message}");
			Notify.Post("Couldn't load the preview.");
		}
		finally
		{
			if (_loading == setId) _loading = -1;
		}
	}
}
