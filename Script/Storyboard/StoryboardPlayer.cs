using Godot;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using QluteStoryboard;

/// <summary>
/// Plays an osu!-format storyboard (sprites, animations, samples and .ogv videos) behind the playfield.
/// Every game mode adds one above its Beatmap_Background with <see cref="AttachToGameplay"/>; the
/// Create editor drives one from its own clock for the Storyboard tab preview.
///
/// Storyboard space is 640x480 (854x480 when widescreen); it is scaled to the height of this control
/// and centred. The Overlay layer goes into <see cref="OverlayHost"/>, which gameplay puts above the playfield.
/// </summary>
public partial class StoryboardPlayer : Control
{
	public const string SettingKey = "storyboard";

	/// <summary>Current song time in ms.</summary>
	public Func<double> TimeSource { get; set; } = () => 0;
	/// <summary>Pass layer when true, Fail layer when false.</summary>
	public Func<bool> Passing { get; set; } = () => true;
	/// <summary>The beatmap background: its dim is copied, and it is hidden when the storyboard replaces it.</summary>
	public CanvasItem BackgroundToFollow { get; set; }
	/// <summary>Song speed for the video (1 = normal).</summary>
	public Func<float> Rate { get; set; } = () => 1f;
	/// <summary>Play storyboard sound samples (off in the editor while paused/scrubbing is handled by the time jump check).</summary>
	public bool PlaySamples { get; set; } = true;

	public StoryboardDoc Doc { get; private set; }
	public string Folder { get; private set; }
	public bool Ready { get; private set; }
	/// <summary>True when the storyboard draws the map's background image itself (osu! hides the normal one then).</summary>
	public bool ReplacesBackground { get; private set; }
	/// <summary>Holds the Overlay layer. Not in the tree until the owner adds it (gameplay puts it above the HUD-less playfield).</summary>
	public Control OverlayHost { get; private set; }

	private Control _frame, _overlayFrame;
	private Node2D _root, _overlayRoot;
	private readonly List<Sprite2D> _sprites = new();
	private readonly Dictionary<SbObject, Sprite2D> _spriteOf = new();
	private VideoStreamPlayer _video;
	private readonly List<AudioStreamPlayer> _samplePlayers = new();
	private int _nextSamplePlayer;
	private double _lastTime = double.NaN;
	private Task<Dictionary<string, Image>> _loading;
	private bool _errored;

