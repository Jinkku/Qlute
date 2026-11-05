using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CreateEditor;
using IOPath = System.IO.Path;
using IOFile = System.IO.File;
using IODirectory = System.IO.Directory;

/// <summary>
/// Mania (4K) beatmap editor. Tabs: Info (metadata, background, audio), Compose (notes),
/// Timing (BPM sections) and Verify (checks). Saves to the game's .osu format in the beatmaps folder.
/// </summary>
public partial class Create : Control
{
	private const string HitSoundPath = "res://SelectableSkins/Slia/Sounds/soft-hitnormal.wav";
	private static readonly int[] Divisors = { 1, 2, 3, 4, 6, 8, 12, 16 };
	private static readonly float[] Rates = { 0.25f, 0.5f, 0.75f, 1f };
	private static readonly string[] AudioExtensions = { ".mp3", ".ogg", ".wav" };
	private static readonly string[] ImageExtensions = { ".png", ".jpg", ".jpeg", ".webp", ".bmp" };

	// ---------- state ----------
	public ManiaBeatmap Map { get; private set; }
	public HashSet<EditorNote> Selection { get; } = new();
	public EditorTool Tool { get; private set; } = EditorTool.Place;
	public int Divisor => Divisors[_divisorIndex];
	public float Zoom { get; private set; } = 1f;
	public double CurrentMs { get; private set; }
	public double SongLengthMs => EditorPlayer.Stream != null
		? EditorPlayer.Stream.GetLength() * 1000.0
		: Math.Max(60000, (Map?.Notes.Count > 0 ? Map.Notes.Max(n => Math.Max(n.Time, n.EndTime)) : 0) + 10000);

	/// <summary>Path of the .osu being edited, null until first save.</summary>
	private string FilePath { get; set; }
	/// <summary>Beatmap set folder, null until first save.</summary>
	private string SetFolder { get; set; }
	/// <summary>Audio/background picked from outside the set folder, copied in on save.</summary>
	private string PendingAudio { get; set; }
	private string PendingBackground { get; set; }
	private bool Dirty { get; set; }

	private int _divisorIndex = 3;
	private bool _playing;
	private double _smoothMs;
	private double _lastHitsoundMs;
	private bool _suppressSeek;
	private bool _suppressUi;
	private int _tab = 1;

	private readonly List<Snapshot> _undo = new();
	private readonly List<Snapshot> _redo = new();
	private string _lastUndoKey;
	private List<EditorNote> _clipboard = new();
	private readonly List<ulong> _taps = new();
	private double _tappedBpm;

	private class Snapshot
	{
		public List<EditorNote> Notes;
		public List<EditorTimingPoint> Timing;
		public int PreviewTime;
	}

	// ---------- nodes ----------
	private AudioStreamPlayer EditorPlayer;
	private TextureRect EditorBackground;
	private HSlider Seeker;
	private Label TimeLabel;
	private Control InfoN, ComposeN, TimingN, VerifyN;
	private ManiaEditorField Field;
	private Label SelectedPos, NoteCount, SelectionCount, ZoomLabel;
	private OptionButton DivisorOption, RateOption;
	private CheckBox HitsoundsToggle;
	private Button SelectBtn, PlaceBtn, RemoveBtn;
	private ButtonFade PlayPauseBtn;

	private LineEdit TitleEdit, ArtistEdit, DifficultyEdit, MapperEdit, SourceEdit, TagsEdit;
	private HSlider OdSlider, HpSlider;
	private LineEdit OdEdit, HpEdit;
	private TextureRect BackgroundPreview;
	private Label BackgroundFile, AudioFile, PreviewValue;

	private ItemList TimingList;
	private SpinBox OffsetSpin, BpmSpin, MeterSpin;
	private CheckBox KiaiCheck;
	private Label TapResult;

	private ItemList IssuesList;
	private Label VerifySummary;
	private readonly List<double> _issueTimes = new();

	private const string InfoGrid = "Info/Panel/Scroll/GridContainer/";
	private const string TimingBox = "Timing/Panel/Box/";

