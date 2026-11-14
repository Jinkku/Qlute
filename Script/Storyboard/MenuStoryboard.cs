using Godot;
using System;
using System.IO;
using System.Threading.Tasks;
using QluteStoryboard;

/// <summary>
/// The selected map's storyboard behind the home screen / song select, synced to the menu music.
/// BeatmapBackground adds it above itself when its ShowStoryboard is on. Loading waits until the
/// selection has settled and parses off the main thread, so scrolling through maps stays smooth.
/// </summary>
public partial class MenuStoryboard : Control
{
	private const double SettleSeconds = 0.35;

	public CanvasItem Background { get; set; }

	private StoryboardPlayer _player;
	private string _shownUrl;      // map whose storyboard is shown (or was found empty)
	private string _pendingUrl;    // selection waiting to settle
	private double _pendingFor;
	private Task<StoryboardDoc> _parsing;
	private string _parsingUrl;
	private bool _enabled = true;
	private int _settingsVersion = -1;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		_player = new StoryboardPlayer
		{
			TimeSource = MusicTimeMs,
			BackgroundToFollow = Background,
			PlaySamples = false, // the menu plays a preview, not the map
		};
		AddChild(_player);
		_player.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		// The overlay layer stays behind the menu UI too.
		AddChild(_player.OverlayHost);
		_player.OverlayHost.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
	}

	/// <summary>Song position of the menu music (ms), between audio mixes too.</summary>
	private static double MusicTimeMs()
	{
		var audio = AudioPlayer.Instance;
		if (audio == null || audio.Stream == null) return 0;
		double pos = audio.GetPlaybackPosition();
		if (audio.Playing && !audio.StreamPaused) pos += AudioServer.GetTimeSinceLastMix() - AudioServer.GetOutputLatency();
		return pos * 1000.0;
	}

	public override void _Process(double delta)
	{
		if (_settingsVersion != SettingsOperator.SettingsVersion)
		{
			_settingsVersion = SettingsOperator.SettingsVersion;
			bool on = Check.CheckBoolValue(SettingsOperator.GetSetting(StoryboardPlayer.SettingKey)?.ToString() ?? "true");
			if (on != _enabled)
			{
				_enabled = on;
				if (!on) { _player.Unload(); _shownUrl = null; }
			}
		}
		if (!_enabled) return;

		var url = SettingsOperator.SessionConfig.BeatmapURL;
		if (url != _shownUrl && url != _parsingUrl)
		{
			if (url != _pendingUrl)
			{
				_pendingUrl = url;
				_pendingFor = 0;
				// the old map's storyboard goes right away so the new background shows while this one loads
				if (_shownUrl != null) { _player.Unload(); _shownUrl = null; }
			}
			_pendingFor += delta;
			if (_pendingFor >= SettleSeconds) StartParse(url);
		}

		if (_parsing != null && _parsing.IsCompleted)
		{
			var task = _parsing;
			var parsedUrl = _parsingUrl;
			_parsing = null;
			_parsingUrl = null;
			_shownUrl = parsedUrl;
			var doc = task.Status == TaskStatus.RanToCompletion ? task.Result : null;
			if (doc == null || doc.IsEmpty || parsedUrl != SettingsOperator.SessionConfig.BeatmapURL) _player.Unload();
			else _player.Load(doc, Path.GetDirectoryName(parsedUrl), async: true);
		}
	}

	private void StartParse(string url)
	{
		_pendingUrl = null;
		if (_parsing != null) return; // one at a time; the newest selection is picked up when it finishes
		_parsingUrl = url;
		_parsing = Task.Run(() =>
		{
			try
			{
				if (string.IsNullOrEmpty(url) || !File.Exists(url)) return null;
				var doc = StoryboardDoc.LoadForBeatmap(url);
				if (doc.IsEmpty) return doc;
				doc.CompileAll();
				return doc;
			}
			catch (Exception e)
			{
				GD.PrintErr("[Storyboard] menu: " + e.Message);
				return null;
			}
		});
	}
}