	private static CanvasItemMaterial _additive;
	private static CanvasItemMaterial Additive => _additive ??= new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };

	// textures by lower-case relative path; null = missing
	private readonly Dictionary<string, Texture2D> _textures = new();
	private readonly Dictionary<string, AudioStream> _sounds = new();
	private Dictionary<string, string> _fileIndex = new();

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		_frame = new Control { MouseFilter = MouseFilterEnum.Ignore, ClipContents = true };
		AddChild(_frame);
		_root = new Node2D();
		_frame.AddChild(_root);

		OverlayHost = new Control { MouseFilter = MouseFilterEnum.Ignore };
		OverlayHost.SetAnchorsPreset(LayoutPreset.FullRect);
		_overlayFrame = new Control { MouseFilter = MouseFilterEnum.Ignore, ClipContents = true };
		OverlayHost.AddChild(_overlayFrame);
		_overlayRoot = new Node2D();
		_overlayFrame.AddChild(_overlayRoot);
	}

	public override void _ExitTree()
	{
		if (BackgroundToFollow != null && IsInstanceValid(BackgroundToFollow) && ReplacesBackground)
			BackgroundToFollow.Visible = true;
		if (OverlayHost != null && IsInstanceValid(OverlayHost) && OverlayHost.GetParent() == null)
			OverlayHost.QueueFree();
	}

	// =====================================================================
	// Setup
	// =====================================================================

	/// <summary>
	/// Adds a storyboard for the selected beatmap right above <paramref name="background"/> (and the overlay
	/// layer on top of <paramref name="root"/>). Returns null when the map has none or storyboards are off.
	/// </summary>
	public static StoryboardPlayer AttachToGameplay(Control root, CanvasItem background)
	{
		try
		{
			if (!Check.CheckBoolValue(SettingsOperator.GetSetting(SettingKey)?.ToString() ?? "true")) return null;
			var osu = SettingsOperator.SessionConfig.BeatmapURL;
			if (string.IsNullOrEmpty(osu) || !File.Exists(osu)) return null;
			var doc = StoryboardDoc.LoadForBeatmap(osu);
			if (doc.IsEmpty) return null;
			doc.CompileAll();

			var player = new StoryboardPlayer
			{
				TimeSource = () => SettingsOperator.Gameplaycfg.Time * 1000.0,
				Passing = () => HealthBar.Health * 2 >= HealthBar.MaxHealth,
				BackgroundToFollow = background,
				Rate = () => AudioPlayer.Instance?.PitchScale ?? 1f,
			};
			root.AddChild(player);
			player.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			root.MoveChild(player, background != null && background.GetParent() == root ? background.GetIndex() + 1 : 0);
			root.AddChild(player.OverlayHost);
			player.OverlayHost.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			player.Load(doc, Path.GetDirectoryName(osu), async: true);
			GD.Print($"[Storyboard] {doc.Objects.Count} objects, {doc.Samples.Count} samples, video={doc.VideoPath ?? "none"}");
			return player;
		}
		catch (Exception e)
		{
			GD.PrintErr("[Storyboard] couldn't load: " + e);
			return null;
		}
	}

	/// <summary>Show <paramref name="doc"/> (already compiled). Images load in the background when <paramref name="async"/>.</summary>
	public void Load(StoryboardDoc doc, string folder, bool async)
	{
		Doc = doc;
		if (Folder != folder)
		{
			Folder = folder;
			_textures.Clear();
			_sounds.Clear();
			_fileIndex = BuildFileIndex(folder);
		}
		Ready = false;
		_lastTime = double.NaN;
		var missing = doc.ImagePaths().Select(Key).Distinct().Where(k => !_textures.ContainsKey(k)).ToList();
		if (async && missing.Count > 0)
		{
			var index = _fileIndex;
			_loading = Task.Run(() =>
			{
				var images = new Dictionary<string, Image>();
				foreach (var key in missing)
					images[key] = index.TryGetValue(key, out var full) ? DecodeImage(full) : null;
				return images;
			});
		}
		else
		{
			foreach (var key in missing)
				_textures[key] = _fileIndex.TryGetValue(key, out var full) ? ToTexture(DecodeImage(full)) : null;
			// The editor reloads after every edit: keep the sprite nodes when only commands/positions changed.
			var layout = doc.Objects.Select(o => (o, o.Layer)).ToList();
			if (_layout != null && _layout.SequenceEqual(layout) && _sprites.Count == layout.Count)
			{
				UpdateReplacesBackground();
				Ready = true;
				return;
			}
			_layout = layout;
			BuildNodes();
		}
	}

	private List<(SbObject, SbLayer)> _layout;

	private void UpdateReplacesBackground()
	{
		var bgKey = Key(Doc.BackgroundPath);
		bool replaces = bgKey.Length > 0 && Doc.Objects.Any(o => Key(o.Path) == bgKey);
		if (ReplacesBackground && !replaces && BackgroundToFollow != null && IsInstanceValid(BackgroundToFollow))
			BackgroundToFollow.Visible = true;
		ReplacesBackground = replaces;
		QueueRedraw();
	}

	/// <summary>Forget a cached image (the editor calls this after copying a new file into the set).</summary>
	public void RefreshFiles()
	{
		_fileIndex = BuildFileIndex(Folder);
		foreach (var k in _textures.Where(kv => kv.Value == null).Select(kv => kv.Key).ToList()) _textures.Remove(k);
	}

	private static string Key(string relPath) => (relPath ?? "").Replace('\\', '/').TrimStart('/').ToLowerInvariant();

	private static Dictionary<string, string> BuildFileIndex(string folder)
	{
		var index = new Dictionary<string, string>();
		if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return index;
		try
		{
			foreach (var f in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
			{
				var rel = Path.GetRelativePath(folder, f);
				index.TryAdd(Key(rel), f);
			}
		}
		catch (Exception e) { GD.PrintErr("[Storyboard] " + e.Message); }
		return index;
	}

	private static Image DecodeImage(string path)
	{
		try
		{
			var img = Image.LoadFromFile(path);
			return img != null && !img.IsEmpty() ? img : null;
		}
		catch { return null; }
	}

	private static Texture2D ToTexture(Image img) => img == null ? null : ImageTexture.CreateFromImage(img);

	private void BuildNodes()
	{
		foreach (var s in _sprites) s.QueueFree();
		_sprites.Clear();
		if (_video != null) { _video.QueueFree(); _video = null; }
		_layout ??= Doc.Objects.Select(o => (o, o.Layer)).ToList();

		// draw order: Background, Fail, Pass, Foreground; Overlay on its own host
		var order = new[] { SbLayer.Background, SbLayer.Fail, SbLayer.Pass, SbLayer.Foreground, SbLayer.Overlay };
		var byObject = new Dictionary<SbObject, Sprite2D>();
		foreach (var layer in order)
			foreach (var o in Doc.Objects.Where(o => o.Layer == layer))
			{
				var s = new Sprite2D { Centered = false, Visible = false, TextureFilter = TextureFilterEnum.Linear };
				(layer == SbLayer.Overlay ? _overlayRoot : _root).AddChild(s);
				byObject[o] = s;
			}
		foreach (var o in Doc.Objects) _sprites.Add(byObject[o]);
		_spriteOf.Clear();
		foreach (var (o, spr) in byObject) _spriteOf[o] = spr;

		SetupVideo();

		UpdateReplacesBackground();
		Ready = true;
		QueueRedraw();
	}

	private void SetupVideo()
	{
		if (string.IsNullOrEmpty(Doc.VideoPath)) return;
		if (!_fileIndex.TryGetValue(Key(Doc.VideoPath), out var full)) return;
		if (!full.EndsWith(".ogv", StringComparison.OrdinalIgnoreCase))
		{
			GD.Print("[Storyboard] video skipped (only .ogv/Theora videos can play): " + Doc.VideoPath);
			return;
		}
		_video = new VideoStreamPlayer
		{
			Stream = new VideoStreamTheora { File = full },
			Expand = true,
			MouseFilter = MouseFilterEnum.Ignore,
			Bus = "Music",
			VolumeDb = -80, // the song is the audio; the video's own track is muted
		};
		_frame.AddChild(_video);
		_frame.MoveChild(_video, 0);
	}

	// =====================================================================
	// Playback
	// =====================================================================

	/// <summary>Scale from storyboard units to pixels, and the left edge of the 640-wide area.</summary>
	public float PixelScale => Size.Y / 480f;
	private float FrameWidth => (Doc?.Widescreen ?? true ? 854f : 640f) * PixelScale;

	/// <summary>Storyboard position (640x480 space) to this control's local coordinates.</summary>
	public Vector2 SbToLocal(Vector2 sb)
	{
		float s = PixelScale;
		float left = (Size.X - FrameWidth) / 2f + ((Doc?.Widescreen ?? true) ? 107f * s : 0f);
		return new Vector2(left + sb.X * s, sb.Y * s);
	}

	public Vector2 LocalToSb(Vector2 local)
	{
		float s = Mathf.Max(0.0001f, PixelScale);
		float left = (Size.X - FrameWidth) / 2f + ((Doc?.Widescreen ?? true) ? 107f * s : 0f);
		return new Vector2((local.X - left) / s, local.Y / s);
	}

	public override void _Draw()
	{
		if (Ready && ReplacesBackground) DrawRect(new Rect2(Vector2.Zero, Size), Colors.Black);
	}

	public override void _Process(double delta)
	{
		if (_errored || Doc == null) return;
		try
		{
			if (!Ready && _loading != null && _loading.IsCompleted)
			{
				if (_loading.Status == TaskStatus.RanToCompletion)
					foreach (var (k, img) in _loading.Result) _textures[k] = ToTexture(img);
				_loading = null;
				BuildNodes();
			}
			if (!Ready) return;
			UpdateFrame();
		}
		catch (Exception e)
		{
			_errored = true;
			GD.PrintErr("[Storyboard] stopped: " + e);
		}
	}

	private void UpdateFrame()
	{
		if (BackgroundToFollow != null && IsInstanceValid(BackgroundToFollow))
		{
			var m = BackgroundToFollow.SelfModulate;
			Modulate = new Color(m.R, m.G, m.B, 1f);
			if (OverlayHost != null) OverlayHost.Modulate = Modulate;
			if (ReplacesBackground && BackgroundToFollow.Visible) BackgroundToFollow.Visible = false;
		}

		float s = PixelScale;
		var frameRect = new Rect2((Size.X - FrameWidth) / 2f, 0, FrameWidth, Size.Y);
		foreach (var f in new[] { _frame, _overlayFrame })
		{
			f.Position = frameRect.Position;
			f.Size = frameRect.Size;
		}
		var rootPos = new Vector2(Doc.Widescreen ? 107f * s : 0f, 0);
		_root.Position = rootPos;
		_root.Scale = new Vector2(s, s);
		_overlayRoot.Position = rootPos;
		_overlayRoot.Scale = new Vector2(s, s);

		double t = TimeSource();
		bool passing = Passing();
		for (int i = 0; i < Doc.Objects.Count && i < _sprites.Count; i++)
			UpdateSprite(Doc.Objects[i], _sprites[i], t, passing);

		UpdateVideo(t);
		UpdateSamples(t, passing);
		_lastTime = t;
	}

	private void UpdateSprite(SbObject o, Sprite2D spr, double t, bool passing)
	{
		bool layerOn = o.Layer switch { SbLayer.Fail => !passing, SbLayer.Pass => passing, _ => true };
		if (!layerOn || !o.HasCommands || t < o.StartTime || t > o.EndTime)
		{
			if (spr.Visible) spr.Visible = false;
			return;
		}
		var st = o.StateAt(t);
		if (st.Alpha <= 0.002f || st.ScaleX == 0 || st.ScaleY == 0)
		{
			if (spr.Visible) spr.Visible = false;
			return;
		}
		_textures.TryGetValue(Key(o.FramePath(o.FrameAt(t))), out var tex);
		if (tex == null)
		{
			if (spr.Visible) spr.Visible = false;
			return;
		}

		spr.Texture = tex;
		var size = tex.GetSize();
		var (fx, fy) = SbObject.OriginFraction(o.Origin);
		if (st.FlipH) fx = 1 - fx;
		if (st.FlipV) fy = 1 - fy;
		spr.FlipH = st.FlipH;
		spr.FlipV = st.FlipV;
		spr.Offset = new Vector2(-fx * size.X, -fy * size.Y);
		spr.Position = new Vector2(st.X, st.Y);
		spr.Rotation = st.Rotation;
		spr.Scale = new Vector2(st.ScaleX, st.ScaleY);
		spr.SelfModulate = new Color(st.R, st.G, st.B, Mathf.Clamp(st.Alpha, 0, 1));
		var mat = st.Additive ? Additive : null;
		if (spr.Material != mat) spr.Material = mat;
		if (!spr.Visible) spr.Visible = true;
	}

	private void UpdateVideo(double t)
	{
		if (_video == null) return;
		double videoTime = (t - Doc.VideoOffset) / 1000.0;
		var len = _video.GetStreamLength();
		bool shouldPlay = videoTime >= 0 && (len <= 0 || videoTime < len);
		_video.Visible = shouldPlay;
		if (!shouldPlay)
		{
			if (_video.IsPlaying()) _video.Stop();
			return;
		}
		if (!_video.IsPlaying())
		{
			_video.Play();
			_video.StreamPosition = videoTime;
		}
		_video.SpeedScale = Math.Max(0.01f, Rate());
		bool timeMoving = !double.IsNaN(_lastTime) && t != _lastTime;
		_video.Paused = !timeMoving;
		if (Math.Abs(_video.StreamPosition - videoTime) > 0.15) _video.StreamPosition = videoTime;
	}

	private void UpdateSamples(double t, bool passing)
	{
		if (!PlaySamples || Doc.Samples.Count == 0 || double.IsNaN(_lastTime)) return;
		// only while time moves forward normally (not on seeks)
		if (t <= _lastTime || t - _lastTime > 500) return;
		foreach (var smp in Doc.Samples)
		{
			if (smp.Time <= _lastTime || smp.Time > t) continue;
			if (smp.Layer == SbLayer.Fail && passing || smp.Layer == SbLayer.Pass && !passing) continue;
			PlaySample(smp);
		}
	}

	private void PlaySample(SbSample smp)
	{
		var key = Key(smp.Path);
		if (!_sounds.TryGetValue(key, out var stream))
		{
			stream = null;
			if (_fileIndex.TryGetValue(key, out var full))
			{
				try { stream = AudioPlayer.AutoDetectFormat(full); }
				catch (Exception e) { GD.PrintErr("[Storyboard] sample " + smp.Path + ": " + e.Message); }
			}
			_sounds[key] = stream;
		}
		if (stream == null) return;
		if (_samplePlayers.Count < 8)
		{
			var p = new AudioStreamPlayer { Bus = "Effects" };
			AddChild(p);
			_samplePlayers.Add(p);
		}
		var player = _samplePlayers[_nextSamplePlayer++ % _samplePlayers.Count];
		player.Stream = stream;
		player.PitchScale = Math.Max(0.01f, Rate());
		float db = Sample.VolumeDb + Mathf.LinearToDb(Mathf.Clamp(smp.Volume, 1, 100) / 100f);
		player.VolumeDb = float.IsFinite(db) ? db : 0;
		player.Play();
	}

	/// <summary>The sprite drawing <paramref name="o"/> (null if it isn't built yet).</summary>
	public Sprite2D SpriteOf(SbObject o)
	{
		if (Doc == null || !Ready || o == null) return null;
		return _spriteOf.TryGetValue(o, out var spr) && IsInstanceValid(spr) ? spr : null;
	}

	/// <summary>Corners of <paramref name="o"/> in this control's coordinates (null when not drawn right now).</summary>
	public Vector2[] CornersOf(SbObject o)
	{
		var spr = SpriteOf(o);
		if (spr == null || !spr.Visible || spr.Texture == null) return null;
		var r = spr.GetRect();
		var xf = GetGlobalTransformWithCanvas().AffineInverse() * spr.GetGlobalTransformWithCanvas();
		return new[] { xf * r.Position, xf * new Vector2(r.End.X, r.Position.Y), xf * r.End, xf * new Vector2(r.Position.X, r.End.Y) };
	}
}