	public override void _Ready()
	{
		SettingsOperator.CreateSelectingBeatmap = false;
		EditorSongInfo.ReturnToEditor = false;
		if (AudioPlayer.Instance.Stream != null)
			AudioPlayer.Instance.StreamPaused = true;

		EditorPlayer = GetNode<AudioStreamPlayer>("EditorPlayer");
		EditorBackground = GetNode<TextureRect>("EditorBackground");
		Seeker = GetNode<HSlider>("ControlPanel/Box/PlayerControl/Seek");
		TimeLabel = GetNode<Label>("ControlPanel/Box/TimeBox/Time");
		InfoN = GetNode<Control>("Info");
		ComposeN = GetNode<Control>("Compose");
		TimingN = GetNode<Control>("Timing");
		VerifyN = GetNode<Control>("Verify");

		// Compose
		Field = new ManiaEditorField { Editor = this };
		var ground = GetNode<Control>("Compose/Ground");
		ground.AddChild(Field);
		Field.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		const string ctx = "Compose/Info/ContextSections/";
		SelectedPos = GetNode<Label>(ctx + "SelectedPos");
		NoteCount = GetNode<Label>(ctx + "NoteCount");
		SelectionCount = GetNode<Label>(ctx + "SelectionCount");
		ZoomLabel = GetNode<Label>(ctx + "Zoom");
		DivisorOption = GetNode<OptionButton>(ctx + "Divisor");
		RateOption = GetNode<OptionButton>(ctx + "Rate");
		HitsoundsToggle = GetNode<CheckBox>(ctx + "Hitsounds");
		DivisorOption.ItemSelected += i => { _divisorIndex = (int)i; };
		RateOption.ItemSelected += i => SetRate(Rates[(int)i]);
		const string tools = "Compose/Creativity/ContextSections/";
		SelectBtn = GetNode<Button>(tools + "Select");
		PlaceBtn = GetNode<Button>(tools + "Place");
		RemoveBtn = GetNode<Button>(tools + "Remove");
		SelectBtn.Pressed += () => SetTool(EditorTool.Select);
		PlaceBtn.Pressed += () => SetTool(EditorTool.Place);
		RemoveBtn.Pressed += () => SetTool(EditorTool.Remove);

		// Transport
		PlayPauseBtn = GetNode<ButtonFade>("ControlPanel/Box/Player/Columns/PlayPause");
		var back = GetNode<ButtonFade>("ControlPanel/Box/Player/Columns/SkipBack");
		var fwd = GetNode<ButtonFade>("ControlPanel/Box/Player/Columns/SkipForward");
		back.StringText = "|<";
		fwd.StringText = ">|";
		PlayPauseBtn.StringText = ">";
		PlayPauseBtn.Pressed += TogglePlay;
		back.Pressed += () => Seek(FirstObjectTime() > CurrentMs - 1 ? 0 : FirstObjectTime());
		fwd.Pressed += () => Seek(LastObjectTime() < CurrentMs + 1 ? SongLengthMs : LastObjectTime());
		foreach (var b in new Button[] { PlayPauseBtn, back, fwd }) b.FocusMode = FocusModeEnum.None;
		GetNode<Button>("ControlPanel/Box/Next").Pressed += TestPlay;

		// Info
		TitleEdit = GetNode<LineEdit>(InfoGrid + "Title/Value");
		ArtistEdit = GetNode<LineEdit>(InfoGrid + "Artist/Value");
		DifficultyEdit = GetNode<LineEdit>(InfoGrid + "Difficulty/Value");
		MapperEdit = GetNode<LineEdit>(InfoGrid + "Mapper/Value");
		SourceEdit = GetNode<LineEdit>(InfoGrid + "Source/Value");
		TagsEdit = GetNode<LineEdit>(InfoGrid + "Tags/Value");
		OdSlider = GetNode<HSlider>(InfoGrid + "OD/Box/Value");
		OdEdit = GetNode<LineEdit>(InfoGrid + "OD/Box/ValueEdit");
		HpSlider = GetNode<HSlider>(InfoGrid + "HP/Box/Value");
		HpEdit = GetNode<LineEdit>(InfoGrid + "HP/Box/ValueEdit");
		BackgroundPreview = GetNode<TextureRect>(InfoGrid + "Background/Preview");
		BackgroundFile = GetNode<Label>(InfoGrid + "Background/File");
		AudioFile = GetNode<Label>(InfoGrid + "Audio/File");
		PreviewValue = GetNode<Label>(InfoGrid + "Preview/Value");
		BindText(TitleEdit, v => Map.Title = v);
		BindText(ArtistEdit, v => Map.Artist = v);
		BindText(DifficultyEdit, v => Map.Version = v);
		BindText(MapperEdit, v => Map.Creator = v);
		BindText(SourceEdit, v => Map.Source = v);
		BindText(TagsEdit, v => Map.Tags = v);
		BindSlider(OdSlider, OdEdit, v => Map.OverallDifficulty = v);
		BindSlider(HpSlider, HpEdit, v => Map.HPDrainRate = v);
		GetNode<Button>(InfoGrid + "Background/Pick").Pressed += PickBackground;
		GetNode<Button>(InfoGrid + "Audio/Pick").Pressed += PickAudio;
		GetNode<Button>(InfoGrid + "Preview/Box/SetCurrent").Pressed += () =>
		{
			PushUndo();
			Map.PreviewTime = (int)Math.Round(CurrentMs);
			MarkDirty();
			RefreshInfo();
		};
		GetNode<Button>(InfoGrid + "Preview/Box/Clear").Pressed += () =>
		{
			PushUndo();
			Map.PreviewTime = -1;
			MarkDirty();
			RefreshInfo();
		};

		// Timing
		TimingList = GetNode<ItemList>(TimingBox + "ListBox/Points");
		OffsetSpin = GetNode<SpinBox>(TimingBox + "Edit/Offset");
		BpmSpin = GetNode<SpinBox>(TimingBox + "Edit/Bpm");
		MeterSpin = GetNode<SpinBox>(TimingBox + "Edit/Meter");
		KiaiCheck = GetNode<CheckBox>(TimingBox + "Edit/Kiai");
		TapResult = GetNode<Label>(TimingBox + "Edit/TapResult");
		TimingList.ItemSelected += _ => LoadTimingEditor();
		TimingList.ItemActivated += i => { if (i < Map.TimingPoints.Count) Seek(Map.TimingPoints[(int)i].Offset); };
		GetNode<Button>(TimingBox + "ListBox/Buttons/Add").Pressed += AddTimingPoint;
		GetNode<Button>(TimingBox + "ListBox/Buttons/Remove").Pressed += RemoveTimingPoint;
		GetNode<Button>(TimingBox + "Edit/UseCurrent").Pressed += () => OffsetSpin.Value = Math.Round(CurrentMs);
		GetNode<Button>(TimingBox + "Edit/Tap").Pressed += Tap;
		GetNode<Button>(TimingBox + "Edit/ApplyTap").Pressed += ApplyTap;
		OffsetSpin.ValueChanged += v => EditSelectedTiming("offset", tp => tp.Offset = v);
		BpmSpin.ValueChanged += v => EditSelectedTiming("bpm", tp => tp.Bpm = v);
		MeterSpin.ValueChanged += v => EditSelectedTiming("meter", tp => tp.Meter = (int)v);
		KiaiCheck.Toggled += v => EditSelectedTiming("kiai", tp => tp.Kiai = v);

		// Verify
		IssuesList = GetNode<ItemList>("Verify/Panel/Box/Issues");
		VerifySummary = GetNode<Label>("Verify/Panel/Box/Summary");
		IssuesList.ItemActivated += i =>
		{
			if (i < _issueTimes.Count && _issueTimes[(int)i] >= 0)
			{
				Seek(_issueTimes[(int)i]);
				Menu(1);
			}
		};
		GetNode<Button>("Verify/Panel/Box/Run").Pressed += RunChecks;

		GetWindow().FilesDropped += OnFilesDropped;

		if (EditorSongInfo.FilePath != null && IOFile.Exists(EditorSongInfo.FilePath))
			LoadFile(EditorSongInfo.FilePath);
		else
			NewMap();

		if (EditorSongInfo.ResumeTimeMs > 0)
			Seek(EditorSongInfo.ResumeTimeMs);
		EditorSongInfo.ResumeTimeMs = 0;

		SetTool(EditorTool.Place);
		SetRate(1f);
		Menu(Map.TimingPoints.Count == 0 || EditorPlayer.Stream == null ? 0 : 1, instant: true);
	}

	public override void _ExitTree()
	{
		GetWindow().FilesDropped -= OnFilesDropped;
		if (AudioPlayer.Instance.Stream != null && !EditorSongInfo.ReturnToEditor)
			AudioPlayer.Instance.StreamPaused = false;
	}

	// =====================================================================
	// Loading
	// =====================================================================

	private void NewMap()
	{
		Map = ManiaBeatmap.CreateDefault();
		Map.Title = EditorSongInfo.SongTitle;
		Map.Artist = EditorSongInfo.SongArtist;
		Map.Version = EditorSongInfo.SongDifficulty;
		var user = ApiOperator.Username;
		Map.Creator = string.IsNullOrWhiteSpace(user) || user == "Guest" ? EditorSongInfo.SongMapper : user;
		FilePath = null;
		SetFolder = null;
		PendingAudio = null;
		PendingBackground = null;
		EditorPlayer.Stop();
		EditorPlayer.Stream = null;
		SetBackground(null);
		AfterMapChanged();
		Dirty = false;
	}

	private void LoadFile(string path)
	{
		try
		{
			Map = ManiaBeatmap.Load(path);
		}
		catch (Exception e)
		{
			GD.PrintErr(e);
			Notify.Post("Couldn't open the beatmap:\n" + e.Message, Type: NotificationIcons.NotificationType.Warning);
			NewMap();
			return;
		}
		FilePath = path.Replace("\\", "/");
		SetFolder = IOPath.GetDirectoryName(path)?.Replace("\\", "/");
		PendingAudio = null;
		PendingBackground = null;

		EditorPlayer.Stop();
		var audioPath = IOPath.Combine(SetFolder ?? "", Map.AudioFilename);
		EditorPlayer.Stream = !string.IsNullOrEmpty(Map.AudioFilename) && IOFile.Exists(audioPath) ? AudioPlayer.AutoDetectFormat(audioPath) : null;
		var bgPath = IOPath.Combine(SetFolder ?? "", Map.BackgroundFilename);
		SetBackground(!string.IsNullOrEmpty(Map.BackgroundFilename) && IOFile.Exists(bgPath) ? SettingsOperator.LoadImage(bgPath) : null);
		AfterMapChanged();
		Dirty = false;
	}

	private void AfterMapChanged()
	{
		Selection.Clear();
		_undo.Clear();
		_redo.Clear();
		_lastUndoKey = null;
		_playing = false;
		CurrentMs = 0;
		RefreshInfo();
		RefreshTimingList(0);
		UpdateSeekerRange();
		Seek(0);
	}

	private void UpdateSeekerRange()
	{
		_suppressSeek = true;
		Seeker.MinValue = 0;
		Seeker.MaxValue = SongLengthMs / 1000.0;
		Seeker.Value = CurrentMs / 1000.0;
		_suppressSeek = false;
	}

	private void SetBackground(Texture2D tex)
	{
		tex ??= EditorSongInfo.Background ?? SettingsOperator.GetNullImage();
		EditorBackground.Texture = tex;
		if (BackgroundPreview != null) BackgroundPreview.Texture = tex;
	}

	// =====================================================================
	// Info tab
	// =====================================================================

	private void BindText(LineEdit edit, Action<string> apply)
	{
		edit.TextChanged += v =>
		{
			if (_suppressUi) return;
			apply(v);
			MarkDirty();
		};
	}

	private void BindSlider(HSlider slider, LineEdit edit, Action<float> apply)
	{
		slider.MinValue = 0;
		slider.MaxValue = 10;
		slider.Step = 0.1;
		slider.ValueChanged += v =>
		{
			edit.Text = v.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
			if (_suppressUi) return;
			apply((float)v);
			MarkDirty();
		};
		void Commit()
		{
			if (float.TryParse(edit.Text.Replace(',', '.'), System.Globalization.NumberStyles.Float,
				    System.Globalization.CultureInfo.InvariantCulture, out var f))
				slider.Value = Math.Clamp(f, 0, 10);
			else
				edit.Text = slider.Value.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
		}
		edit.TextSubmitted += _ => { Commit(); edit.ReleaseFocus(); };
		edit.FocusExited += Commit;
	}

	private void RefreshInfo()
	{
		_suppressUi = true;
		SetIfDifferent(TitleEdit, Map.Title);
		SetIfDifferent(ArtistEdit, Map.Artist);
		SetIfDifferent(DifficultyEdit, Map.Version);
		SetIfDifferent(MapperEdit, Map.Creator);
		SetIfDifferent(SourceEdit, Map.Source);
		SetIfDifferent(TagsEdit, Map.Tags);
		OdSlider.Value = Map.OverallDifficulty;
		OdEdit.Text = Map.OverallDifficulty.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
		HpSlider.Value = Map.HPDrainRate;
		HpEdit.Text = Map.HPDrainRate.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
		_suppressUi = false;

		BackgroundFile.Text = string.IsNullOrEmpty(Map.BackgroundFilename)
			? "No background picked"
			: Map.BackgroundFilename + (PendingBackground != null ? "  (copied on save)" : "");
		AudioFile.Text = string.IsNullOrEmpty(Map.AudioFilename)
			? "No audio picked"
			: Map.AudioFilename + (EditorPlayer.Stream != null ? $"  ({FormatTime(SongLengthMs)})" : "  (missing!)")
			  + (PendingAudio != null ? "  (copied on save)" : "");
		PreviewValue.Text = Map.PreviewTime >= 0 ? FormatTime(Map.PreviewTime) : "Not set (song select plays from the start)";
	}

	private static void SetIfDifferent(LineEdit edit, string value)
	{
		if (edit.Text != (value ?? "")) edit.Text = value ?? "";
	}

	private void PickBackground() =>
		OpenFileDialog("Pick a background image", new[] { "*.png, *.jpg, *.jpeg, *.webp, *.bmp ; Images" }, ApplyBackgroundFile);

	private void PickAudio() =>
		OpenFileDialog("Pick the song audio", new[] { "*.mp3, *.ogg, *.wav ; Audio" }, ApplyAudioFile);

	private void OpenFileDialog(string title, string[] filters, Action<string> onPicked)
	{
		var dlg = new FileDialog
		{
			Title = title,
			FileMode = FileDialog.FileModeEnum.OpenFile,
			Access = FileDialog.AccessEnum.Filesystem,
			Filters = filters,
			UseNativeDialog = true,
			CurrentDir = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile),
		};
		AddChild(dlg);
		dlg.FileSelected += p => { onPicked(p); dlg.QueueFree(); };
		dlg.Canceled += dlg.QueueFree;
		dlg.PopupCentered(new Vector2I(900, 600));
	}

	private void OnFilesDropped(string[] files)
	{
		foreach (var f in files)
		{
			var ext = IOPath.GetExtension(f).ToLowerInvariant();
			if (ImageExtensions.Contains(ext)) ApplyBackgroundFile(f);
			else if (AudioExtensions.Contains(ext)) ApplyAudioFile(f);
		}
	}

	private void ApplyBackgroundFile(string path)
	{
		if (!IOFile.Exists(path)) return;
		var tex = SettingsOperator.LoadImage(path);
		if (tex == null) { Notify.Post("That image couldn't be loaded.", Type: NotificationIcons.NotificationType.Warning); return; }
		SetBackground(tex);
		Map.BackgroundFilename = IOPath.GetFileName(path);
		PendingBackground = path;
		MarkDirty();
		RefreshInfo();
		Notify.Post("Background set: " + Map.BackgroundFilename);
	}

	private void ApplyAudioFile(string path)
	{
		if (!IOFile.Exists(path)) return;
		AudioStream stream = null;
		try { stream = AudioPlayer.AutoDetectFormat(path); }
		catch (Exception e) { GD.PrintErr(e); }
		if (stream == null)
		{
			Notify.Post("That audio file isn't supported (use mp3, ogg or wav).", Type: NotificationIcons.NotificationType.Warning);
			return;
		}
		Pause();
		EditorPlayer.Stream = stream;
		Map.AudioFilename = IOPath.GetFileName(path);
		PendingAudio = path;
		MarkDirty();
		UpdateSeekerRange();
		RefreshInfo();
		Notify.Post("Audio set: " + Map.AudioFilename);
	}

	// =====================================================================
	// Tabs
	// =====================================================================

	private Tween MenuTween { get; set; }

	/// <summary>0 = Info, 1 = Compose, 2 = Timing, 3 = Verify</summary>
	private void Menu(int mode, bool instant = false)
	{
		_tab = mode;
		var panels = new[] { InfoN, ComposeN, TimingN, VerifyN };
		MenuTween?.Kill();
		for (int i = 0; i < panels.Length; i++)
		{
			panels[i].Visible = i == mode;
			panels[i].Modulate = new Color(1, 1, 1, i == mode && !instant ? 0f : 1f);
		}
		ComposeN.MouseFilter = MouseFilterEnum.Pass;
		if (!instant)
		{
			MenuTween = CreateTween();
			MenuTween.SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
			MenuTween.TweenProperty(panels[mode], "modulate:a", 1f, 0.2f);
		}
		switch (mode)
		{
			case 0: RefreshInfo(); break;
			case 2: RefreshTimingList(TimingList.IsAnythingSelected() ? TimingList.GetSelectedItems()[0] : IndexOfTimingAt(CurrentMs)); break;
			case 3: RunChecks(); break;
		}
	}

	private void Info() => Menu(0);
	private void Compose() => Menu(1);
	private void Timing() => Menu(2);
	private void Verify() => Menu(3);

	// =====================================================================
	// Playback
	// =====================================================================

	private void TogglePlay()
	{
		if (_playing) Pause();
		else Play();
	}

	private void Play()
	{
		if (CurrentMs >= SongLengthMs - 5) Seek(0);
		_playing = true;
		_smoothMs = CurrentMs;
		_lastHitsoundMs = CurrentMs;
		if (EditorPlayer.Stream != null) EditorPlayer.Play((float)(CurrentMs / 1000.0));
		PlayPauseBtn.StringText = "||";
	}

	private void Pause()
	{
		_playing = false;
		EditorPlayer.Stop();
		PlayPauseBtn.StringText = ">";
	}

	private void SetRate(float rate)
	{
		EditorPlayer.PitchScale = rate;
		var idx = Array.IndexOf(Rates, rate);
		if (idx >= 0 && RateOption.Selected != idx) RateOption.Select(idx);
	}

	public void Seek(double ms)
	{
		CurrentMs = Math.Clamp(ms, 0, Math.Max(0, SongLengthMs));
		_smoothMs = CurrentMs;
		_lastHitsoundMs = CurrentMs;
		if (_playing && EditorPlayer.Stream != null) EditorPlayer.Play((float)(CurrentMs / 1000.0));
		_suppressSeek = true;
		Seeker.Value = CurrentMs / 1000.0;
		_suppressSeek = false;
	}

	/// <summary>Seek bar dragged.</summary>
	private void ValueChanged(double value)
	{
		if (_suppressSeek) return;
		Seek(value * 1000.0);
	}

	public void PlayHitsound()
	{
		if (HitsoundsToggle.ButtonPressed) Sample.PlaySample(HitSoundPath);
	}

	public override void _Process(double delta)
	{
		EditorPlayer.VolumeDb = AudioPlayer.Instance.VolumeDb;

		if (_playing)
		{
			_smoothMs += delta * 1000.0 * EditorPlayer.PitchScale;
			if (EditorPlayer.Stream != null)
			{
				if (!EditorPlayer.Playing)
				{
					// reached the end of the song
					CurrentMs = SongLengthMs;
					Pause();
				}
				else
				{
					double truePos = (EditorPlayer.GetPlaybackPosition() + AudioServer.GetTimeSinceLastMix()
					                  - AudioServer.GetOutputLatency()) * 1000.0;
					if (Math.Abs(truePos - _smoothMs) > 40) _smoothMs = truePos;
				}
			}
			if (_playing)
			{
				CurrentMs = Math.Min(_smoothMs, SongLengthMs);
				if (CurrentMs >= SongLengthMs) Pause();
				// hitsounds for notes crossing the judgement line this frame (one per frame for chords)
				if (Map.Notes.Any(n => n.Time > _lastHitsoundMs && n.Time <= CurrentMs)) PlayHitsound();
				_lastHitsoundMs = CurrentMs;
				_suppressSeek = true;
				Seeker.Value = CurrentMs / 1000.0;
				_suppressSeek = false;
			}
		}

		TimeLabel.Text = FormatTime(CurrentMs, true);
		NoteCount.Text = $"{Map.Notes.Count:N0} Notes";
		SelectionCount.Text = $"{Selection.Count:N0} selected";
		ZoomLabel.Text = $"{Zoom * 100:0}%";
		if (DivisorOption.Selected != _divisorIndex) DivisorOption.Select(_divisorIndex);
	}

	public string FormatTime(double ms, bool hours = false)
	{
		var sign = ms < 0 ? "-" : "";
		var ts = TimeSpan.FromMilliseconds(Math.Abs(ms));
		return sign + (hours ? ts.ToString(@"hh\:mm\:ss\.fff") : ts.ToString(@"mm\:ss\.fff"));
	}

	// =====================================================================
	// Compose helpers (called by ManiaEditorField)
	// =====================================================================

	public void OnFieldHover(int time, int lane)
	{
		SelectedPos.Text = lane < 0 ? "-" : $"Lane {lane + 1}\n{FormatTime(time)}";
	}

	public int SnapTime(double ms)
	{
		var tp = Map.TimingAt(ms);
		if (tp == null) return (int)Math.Round(ms);
		double step = tp.BeatLength / Divisor;
		return (int)Math.Round(tp.Offset + Math.Round((ms - tp.Offset) / step) * step);
	}

	/// <summary>Move the current time by whole snap ticks (positive = later).</summary>
	public void StepSnap(int ticks)
	{
		var tp = Map.TimingAt(CurrentMs);
		if (tp == null) { Seek(CurrentMs + ticks * 100); return; }
		double step = tp.BeatLength / Divisor;
		double k = (CurrentMs - tp.Offset) / step;
		double target = ticks > 0
			? tp.Offset + (Math.Floor(k + 0.01) + ticks) * step
			: tp.Offset + (Math.Ceiling(k - 0.01) + ticks) * step;
		var next = Map.TimingAt(target);
		if (next != tp) target = SnapTime(target);
		Seek(target);
	}

	public void ChangeZoom(int dir)
	{
		Zoom = Mathf.Clamp(dir > 0 ? Zoom * 1.25f : Zoom / 1.25f, 0.25f, 5f);
	}

	public void ChangeDivisor(int dir)
	{
		_divisorIndex = Math.Clamp(_divisorIndex + dir, 0, Divisors.Length - 1);
	}

	private void SetTool(EditorTool tool)
	{
		Tool = tool;
		SelectBtn.ButtonPressed = tool == EditorTool.Select;
		PlaceBtn.ButtonPressed = tool == EditorTool.Place;
		RemoveBtn.ButtonPressed = tool == EditorTool.Remove;
	}

	public void MarkDirty() => Dirty = true;

	private double FirstObjectTime() => Map.Notes.Count > 0 ? Map.Notes[0].Time : 0;
	private double LastObjectTime() => Map.Notes.Count > 0 ? Map.Notes.Max(n => Math.Max(n.Time, n.EndTime)) : SongLengthMs;

	// =====================================================================
	// Undo / clipboard
	// =====================================================================

	/// <summary>Record the current notes/timing so the next change can be undone.
	/// Consecutive pushes with the same key (e.g. dragging a spin box) collapse into one step.</summary>
	public void PushUndo(string key = null)
	{
		if (key != null && key == _lastUndoKey) return;
		_lastUndoKey = key;
		_undo.Add(TakeSnapshot());
		if (_undo.Count > 200) _undo.RemoveAt(0);
		_redo.Clear();
	}

	private Snapshot TakeSnapshot() => new()
	{
		Notes = Map.Notes.Select(n => n.Clone()).ToList(),
		Timing = Map.TimingPoints.Select(t => t.Clone()).ToList(),
		PreviewTime = Map.PreviewTime,
	};

	private void Restore(Snapshot s)
	{
		Map.Notes = s.Notes.Select(n => n.Clone()).ToList();
		Map.TimingPoints = s.Timing.Select(t => t.Clone()).ToList();
		Map.PreviewTime = s.PreviewTime;
		Selection.Clear();
		MarkDirty();
		RefreshTimingList(Math.Min(SelectedTimingIndex(), Map.TimingPoints.Count - 1));
		RefreshInfo();
	}

	private void Undo()
	{
		if (_undo.Count == 0) return;
		_redo.Add(TakeSnapshot());
		var s = _undo[^1];
		_undo.RemoveAt(_undo.Count - 1);
		_lastUndoKey = null;
		Restore(s);
	}

	private void Redo()
	{
		if (_redo.Count == 0) return;
		_undo.Add(TakeSnapshot());
		var s = _redo[^1];
		_redo.RemoveAt(_redo.Count - 1);
		_lastUndoKey = null;
		Restore(s);
	}

	private void CopySelection()
	{
		if (Selection.Count == 0) return;
		var notes = Selection.OrderBy(n => n.Time).ToList();
		int baseTime = notes[0].Time;
		_clipboard = notes.Select(n =>
		{
			var c = n.Clone();
			c.Time -= baseTime;
			if (c.IsHold) c.EndTime -= baseTime;
			return c;
		}).ToList();
	}

	private void DeleteSelection()
	{
		if (Selection.Count == 0) return;
		PushUndo();
		Map.Notes.RemoveAll(n => Selection.Contains(n));
		Selection.Clear();
		MarkDirty();
	}

	private void Paste()
	{
		if (_clipboard.Count == 0) return;
		PushUndo();
		int at = SnapTime(CurrentMs);
		var occupied = Map.Notes.Select(n => (n.Time, n.Lane)).ToHashSet();
		Selection.Clear();
		foreach (var c in _clipboard)
		{
			var n = c.Clone();
			n.Time += at;
			if (n.IsHold) n.EndTime += at;
			if (!occupied.Add((n.Time, n.Lane))) continue;
			Map.Notes.Add(n);
			Selection.Add(n);
		}
		Map.SortAll();
		MarkDirty();
	}

	private void _EditMenu(int id)
	{
		switch (id)
		{
			case 0: Undo(); break;
			case 1: Redo(); break;
			case 2: CopySelection(); DeleteSelection(); break;
			case 3: CopySelection(); break;
			case 4: Paste(); break;
			case 5: DeleteSelection(); break;
			case 6: Selection.Clear(); foreach (var n in Map.Notes) Selection.Add(n); break;
		}
	}

	// =====================================================================
	// Timing tab
	// =====================================================================

	private int SelectedTimingIndex() =>
		TimingList.IsAnythingSelected() ? TimingList.GetSelectedItems()[0] : -1;

	private int IndexOfTimingAt(double ms)
	{
		var tp = Map.TimingAt(ms);
		return tp == null ? -1 : Map.TimingPoints.IndexOf(tp);
	}

	private void RefreshTimingList(int select)
	{
		TimingList.Clear();
		foreach (var tp in Map.TimingPoints)
			TimingList.AddItem($"{FormatTime(tp.Offset)}    {tp.Bpm:0.###} BPM    {tp.Meter}/4{(tp.Kiai ? "    Kiai" : "")}");
		if (Map.TimingPoints.Count > 0)
		{
			select = Math.Clamp(select < 0 ? 0 : select, 0, Map.TimingPoints.Count - 1);
			TimingList.Select(select);
			TimingList.EnsureCurrentIsVisible();
		}
		LoadTimingEditor();
	}

	private void LoadTimingEditor()
	{
		var i = SelectedTimingIndex();
		bool has = i >= 0 && i < Map.TimingPoints.Count;
		_suppressUi = true;
		foreach (var c in new Control[] { OffsetSpin, BpmSpin, MeterSpin, KiaiCheck })
		{
			if (c is SpinBox sb) sb.Editable = has;
			if (c is CheckBox cb) cb.Disabled = !has;
		}
		if (has)
		{
			var tp = Map.TimingPoints[i];
			OffsetSpin.Value = tp.Offset;
			BpmSpin.Value = tp.Bpm;
			MeterSpin.Value = tp.Meter;
			KiaiCheck.ButtonPressed = tp.Kiai;
		}
		_suppressUi = false;
		_lastUndoKey = null;
	}

	private void EditSelectedTiming(string key, Action<EditorTimingPoint> apply)
	{
		if (_suppressUi) return;
		var i = SelectedTimingIndex();
		if (i < 0 || i >= Map.TimingPoints.Count) return;
		var tp = Map.TimingPoints[i];
		PushUndo("timing-" + key + i);
		apply(tp);
		Map.SortAll();
		MarkDirty();
		var keep = _lastUndoKey;
		RefreshTimingList(Map.TimingPoints.IndexOf(tp));
		_lastUndoKey = keep;
	}

	private void AddTimingPoint()
	{
		var at = Math.Round(CurrentMs);
		if (Map.TimingPoints.Any(t => Math.Abs(t.Offset - at) < 1))
		{
			Notify.Post("There's already a timing point here.");
			return;
		}
		PushUndo();
		var prev = Map.TimingAt(at);
		var tp = new EditorTimingPoint { Offset = at, Bpm = prev?.Bpm ?? 120, Meter = prev?.Meter ?? 4, Volume = prev?.Volume ?? 100 };
		Map.TimingPoints.Add(tp);
		Map.SortAll();
		MarkDirty();
		RefreshTimingList(Map.TimingPoints.IndexOf(tp));
	}

	private void RemoveTimingPoint()
	{
		var i = SelectedTimingIndex();
		if (i < 0) return;
		if (Map.TimingPoints.Count <= 1)
		{
			Notify.Post("A beatmap needs at least one timing point. Edit it instead.");
			return;
		}
		PushUndo();
		Map.TimingPoints.RemoveAt(i);
		MarkDirty();
		RefreshTimingList(i - 1);
	}

	private void Tap()
	{
		var now = Time.GetTicksMsec();
		if (_taps.Count > 0 && now - _taps[^1] > 2000) _taps.Clear();
		_taps.Add(now);
		if (_taps.Count > 16) _taps.RemoveAt(0);
		if (_taps.Count < 4)
		{
			TapResult.Text = $"Tap {4 - _taps.Count} more time(s)";
			return;
		}
		double avg = (double)(_taps[^1] - _taps[0]) / (_taps.Count - 1);
		// Taps are in real time; convert back to song time when the playback rate is slowed down.
		_tappedBpm = 60000.0 / avg * (1.0 / EditorPlayer.PitchScale);
		TapResult.Text = $"{_tappedBpm:0.00} BPM ({_taps.Count} taps)";
	}

	private void ApplyTap()
	{
		if (_tappedBpm <= 0) { Notify.Post("Tap along with the song first."); return; }
		BpmSpin.Value = Math.Round(_tappedBpm);
	}

	// =====================================================================
	// Verify tab
	// =====================================================================

	private enum Severity { Error, Warning, Info }

	private List<(Severity sev, string text, double time)> CollectIssues()
	{
		var issues = new List<(Severity, string, double)>();
		void Add(Severity s, string t, double time = -1) => issues.Add((s, t, time));

		if (string.IsNullOrWhiteSpace(Map.Title)) Add(Severity.Error, "Song title is empty.");
		if (string.IsNullOrWhiteSpace(Map.Artist)) Add(Severity.Error, "Song artist is empty.");
		if (string.IsNullOrWhiteSpace(Map.Version)) Add(Severity.Error, "Difficulty name is empty.");
		if (string.IsNullOrWhiteSpace(Map.Creator)) Add(Severity.Error, "Mapper name is empty.");
		if (Map.Title == "Untitled Title" || Map.Artist == "Untitled Artist")
			Add(Severity.Warning, "Title/artist still use the placeholder text.");

		if (EditorPlayer.Stream == null)
			Add(Severity.Error, string.IsNullOrEmpty(Map.AudioFilename) ? "No audio picked (Info tab)." : $"Audio file '{Map.AudioFilename}' couldn't be loaded.");
		if (string.IsNullOrEmpty(Map.BackgroundFilename)) Add(Severity.Warning, "No background image picked (Info tab).");
		if (Map.PreviewTime < 0) Add(Severity.Info, "No song select preview point set (Info tab).");

		if (Map.TimingPoints.Count == 0) Add(Severity.Error, "No timing points (Timing tab).");
		if (Map.Notes.Count == 0) Add(Severity.Error, "The beatmap has no notes.");

		if (Map.TimingPoints.Count > 0)
		{
			var first = Map.TimingPoints[0].Offset;
			var early = Map.Notes.Where(n => n.Time < first).ToList();
			if (early.Count > 0) Add(Severity.Warning, $"{early.Count} note(s) are before the first timing point.", early[0].Time);
		}

		if (EditorPlayer.Stream != null)
		{
			var len = SongLengthMs;
			var late = Map.Notes.Where(n => Math.Max(n.Time, n.EndTime) > len).ToList();
			if (late.Count > 0) Add(Severity.Error, $"{late.Count} note(s) are after the end of the song.", late[0].Time);
		}

		// unsnapped notes: not within 2ms of any 1/1..1/16 or 1/12 tick
		int unsnapped = 0;
		double firstUnsnapped = -1;
		foreach (var n in Map.Notes)
		{
			var tp = Map.TimingAt(n.Time);
			if (tp == null) break;
			bool ok = new[] { 16, 12 }.Any(d =>
			{
				double step = tp.BeatLength / d;
				double k = Math.Round((n.Time - tp.Offset) / step);
				return Math.Abs(tp.Offset + k * step - n.Time) <= 2;
			});
			if (!ok) { unsnapped++; if (firstUnsnapped < 0) firstUnsnapped = n.Time; }
		}
		if (unsnapped > 0) Add(Severity.Warning, $"{unsnapped} note(s) aren't snapped to the beat grid.", firstUnsnapped);

		// notes sitting inside a hold in the same lane
		foreach (var hold in Map.Notes.Where(n => n.IsHold))
		{
			var inside = Map.Notes.FirstOrDefault(n => n != hold && n.Lane == hold.Lane && n.Time >= hold.Time && n.Time <= hold.EndTime);
			if (inside != null) Add(Severity.Error, $"Note overlaps a hold in lane {hold.Lane + 1}.", inside.Time);
		}

		// another difficulty in the set already uses this file name
		if (SetFolder != null && IODirectory.Exists(SetFolder))
		{
			var target = IOPath.Combine(SetFolder, Map.DifficultyFileName()).Replace("\\", "/");
			if (target != FilePath && IOFile.Exists(target))
				Add(Severity.Error, $"Another difficulty in this set is already called '{Map.Version}'.");
		}

		if (Map.Notes.Any(n => n.IsHold))
			Add(Severity.Info, "Hold notes are kept in the file, but gameplay currently plays them as taps.");

		return issues;
	}

	private void RunChecks()
	{
		var issues = CollectIssues();
		IssuesList.Clear();
		_issueTimes.Clear();
		foreach (var (sev, text, time) in issues)
		{
			var prefix = sev switch { Severity.Error => "[Error]", Severity.Warning => "[Warning]", _ => "[Info]" };
			var label = time >= 0 ? $"{prefix} {FormatTime(time)}  {text}" : $"{prefix} {text}";
			int idx = IssuesList.AddItem(label);
			IssuesList.SetItemCustomFgColor(idx, sev switch
			{
				Severity.Error => new Color(1f, 0.45f, 0.45f),
				Severity.Warning => new Color(1f, 0.85f, 0.4f),
				_ => new Color(0.7f, 0.85f, 1f),
			});
			_issueTimes.Add(time);
		}
		int errors = issues.Count(i => i.sev == Severity.Error);
		int warnings = issues.Count(i => i.sev == Severity.Warning);
		VerifySummary.Text = errors == 0 && warnings == 0
			? "Everything looks good! Ready to save and play."
			: $"{errors} error(s), {warnings} warning(s). Errors must be fixed before the beatmap can be played.";
		if (issues.Count == 0)
		{
			IssuesList.AddItem("No problems found.");
			_issueTimes.Add(-1);
		}
	}

	// =====================================================================
	// Saving
	// =====================================================================

	private bool Save(bool quiet = false)
	{
		if (string.IsNullOrWhiteSpace(Map.Title) || string.IsNullOrWhiteSpace(Map.Artist) || string.IsNullOrWhiteSpace(Map.Version))
		{
			Notify.Post("Fill in the title, artist and difficulty name before saving (Info tab).", Type: NotificationIcons.NotificationType.Warning);
			Menu(0);
			return false;
		}
		bool hasAudio = PendingAudio != null ||
		                (SetFolder != null && !string.IsNullOrEmpty(Map.AudioFilename) && IOFile.Exists(IOPath.Combine(SetFolder, Map.AudioFilename)));
		if (!hasAudio)
		{
			Notify.Post("Pick an audio file before saving (Info tab).", Type: NotificationIcons.NotificationType.Warning);
			Menu(0);
			return false;
		}

		try
		{
			var folder = SetFolder;
			if (folder == null)
			{
				var baseName = IOPath.Combine(SettingsOperator.beatmapsdir, Map.SetFolderName());
				folder = baseName;
				for (int n = 2; IODirectory.Exists(folder); n++) folder = $"{baseName} ({n})";
				IODirectory.CreateDirectory(folder);
			}
			folder = folder.Replace("\\", "/");

			if (PendingAudio != null) CopyInto(PendingAudio, folder, Map.AudioFilename);
			if (PendingBackground != null) CopyInto(PendingBackground, folder, Map.BackgroundFilename);

			var target = IOPath.Combine(folder, Map.DifficultyFileName()).Replace("\\", "/");
			if (target != FilePath && IOFile.Exists(target))
			{
				Notify.Post($"Another difficulty in this set is already called '{Map.Version}'. Rename it in the Info tab.", Type: NotificationIcons.NotificationType.Warning);
				Menu(0);
				return false;
			}

			IOFile.WriteAllText(target, Map.Serialize(), new UTF8Encoding(false));
			var oldPath = FilePath;
			if (oldPath != null && oldPath != target && IOFile.Exists(oldPath))
				IOFile.Delete(oldPath); // difficulty was renamed

			SetFolder = folder;
			FilePath = target;
			PendingAudio = null;
			PendingBackground = null;
			Dirty = false;
			EditorSongInfo.FilePath = target;
			RegisterInSongList(oldPath, target);
			RefreshInfo();
			if (!quiet) Notify.Post($"Saved {Map.Artist} - {Map.Title} [{Map.Version}]");
			return true;
		}
		catch (Exception e)
		{
			GD.PrintErr(e);
			Notify.Post("Couldn't save the beatmap:\n" + e.Message, Type: NotificationIcons.NotificationType.Warning);
			return false;
		}
	}

	private static void CopyInto(string source, string folder, string fileName)
	{
		var dest = IOPath.Combine(folder, fileName);
		if (IOPath.GetFullPath(source) == IOPath.GetFullPath(dest)) return;
		IOFile.Copy(source, dest, true);
	}

	private static bool SamePath(string a, string b) =>
		a != null && b != null && a.Replace("\\", "/") == b.Replace("\\", "/");

	/// <summary>Add or refresh this difficulty in the song list so it shows up in song select.</summary>
	private int RegisterInSongList(string oldPath, string newPath)
	{
		var list = SettingsOperator.Beatmaps;
		int idx = list.FindIndex(b => SamePath(b.Rawurl, oldPath) || SamePath(b.Rawurl, newPath));
		int setId;
		if (idx >= 0) setId = list[idx].SetID;
		else
		{
			var folder = IOPath.GetDirectoryName(newPath)?.Replace("\\", "/");
			var sibling = list.FirstOrDefault(b => SamePath(IOPath.GetDirectoryName(b.Rawurl ?? ""), folder));
			if (sibling != null) setId = sibling.SetID;
			else
			{
				var listener = GetNode<BeatmapListener>("/root/BeatmapListener");
				setId = listener.SetID++;
			}
		}

		SettingsOperator.Parse_Beatmapfile(newPath, SetID: setId);
		if (idx >= 0)
		{
			var legend = list[^1];
			list.RemoveAt(list.Count - 1);
			legend.ID = list[idx].ID;
			list[idx] = legend;
		}
		else idx = list.Count - 1;

		SettingsOperator.SessionConfig.ReloadDB = true;
		return idx;
	}

	private void SaveAsNewDifficulty()
	{
		PromptText("Save as a new difficulty", "New difficulty name:", Map.Version + " 2", name =>
		{
			name = name.Trim();
			if (name.Length == 0) return;
			if (Dirty || FilePath == null)
			{
				if (!Save(quiet: true)) return;
			}
			Map.Version = name;
			Map.Notes.Clear();
			Map.BeatmapID = -1;
			Selection.Clear();
			_undo.Clear();
			_redo.Clear();
			FilePath = null;
			// SetFolder stays: the new difficulty lives in the same set and shares audio/background.
			if (Save(quiet: true))
				Notify.Post($"Created difficulty [{name}]");
			RefreshInfo();
		});
	}

	private void Export()
	{
		if ((Dirty || FilePath == null) && !Save(quiet: true)) return;
		try
		{
			IODirectory.CreateDirectory(SettingsOperator.exportdir);
			var zip = IOPath.Combine(SettingsOperator.exportdir, IOPath.GetFileName(SetFolder) + ".osz");
			if (IOFile.Exists(zip)) IOFile.Delete(zip);
			System.IO.Compression.ZipFile.CreateFromDirectory(SetFolder, zip);
			Notify.Post("Exported to\n" + zip);
			OS.ShellShowInFileManager(zip);
		}
		catch (Exception e)
		{
			GD.PrintErr(e);
			Notify.Post("Couldn't export the beatmap:\n" + e.Message, Type: NotificationIcons.NotificationType.Warning);
		}
	}

	private void TestPlay()
	{
		var errors = CollectIssues().Where(i => i.sev == Severity.Error).ToList();
		if (errors.Count > 0)
		{
			Notify.Post($"Fix {errors.Count} error(s) before testing:\n{errors[0].text}", Type: NotificationIcons.NotificationType.Warning);
			Menu(3);
			return;
		}
		if ((Dirty || FilePath == null) && !Save(quiet: true)) return;
		var idx = SettingsOperator.Beatmaps.FindIndex(b => SamePath(b.Rawurl, FilePath));
		if (idx < 0) idx = RegisterInSongList(null, FilePath);

		Pause();
		EditorSongInfo.ReturnToEditor = true;
		EditorSongInfo.ResumeTimeMs = CurrentMs;
		EditorSongInfo.FilePath = FilePath;
		SettingsOperator.SessionConfig.SongID = -1; // force a full refresh even if this map was already selected
		GetNode<SettingsOperator>("/root/SettingsOperator").SelectSongID(idx);
		SettingsOperator.SessionConfig.GameMode = 0; // this editor makes mania maps
		GetNode<SceneTransition>("/root/Transition").Switch("res://Panels/Screens/SongLoadingScreen.tscn");
	}

	// =====================================================================
	// File menu / navigation
	// =====================================================================

	private void _FileMenu(int id)
	{
		switch (id)
		{
			case 0: ConfirmDiscard(() => { Pause(); EditorSongInfo.Reset(); NewMap(); Menu(0); }); break;
			case 1:
				ConfirmDiscard(() =>
				{
					Pause();
					SettingsOperator.CreateSelectingBeatmap = true;
					GetNode<SceneTransition>("/root/Transition").Switch("res://Panels/Screens/song_select.tscn");
				});
				break;
			case 2: SaveAsNewDifficulty(); break;
			case 3: Save(); break;
			case 4: Export(); break;
			case 5: _on_back(); break;
		}
	}

	private void _on_back()
	{
		ConfirmDiscard(() =>
		{
			Pause();
			GetNode<SceneTransition>("/root/Transition").Switch("res://Panels/Screens/home_screen.tscn");
		});
	}

	private void ConfirmDiscard(Action then)
	{
		if (!Dirty) { then(); return; }
		var dlg = new ConfirmationDialog
		{
			Title = "Unsaved changes",
			DialogText = "You have unsaved changes. Save them first?",
			OkButtonText = "Save",
			CancelButtonText = "Cancel",
		};
		dlg.AddButton("Discard", true, "discard");
		AddChild(dlg);
		dlg.Confirmed += () => { dlg.QueueFree(); if (Save()) then(); };
		dlg.CustomAction += action =>
		{
			if (action != "discard") return;
			dlg.QueueFree();
			Dirty = false;
			then();
		};
		dlg.Canceled += dlg.QueueFree;
		dlg.PopupCentered();
	}

	private void PromptText(string title, string label, string initial, Action<string> onOk)
	{
		var dlg = new ConfirmationDialog { Title = title, OkButtonText = "OK" };
		var box = new VBoxContainer();
		box.AddChild(new Label { Text = label });
		var edit = new LineEdit { Text = initial, CustomMinimumSize = new Vector2(320, 0) };
		box.AddChild(edit);
		dlg.AddChild(box);
		AddChild(dlg);
		dlg.RegisterTextEnter(edit);
		dlg.Confirmed += () => { var t = edit.Text; dlg.QueueFree(); onOk(t); };
		dlg.Canceled += dlg.QueueFree;
		dlg.PopupCentered();
		edit.GrabFocus();
		edit.SelectAll();
	}

	// =====================================================================
	// Keyboard shortcuts
	// =====================================================================

	private bool IsTyping()
	{
		var focus = GetViewport().GuiGetFocusOwner();
		return focus is LineEdit or TextEdit;
	}

	public override void _Input(InputEvent @event)
	{
		if (@event is not InputEventKey { Pressed: true } key) return;
		if (IsTyping()) return;
		// Don't steal keys from open dialogs.
		if (GetChildren().OfType<Window>().Any(w => w.Visible)) return;

		bool ctrl = key.CtrlPressed || key.MetaPressed;
		bool handled = true;
		switch (key.Keycode)
		{
			case Key.S when ctrl: Save(); break;
			case Key.Z when ctrl && key.ShiftPressed: Redo(); break;
			case Key.Z when ctrl: Undo(); break;
			case Key.Y when ctrl: Redo(); break;
			case Key.Space: TogglePlay(); break;
			case Key.C when ctrl && _tab == 1: CopySelection(); break;
			case Key.X when ctrl && _tab == 1: CopySelection(); DeleteSelection(); break;
			case Key.V when ctrl && _tab == 1: Paste(); break;
			case Key.A when ctrl && _tab == 1: Selection.Clear(); foreach (var n in Map.Notes) Selection.Add(n); break;
			case Key.Delete or Key.Backspace when _tab == 1: DeleteSelection(); break;
			case Key.Escape when _tab == 1 && Selection.Count > 0: Selection.Clear(); break;
			case Key.Key1 when _tab == 1 && !ctrl: SetTool(EditorTool.Select); break;
			case Key.Key2 when _tab == 1 && !ctrl: SetTool(EditorTool.Place); break;
			case Key.Key3 when _tab == 1 && !ctrl: SetTool(EditorTool.Remove); break;
			case Key.Up or Key.Right when _tab == 1: StepSnap(key.ShiftPressed ? 4 : 1); break;
			case Key.Down or Key.Left when _tab == 1: StepSnap(key.ShiftPressed ? -4 : -1); break;
			case Key.Home: Seek(0); break;
			case Key.End: Seek(SongLengthMs); break;
			case Key.T when _tab == 2 && !ctrl: Tap(); break;
			default: handled = false; break;
		}
		if (handled) GetViewport().SetInputAsHandled();
	}
}
