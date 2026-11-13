using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using Game;
using System.Reflection.Metadata;
public class NotesEn {
	public int timing {get;set;}
	public int NotePart { get; set; } = -1;
	public bool hit {get;set;}
	public bool exist {get;set;}
	public string Sample => SampleSet.Normal.First();
}
public class KeyL
{
	public PanelContainer Node { get; set; }
	public bool hit { get; set; }
	public string KeyCode { get; set; }
	public Tween Ani { get; set; }
}

public partial class Gameplay : Control
{
	public static Gameplay Instance;
	private string oldtitle = "";
	private SettingsOperator SettingsOperator { get; set; }
	private int BadCombo { get; set; }
	public static ApiOperator ApiOperator { get; set; }
	//public static Label Ttiming { get; set; }
	//public static Label Hits { get; set; }
	public HBoxContainer Chart { get; set; }
	public int HitPoint { get; set; }
	private Tween MainScreenAnimation { get; set; }
	public List<KeyL> Keys = new List<KeyL>();
	public int JudgeResult = -1;
	public int nodeSize = 54;
	public bool HideHUD { get; set; }
	public ColorRect meh { get; set; }
	public ColorRect great { get; set; }
	public ColorRect perfect { get; set; }
	public float mshit { get; set; }
	public float mshitold { get; set; }
	public long startedtime { get; set; } = 0;
	public bool songstarted = false;
	//private Control PlayfieldNode { get; set; }
	public Node2D noteblock { get; set; }
	private Label ppv2LabelTest { get; set; }
	public bool hittextinit = false;
	public TextureRect hittext { get; set; }
	public Tween hittextani { get; set; }
	private Tween hitnoteani { get; set; }
	private Tween HurtAnimation { get; set; }
	public Vector2 hittextoldpos { get; set; }
	public Timer WaitClock { get; set; }
	public static List<NotesEn> Notes = new List<NotesEn>();
	public TextureRect Beatmap_Background { get; set; }
	private Node PauseMenu { get; set; }
	public bool Finished { get; private set; }
	public static int score { get; set; }
	public static List<DanceCounter> dance { get; set; }
	private int DanceIndex { get; set; }
	private Label debugtext { get; set; }
	public static int ReplayINT { get; set;} // Track the progress of replay...
	public static bool Dead { get; set; }
	private int scoreint { get; set; }
	private Control SpectatorPanel { get; set; }
	private Tween scoretween { get; set; }
	public int MaxNotes { get; set; }
	private Label ComboCounter { get; set; }
	public static int seed = 0;
	private Control HUD { get; set; }
	private float speedold = 1f;
	// pp v3 / competitive score state for this play (see Game.PerformanceCalculator).
	/// <summary>Note count the server rates the map with (real columns, duplicates removed).</summary>
	public static int PerfNoteCount { get; private set; }
	/// <summary>pp of an SS with the selected mods.</summary>
	public static double MaxPp { get; private set; }
	public static double StarRating { get; private set; }
	/// <summary>Score multiplier, fixed when the map loads (the fail animation bends the pitch).</summary>
	public static double ScoreMult { get; private set; } = 1.0;
	/// <summary>Sum of PerformanceCalculator.ComboWeight over every hit so far.</summary>
	public static long ComboPortion { get; private set; }
	private float audioOffset { get; set; } = 0;
	[Export] private PackedScene EffectTemplate;
	private Effect EffectNode { get; set; }

	// ── Judgement rules ──────────────────────────────────────────────────────
	// The server re-plays every submitted replay to compute pp
	// (Chikara/Chikara/ppv2calc.py). Everything that decides a judgement lives
	// in ProcessInputFrame/MissPass/JudgeDiff below and is mirrored 1:1 there,
	// so the server lands on the exact same counts and pp. Change both together.
	//
	// All times are whole song-milliseconds (floor of gametime). A replay line
	// "t,col" means: the press of column `col` was processed on the frame at t.
	// diff = t - noteTime (negative = early).

	// Spam protection: minimum ms between accepted key presses per column
	public const int SPAM_COOLDOWN_MS = 80; // 80ms-matches osu!mania's input buffer
	// An unhit note counts as a miss once it is this many ms late
	// (= where the old on-screen check fired with the default playfield layout).
	public const int MISS_LATE_MS = 206;
	private long[] LastKeyPressTime = new long[4] { long.MinValue, long.MinValue, long.MinValue, long.MinValue };
	// Presses that arrived in _Input since the last game tick, in order.
	private readonly List<int> QueuedPresses = new List<int>();
	// Time of the last hit object (the song ends here); no note becomes a miss after it.
	private int SongEndMs = int.MaxValue;

	// Unstable Rate: stores raw hit offsets (ms) to compute std-dev * 10
	private List<float> HitOffsets = new List<float>();
	private static readonly Game.ScoreCalculator ScoreCalc = new Game.ScoreCalculator();
	private float ComputeUnstableRate()
	{
		if (HitOffsets.Count < 2) return 0f;
		float mean = HitOffsets.Average();
		float variance = HitOffsets.Select(x => (x - mean) * (x - mean)).Average();
		return (float)Math.Sqrt(variance) * 10f;
	}
	private void ShowPauseMenu()
	{
		Cursor.CursorVisible = true;
		PauseMenu = GD.Load<PackedScene>("res://Panels/Screens/PauseMenu.tscn").Instantiate().GetNode<Control>(".");
		GetTree().Root.AddChild(PauseMenu);
	}

	private void FailAnimation()
	{
		var Interval = 1f; // Interval of speed that the animation will go.
		MainScreenAnimation?.Kill();
		MainScreenAnimation = CreateTween();
		PivotOffset = new Vector2(Size.X / 2, Size.Y / 2);
		MainScreenAnimation.TweenInterval(0.5f);
		MainScreenAnimation.Parallel().TweenProperty(this, "modulate", new Color(1f, 0.8f, 0.8f, 1f), Interval).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		MainScreenAnimation.Parallel().TweenProperty(this, "position", new Vector2(Position.X, Position.Y + 20), Interval).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		MainScreenAnimation.Parallel().TweenProperty(this, "rotation", 0.05f, Interval).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		MainScreenAnimation.Parallel().TweenProperty(AudioPlayer.Instance, "pitch_scale", 0.01f, Interval).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		MainScreenAnimation.Parallel().TweenProperty(this, "scale", Scale * 0.7f, Interval).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		MainScreenAnimation.Connect("finished", new Callable(this, nameof(ShowPauseMenu)));
	}
	private int maxrndvalue { get; set; }
	public void ReloadBeatmap(string filepath)
	{
		Notes.Clear();
		using var file = FileAccess.Open(filepath, FileAccess.ModeFlags.Read);
		var text = file.GetAsText();
		var lines = text.Split("\n");
		var part = 0;
		var timing = 0;
		var timen = -1;
		var BaseRnd = new Random(seed);
		var isHitObjectSection = false;
		int index = 0;
		BeatmapLegend beatmap = SettingsOperator.Beatmaps[SettingsOperator.SessionConfig.SongID];
		dance = beatmap.Dance;
		SongEndMs = (int)beatmap.Timetotal;

		var seen = new HashSet<(int timing, int section)>();
		// The map as the server rates it: real columns (never Random's), duplicates removed.
		var ratedNotes = new List<(int time, int col)>();
		var ratedSeen = new HashSet<(int, int)>();

		foreach (string line in lines)
		{
			if (line.Trim() == "[HitObjects]") { isHitObjectSection = true; continue; }
			if (!isHitObjectSection) continue;
			if (string.IsNullOrWhiteSpace(line) || line.StartsWith('[')) break;

			string[] section = line.Split(':', ',');
			timing = Convert.ToInt32(section[2]);

			int realPart = Math.Clamp((int)Math.Floor(Convert.ToInt32(section[0]) * 4 / 512.0), 0, 3);
			if (ratedSeen.Add((timing, realPart))) ratedNotes.Add((timing, realPart));
			if (ModsOperator.Mods["random"])
				part = BaseRnd.Next(0, 4);
			else
				part = realPart;

			timen = -timing;
			var key = (timen, part);

			// ✅ Skip duplicates in O(1), no linear scan
			if (seen.Contains(key)) { index++; continue; }
			seen.Add(key);

			Notes.Add(new NotesEn
			{
				timing = timen,
				NotePart = part,
			});
			index++;
		}

		MaxNotes = Notes.Count;

		string alias = ModsOperator.AliasOf(ModsOperator.Mods);
		double speed = AudioPlayer.Instance.PitchScale;
		PerfNoteCount = ratedNotes.Count;
		StarRating = PerformanceCalculator.StarRating(ratedNotes, PerformanceCalculator.GetRate(alias, speed));
		MaxPp = PerformanceCalculator.MaxPerformance(StarRating, PerfNoteCount, alias);
		ScoreMult = PerformanceCalculator.ScoreMultiplier(alias, speed);
		ComboPortion = 0;
		SettingsOperator.Gameplaycfg.maxpp = MaxPp;
	}

	/// <summary>Adds a hit's combo weight (call after Combo++).</summary>
	private static void AddComboWeight(int judgementWeight)
	{
		ComboPortion += PerformanceCalculator.ComboWeight(judgementWeight, SettingsOperator.Gameplaycfg.Combo);
	}

	/// <summary>Final pp of the play, exactly what the server computes from the replay.</summary>
	public static double FinalPerformance() => PerformanceCalculator.Performance(MaxPp, PerfNoteCount,
		SettingsOperator.Gameplaycfg.Max, SettingsOperator.Gameplaycfg.Great, SettingsOperator.Gameplaycfg.Meh, SettingsOperator.Gameplaycfg.Bad);

	public override void _EnterTree()
	{
		SettingsOperator.inGameplay = true;
		oldtitle = GetWindow().Title;
		DisplayServer.WindowSetTitle($"{oldtitle} - {SettingsOperator.SessionConfig.BeatmapArtist ?? ""} - {SettingsOperator.SessionConfig.BeatmapTitle ?? ""}");
		SettingsOperator.Gameplaycfg.Username = ApiOperator.Username;
		SettingsOperator.Gameplaycfg.EpochTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
		float scale = 1f - (SettingsOperator.Gameplaycfg.BeatmapAccuracy / 10f);
		SettingsOperator.PerfectJudge = (int)Math.Max(16,SettingsOperator.PerfectJudgeMin * scale);
		SettingsOperator.GreatJudge = (int)(SettingsOperator.PerfectJudge * 4);
		SettingsOperator.MehJudge = (int)(SettingsOperator.PerfectJudge * 6);
		SettingsOperator.ResetScore();
		SettingsOperator.Resetms();
	}
	 public Vector2 PlayFieldPOS { get; set; }
	public override void _Ready()
	{
		
		Instance = this;
		GD.Print("Setting up stuff...");
		speedold = AudioPlayer.Instance.PitchScale;
		seed = new Random().Next(1,214562543);
		ReplayINT = 0;
		SettingsOperator = GetNode<SettingsOperator>("/root/SettingsOperator");
		SpectatorPanel = GD.Load<PackedScene>("res://Panels/Overlays/SpectatorSettings.tscn").Instantiate().GetNode<Control>(".");
		ApiOperator = GetNode<ApiOperator>("/root/ApiOperator");
		Beatmap_Background = GetNode<TextureRect>("./Beatmap_Background");
		WaitClock = GetNode<Timer>("Wait");
		EffectNode = EffectTemplate.Instantiate<Effect>();
		EffectNode.Template = true;
		GetTree().CurrentScene.AddChild(EffectNode);
		EffectNode.SelfModulate = new Color(1f, 1f, 1f, 0f);
		AudioPlayer.Instance.Stop();
		ClipContents = true;
		HUD = GetNode<Control>("HUD");
		HideHUD = Check.CheckBoolValue(SettingsOperator.GetSetting("hidehud").ToString());
		HUD.Visible = !HideHUD;
		
		if (HasNode("Combo"))
		{
			ComboCounter = GetNode<Label>("Combo");
		}

		Dead = false;
		BeatmapBackground.FlashEnable = false;

		HealthBar.Reset();
		//PlayfieldNode = GetNode<Control>("Playfield");
		Chart = GetNode<HBoxContainer>("Playfield/ChartSections");
		hittext = GD.Load<PackedScene>("res://Panels/GameplayElements/Static/hittext.tscn").Instantiate().GetNode<TextureRect>(".");
		hittextinit = true;
		hittext.Modulate = new Color(1f, 1f, 1f, 0f);
		hittext.ZIndex = 100;
		GetNode<Control>("Playfield").AddChild(hittext);
		hittextoldpos = hittext.Position;
		
		meh = new ColorRect();
		meh.Color = new Color(0.5f, 0f, 0f, 0.3f);
		meh.Visible = false;
		meh.Size = new Vector2(450, SettingsOperator.MehJudge);
		meh.Position = new Vector2(0, -SettingsOperator.MehJudge / 2);
		GetNode<ColorRect>("Playfield/Guard").AddChild(meh);

		great = new ColorRect();
		great.Color = new Color(0f, 0.5f, 0f, 0.3f);
		great.Visible = false;
		great.Size = new Vector2(450, SettingsOperator.GreatJudge);
		great.Position = new Vector2(0, -SettingsOperator.GreatJudge / 2);
		GetNode<ColorRect>("Playfield/Guard").AddChild(great);

		perfect = new ColorRect();
		perfect.Color = new Color(0f, 0f, 0.5f, 0.3f);
		perfect.Visible = false;
		perfect.Size = new Vector2(450, SettingsOperator.PerfectJudge * 2);
		perfect.Position = new Vector2(0, -SettingsOperator.PerfectJudge);
		GetNode<ColorRect>("Playfield/Guard").AddChild(perfect);

		foreach (int i in Enumerable.Range(1, 4))
		{
			var notet = GetNode<PanelContainer>("Playfield/ChartSections/Section" + i);
			Keys.Add(new KeyL { Node = notet, hit = false , KeyCode = SettingsOperator.GetSetting($"Key{i - 1}").ToString()});
			notet.SelfModulate = Skin.Element.LaneNotes[i - 1] / 2;
		}
		PlayFieldPOS =GetNode<HBoxContainer>($"Playfield/ChartSections").Position;
		maxrndvalue = (int)(1000000 * ModsOperator.ProcessMultiplierByModList(ModsOperator.Mods));
	
		HitOffsets.Clear();
		Array.Fill(LastKeyPressTime, long.MinValue);
		QueuedPresses.Clear();
		GD.Print("Loading beatmap...");
		if (SettingsOperator.SessionConfig.BeatmapURL != null) ReloadBeatmap(SettingsOperator.SessionConfig.BeatmapURL.ToString());
		// If auto is enabled, it will make a Replay file with Auto being the player playing the beatmap. Before this it didn't make the replay file, it just plays.
		// I am doing this because it's simpler for me and don't have to worry about breaking auto (Qlutina)
		if (ModsOperator.Mods["auto"]) {
			Replay.ResetReplay(); // Resets the replay to be cleared before making the auto replay.
			SettingsOperator.SpectatorMode = true; // Enables Spectator Mode to play the Replay, without this it won't know it even existed lol :p
			foreach (NotesEn note in Notes)
			{
				var time = -note.timing - 50;
				var key = note.NotePart;
				Replay.AddReplay(time, key); // Adds the input into the Replay Cache.
			}
		}

		// LAN match: send the chart once so spectators can draw this playfield live.
		if (LanNetwork.InMatch) LanNetwork.Instance.SpecSendChart(Notes, (int)GetViewportRect().Size.Y);

		if (SettingsOperator.SpectatorMode)
		{
			AddChild(SpectatorPanel);
			Cursor.CursorVisible = true;
		}
		else
		{
			Replay.ResetReplay();
			Cursor.CursorVisible = false;
		}
		audioOffset = !SettingsOperator.SpectatorMode ? SettingsOperator.AudioOffset * 0.001f : 0f;
		GD.Print($"Spectator mode: {SettingsOperator.SpectatorMode}");
		GD.Print($"Your offset is currently: {audioOffset / 0.001}ms");
		
	}

	public Texture2D NoteSkinBack { get; set; }
	public Texture2D NoteSkinFore { get; set; }
	public Color chartclear = new Color(0.03f, 0.03f, 0.03f, 0.78f);
	public Color chartbeam = new Color(0.20f, 0.0f, 0.20f, 0.78f);
	
	private string _hitSoundPath;
	private string HitSoundPath => _hitSoundPath ??= "res://SelectableSkins/Slia/Sounds/" + (
		SettingsOperator.Gameplaycfg.SampleSet == SampleSet.Type[0] ? SampleSet.Normal.First() :
		SettingsOperator.Gameplaycfg.SampleSet == SampleSet.Type[1] ? SampleSet.Soft.First() :
		SettingsOperator.Gameplaycfg.SampleSet == SampleSet.Type[2] ? SampleSet.Drum.First() : "");

	public void hitnote(int Keyx, bool hit, int est)
	{
		var key = Keys[Keyx];
		if (!SettingsOperator.SpectatorMode && LanNetwork.InMatch) LanNetwork.Instance.SpecKey(Keyx, hit, est);
		if (hit)
		{
			Sample.PlaySample(HitSoundPath);
			key.Node.SelfModulate = Skin.Element.LaneNotes[Keyx];
			key.Ani?.Kill(); // Abort the previous animation
			key.Ani = Keys[Keyx].Node.CreateTween();
			key.Ani.TweenProperty(Keys[Keyx].Node, "self_modulate", Skin.Element.LaneNotes[Keyx] / 2, 0.5f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
			key.Ani.Play();
		}
		key.hit = hit;
	}

	public const float MAX_TIME_RANGE = 11485;
	public static float ComputeScrollTime(float scrollSpeed) => MAX_TIME_RANGE / scrollSpeed;
	public static float ReloadAccuracy(int max, int great, int meh, int bad)
	{
		return ((float)max + ((float)great / 2) + ((float)meh / 3)) / ((float)max + (float)great + (float)meh + (float)bad);
	}
	
	private readonly SongClock Clock = new SongClock();

    /// <summary>
	/// Game Clock
	/// </summary>
	public float GetRemainingTime(float delta = 0f)
	{
		double songTime = Clock.Update(AudioPlayer.Instance, delta);

		SettingsOperator.Gameplaycfg.Time = -WaitClock.TimeLeft + songTime;
		SettingsOperator.Gameplaycfg.Time -= startedtime;
		SettingsOperator.Gameplaycfg.Time += audioOffset;

		return (float)SettingsOperator.Gameplaycfg.Time;
	}

	/// <summary>
	/// Starts Playback
	/// </summary>
	private void StartPlay()
	{
		BreakOver();
		songstarted = true;
		AudioPlayer.Instance.Play();
	}
	
	private ReplayLegend Replayindex { get; set; }
	private float _lastReplayGametime = float.MinValue;
    /// <summary>
    /// If SettingsOperator.ReplayMode is enabled, this will check at the specified index of the replay cache to simulate a keypress in Spectator Mode.
    /// </summary>
	private void CheckReplayKey(int est)
	{
		if (!SettingsOperator.SpectatorMode || Replay.ReplayCache.Count == 0) return;
		// Release the keys pressed on the previous replay frame (visual only).
		for (int c = 0; c < Keys.Count; c++)
			if (Keys[c].hit) hitnote(c, false, est);
		// Every recorded press that is due is processed on its own recorded frame time,
		// so playback judges exactly like the original run (and like the server).
		var group = new List<int>();
		while (ReplayINT < Replay.ReplayCache.Count && Replay.ReplayCache[ReplayINT].Time <= est)
		{
			int t = Replay.ReplayCache[ReplayINT].Time;
			group.Clear();
			while (ReplayINT < Replay.ReplayCache.Count && Replay.ReplayCache[ReplayINT].Time == t)
			{
				int col = Replay.ReplayCache[ReplayINT].NoteTap;
				if ((uint)col < 4u) { group.Add(col); hitnote(col, true, t); }
				ReplayINT++;
			}
			ProcessInputFrame(t, group);
		}
	}
    /// <summary>
    /// This is the one that controls the Player input.
    /// </summary>
    public override void _Input(InputEvent @event)
    {
	    if (SettingsOperator.SpectatorMode) return;

	    if (!(@event is InputEventKey Key)) return;
	    var keycode = Key.Keycode.ToString();
	    for (int i = 0; i < 4; i++)
	    {
		    if (keycode == Keys[i].KeyCode)
		    {
			    bool pressed = @event.IsPressed();

			    // Presses are judged (and spam-checked) on the next game tick, in order.
			    if (pressed && !Finished) QueuedPresses.Add(i);

			    hitnote(i, pressed, (int)gametime);
		    }
	    }
    }

    /// <summary>
    /// Registers a miss on note <paramref name="i"/> (pp / combo / health penalty).
    /// </summary>
    private void RegisterMiss(int i)
    {
	    var Note = Notes[i];
	    Hittext(Skin.Element.JudgeMiss);
	    SettingsOperator.Gameplaycfg.Bad++;
	    if (SettingsOperator.Gameplaycfg.Combo > 50) Sample.PlaySample("res://SelectableSkins/Slia/Sounds/combobreak.wav");
	    SettingsOperator.Gameplaycfg.Combo = 0;
	    BadCombo++;
	    ComboAnimation();
	    HealthBar.Damage(5 * BadCombo);
	    if (HurtAnimation != null && HurtAnimation.IsRunning()) HurtAnimation.Stop();
	    if (GetTree().CurrentScene is CanvasItem canvasScene) canvasScene.Modulate = new Color(1f, 0.5f, 0.5f, 1f);
	    HurtAnimation = CreateTween();
	    HurtAnimation.TweenProperty(GetTree().CurrentScene, "modulate", new Color(1f, 1f, 1f, 1f), 0.5f)
		    .SetTrans(Tween.TransitionType.Cubic)
		    .SetEase(Tween.EaseType.Out);
	    Note.hit = true;
	    if (LanNetwork.InMatch) LanNetwork.Instance.SpecJudge(i, 3, gametime);
    }

    /// <summary>
    /// Called when a key is spammed. Registers a miss on the first unhit note in the column.
    /// </summary>
    private void TriggerSpamMiss(int column)
    {
	    for (int i = Math.Min(MaxNotes, NoteTick); i < Math.Min(MaxNotes, NoteTick + 256); i++)
	    {
		    if (Notes[i].NotePart == column && !Notes[i].hit)
		    {
			    RegisterMiss(i);
			    break;
		    }
	    }
    }

    /// <summary>
    /// Misses every unhit note that is more than MISS_LATE_MS late at time <paramref name="t"/>.
    /// </summary>
    private void MissPass(int t)
    {
	    for (int i = Math.Min(MaxNotes, NoteTick); i < Math.Min(MaxNotes, NoteTick + 256); i++)
	    {
		    var Note = Notes[i];
		    // Note.timing is -noteTime.
		    if (!Note.hit && Math.Min(t, SongEndMs) + Note.timing > MISS_LATE_MS) RegisterMiss(i);
	    }
    }

    /// <summary>
    /// One judgement frame at song time <paramref name="t"/> (ms) with the presses that
    /// arrived for it, in order. Order: late misses, then each press (spam check / record),
    /// then each accepted press hits the first unhit note in its column (note order).
    /// Mirrored exactly by the server's ppv2calc.py.
    /// </summary>
    private void ProcessInputFrame(int t, List<int> presses)
    {
	    MissPass(t);
	    if (presses.Count == 0) return;
	    Span<bool> accepted = stackalloc bool[4];
	    foreach (int col in presses)
	    {
		    if (!SettingsOperator.SpectatorMode) Replay.AddReplay(t, col);
		    long elapsed = (long)t - LastKeyPressTime[col];
		    if (elapsed >= 0 && elapsed < SPAM_COOLDOWN_MS)
		    {
			    // Too soon after the last accepted press: forced miss, press ignored.
			    TriggerSpamMiss(col);
			    continue;
		    }
		    LastKeyPressTime[col] = t;
		    accepted[col] = true;
	    }
	    for (int i = Math.Min(MaxNotes, NoteTick); i < Math.Min(MaxNotes, NoteTick + 256); i++)
	    {
		    var Note = Notes[i];
		    int col = Note.NotePart;
		    if (Note.hit || (uint)col >= 4u || !accepted[col]) continue;
		    accepted[col] = false; // the first unhit note in the column takes the press
		    int diff = t + Note.timing;
		    JudgeResult = JudgeDiff(diff, Note);
		    if (JudgeResult < 3)
		    {
			    SettingsOperator.Addms(diff + 50);
			    SettingsOperator.Gameplaycfg.ms = ComputeUnstableRate();
			    if (LanNetwork.InMatch) LanNetwork.Instance.SpecJudge(i, JudgeResult, gametime);
			    Note.hit = true;
		    }
	    }
    }

    public override void _ExitTree()
    {
	    // Only added to the tree when spectating; otherwise it was an orphan that leaked every play.
	    if (IsInstanceValid(SpectatorPanel) && !SpectatorPanel.IsInsideTree()) SpectatorPanel.Free();
	    if (Instance == this) Instance = null;
	    SettingsOperator.inGameplay = false;
	    DisplayServer.WindowSetTitle(oldtitle);
	    HurtAnimation?.Kill();
	    Modulate = new Color("#FFFFFF");
		Cursor.CursorVisible = true;
		SettingsOperator.SpectatorMode = false; // Disables Spectator mode.
		AudioPlayer.Instance.PitchScale = speedold;
    }

    public float gametime { get; set; }
    private float gametimeraw { get; set; }
	public int NoteTick { get; set; }
	private bool BreakTime { get; set; } = true;
	private int NoteBreakTiming { get; set; }
	private Break Break { get; set; }
	private Tween BreakBGTween { get; set; }
	private void BreakNow()
	{
		BreakTime = true;
		if (!DisableFadeSetting)
		{
			BreakBGTween?.Kill();
			BreakBGTween = GetTree().CreateTween();
			BreakBGTween.SetTrans(Tween.TransitionType.Cubic);
			BreakBGTween.SetEase(Tween.EaseType.Out);
			BreakBGTween.TweenProperty(Beatmap_Background, "self_modulate", new Color("7a7a7a"), 0.5f);
		}
		if (IsInstanceValid(Break))
		{
			Break?.QueueFree();
		}
		Break = GD.Load<PackedScene>("res://Panels/Screens/Break.tscn").Instantiate().GetNode<Break>(".");
		Break.MaxTick = NoteBreakTiming * 0.001;
		AddChild(Break);
		Break.BreakFinished += () =>
		{
			BreakOver();
		};
	}

	private void BreakOver()
	{
		if (!DisableFadeSetting)
		{
			BreakBGTween?.Kill();
			BreakBGTween = GetTree().CreateTween();
			BreakBGTween.SetTrans(Tween.TransitionType.Cubic);
			BreakBGTween.SetEase(Tween.EaseType.Out);
			BreakBGTween.TweenProperty(Beatmap_Background, "self_modulate", new Color(dim, dim, dim, 1f), 0.5f);
			BreakBGTween.TweenCallback(Callable.From((() => { BreakTime = false; })));
		}
		else
		{
			BreakTime = false;
		}
	}
	public float notex { get; set; }
private void _GameNoteTick(double delta)
	{
		gametime = GetRemainingTime(delta: (float)delta) / 0.001f;

		//var scrollspeed = ComputeScrollTime(int.Parse(SettingsOperator.GetSetting("scrollspeed").ToString())); // Scroll speed for the notes
		var scrollspeed = 1;
		// Gamenotes

		var viewportSize = 0f;
		if (IsInsideTree())
		{
			viewportSize = GetViewportRect().Size.Y;
		}
		_viewportHeight = viewportSize;

		
		if (!BreakTime && songstarted && Notes[Math.Min(Notes.Count - 1, NoteTick)].timing  + gametime + HitPoint <= -4500)
		{
			NoteBreakTiming = -(int)(Notes[Math.Min(Notes.Count - 1, NoteTick)].timing);
			BreakNow();
		}
		
		notex = gametime + HitPoint;
		// Judge this frame: queued key presses + late misses, all at one whole-ms time.
		int now = (int)Math.Floor(gametime);
		if (!Finished)
		{
			ProcessInputFrame(now, QueuedPresses);
			QueuedPresses.Clear();
		}

		for (int i = Math.Min(MaxNotes, NoteTick); i < Math.Min(MaxNotes, NoteTick + 256); i++)
		{
			var Note = Notes[i];
			if (Note.timing + notex > viewportSize + 150)
			{
				NoteTick++;
			}
		}
	}

	private Tween HUDTween { get; set; }
	private void HUDAni(bool value)
	{
		HUDTween?.Kill();
		HUDTween = CreateTween();
		HUDTween.SetEase(Tween.EaseType.Out);
		HUDTween.SetTrans(Tween.TransitionType.Cubic);
		if (value)
		{
			HUDTween.TweenProperty(HUD, "modulate:a", 0f, 0.5f);
			HUDTween.TweenCallback(Callable.From(() => HUD.Hide()));
		}
		else
		{
			HUDTween.TweenProperty(HUD, "modulate:a", 0f, 0f);
			HUDTween.TweenCallback(Callable.From(() => HUD.Show()));
			HUDTween.TweenProperty(HUD, "modulate:a", 1f, 0.5f);
		}
	}

	private float dim { get; set; }
	private bool DisableDarkeningSetting { get; set; }
	private bool DisableFadeSetting { get; set; }
	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		DisableDarkeningSetting = Check.CheckBoolValue(SettingsOperator.GetSetting("ddarkening").ToString());
		DisableFadeSetting = Check.CheckBoolValue(SettingsOperator.GetSetting("dfadeout").ToString());
		dim = 1f - (1f * (SettingsOperator.SessionConfig.Backgrounddim * 0.01f));
		// While playing: projected score (drops on misses). Once finished: the exact final score the server saves.
		score = Finished
			? ScoreCalc.ProcessScore(PerfNoteCount, SettingsOperator.Gameplaycfg.Max,
				SettingsOperator.Gameplaycfg.Great, SettingsOperator.Gameplaycfg.Meh, ComboPortion, ScoreMult)
			: PerformanceCalculator.LiveScore(SettingsOperator.Gameplaycfg.Max, SettingsOperator.Gameplaycfg.Great,
				SettingsOperator.Gameplaycfg.Meh, SettingsOperator.Gameplaycfg.Bad, ComboPortion, ScoreMult);
		if (!Finished)
			SettingsOperator.Gameplaycfg.pp = PerformanceCalculator.LivePerformance(MaxPp, PerfNoteCount,
				SettingsOperator.Gameplaycfg.Max, SettingsOperator.Gameplaycfg.Great, SettingsOperator.Gameplaycfg.Meh, SettingsOperator.Gameplaycfg.Bad);
		if ( (scoretween == null || !scoretween.IsRunning()) & scoreint != score)
		{
			scoretween?.Kill();
			scoretween = CreateTween();
			scoretween.TweenProperty(this, "scoreint",
				score, 0.3f);
			scoretween.Play();
		}
		SettingsOperator.Gameplaycfg.Score = scoreint; // Set the score of the player
		HitPoint = (int)Chart.Size.Y - 150;
		try
		{
			// DEATH

			if (HealthBar.Health == 0 && !Dead && !ModsOperator.Mods["no-fail"] && !LanNetwork.InMatch) // LAN matches are no-fail
			{
				Dead = !Dead;
				FailAnimation();
			}

			// End Game

			if (SettingsOperator.Gameplaycfg.TimeTotalGame - SettingsOperator.Gameplaycfg.Time < 0 && !Finished)
			{
				Finished = true;
				MissPass(SongEndMs); // final misses, before the result is saved / submitted
				SettingsOperator.Gameplaycfg.pp = FinalPerformance();
				score = ScoreCalc.ProcessScore(PerfNoteCount, SettingsOperator.Gameplaycfg.Max,
					SettingsOperator.Gameplaycfg.Great, SettingsOperator.Gameplaycfg.Meh, ComboPortion, ScoreMult);
				Replay.SaveReplay();
				ApiOperator.SS();
				if (!SettingsOperator.Marathon)
				{
					AddChild(GD.Load<PackedScene>("res://Panels/Screens/EndScreen.tscn").Instantiate());
				}
				else
				{
					SettingsOperator.MarathonID++;
					if (SettingsOperator.MarathonID >= SettingsOperator.MarathonMapPaths.Count - 1) GetNode<SceneTransition>("/root/Transition").Switch("res://Panels/Screens/ResultScreenv2.tscn");
					SettingsOperator.SelectSongID(SettingsOperator.MarathonMapPaths[SettingsOperator.MarathonID]);
					GetNode<SceneTransition>("/root/Transition").Switch("res://Panels/Screens/SongLoadingScreen.tscn");
				}
			}

			if (SettingsOperator.Gameplaycfg.Combo > SettingsOperator.Gameplaycfg.MaxCombo)
			{
				SettingsOperator.Gameplaycfg.MaxCombo = SettingsOperator.Gameplaycfg.Combo;
			}

			if (!BreakTime | DisableFadeSetting)
				Beatmap_Background.SelfModulate = new Color(dim, dim, dim);
			SettingsOperator.Gameplaycfg.Accuracy = ReloadAccuracy(SettingsOperator.Gameplaycfg.Max, SettingsOperator.Gameplaycfg.Great, SettingsOperator.Gameplaycfg.Meh, SettingsOperator.Gameplaycfg.Bad);
			if (Input.IsActionJustPressed("pausemenu") && SettingsOperator.SpectatorMode)
			{
				BeatmapBackground.FlashEnable = true;
				SettingsOperator.toppaneltoggle(true);
				GetNode<SceneTransition>("/root/Transition").Switch("res://Panels/Screens/song_select.tscn");
			}
			else if (Input.IsActionJustPressed("pausemenu") && LanNetwork.InMatch)
			{
				// Can't pause a LAN match (the others keep playing): Esc forfeits back to the room.
				LanNetwork.Instance.ReportFinished(quit: true);
				Cursor.CursorVisible = true;
				BeatmapBackground.FlashEnable = true;
				SettingsOperator.toppaneltoggle(true);
				AudioPlayer.Instance.PitchScale = speedold;
				GetNode<SceneTransition>("/root/Transition").Switch(LanNetwork.Instance.ReturnScene);
			}
			else if (Input.IsActionJustPressed("pausemenu") && !SettingsOperator.Marathon)
			{
				Cursor.CursorVisible = true;
				ShowPauseMenu();
			}
			else if (Input.IsActionJustPressed("pausemenu") && SettingsOperator.Marathon)
			{
				Cursor.CursorVisible = true;
				SettingsOperator.toppaneltoggle(true);
				BeatmapBackground.FlashEnable = true;
				GetNode<SceneTransition>("/root/Transition").Switch("res://Panels/Screens/MarathonMode.tscn");
				SettingsOperator.Marathon = false;
			}
			else if (Input.IsActionJustPressed("retry") && !SettingsOperator.Marathon && !LanNetwork.InMatch)
			{
				BeatmapBackground.FlashEnable = true;
				SettingsOperator.toppaneltoggle(true);
				MainScreenAnimation?.Kill();
				AudioPlayer.Instance.PitchScale = speedold;
				GetNode<SceneTransition>("/root/Transition").Switch("res://Panels/Screens/SongLoadingScreen.tscn");
			}
			else if (Input.IsActionJustPressed("HideHUD"))
			{
				SettingsOperator.SetSetting("hidehud", !HideHUD);
				HideHUD = !HideHUD;
				HUDAni(HideHUD);
			}
			//debugtext.Text = $"est: {est}\nDanceIndex:{DanceIndex}\nTimeindex:{dance[DanceIndex].time}";

			if ((int)gametime >= dance[DanceIndex].time + BeatmapBackground.bpm && BeatmapBackground.FlashEnable)
			{
				Transitioning(dance[DanceIndex].flash);
				IncreaseDanceIndex();
			}
			else if ((int)gametime >= dance[DanceIndex].time && !BeatmapBackground.FlashEnable)
			{
				Transitioning(dance[DanceIndex].flash);
				IncreaseDanceIndex();
			}

			if (!Finished) CheckReplayKey((int)Math.Floor(gametime));
			_GameNoteTick(delta);
			if (LanNetwork.InMatch) LanNetwork.Instance.SpecTime(gametime); // spectators follow this clock
		}
		catch (Exception e)
		{
			if (erroredout == false)
			{
				erroredout = true;
				GD.PrintErr(e);
				GD.PushError(e);
				Notify.Post("Can't play the beatmap because\n" + e.Message, Type:NotificationIcons.NotificationType.Warning);
				SettingsOperator.toppaneltoggle(true);
				BeatmapBackground.FlashEnable = true;
				GetNode<SceneTransition>("/root/Transition").Switch("res://Panels/Screens/song_select.tscn");
			}
		}
	}

	private void IncreaseDanceIndex()
	{
		if (DanceIndex + 1 < dance.Count)
		{
			DanceIndex++;
		}
	}
	private Tween DancePrepare { get; set; }
	private void Transitioning(bool value)
	{
		if (DisableDarkeningSetting) return;
		else if (value)
		{
			DancePrepare?.Kill();
			DancePrepare = GetTree().CreateTween();
			DancePrepare.TweenProperty(GetTree().CurrentScene, "modulate", new Color(1f, 1f, 1f, 0.6f), BeatmapBackground.bpm)
					.SetTrans(Tween.TransitionType.Cubic)
					.SetEase(Tween.EaseType.Out);
			DancePrepare.TweenCallback(Callable.From(() => BeatmapBackground.FlashEnable = true));
			DancePrepare.TweenProperty(GetTree().CurrentScene, "modulate", new Color(1f, 1f, 1f, 1f), 0.5)
					.SetTrans(Tween.TransitionType.Cubic)
					.SetEase(Tween.EaseType.Out);
			DancePrepare.Play();
		}
		else
		{
			BeatmapBackground.FlashEnable = value;
		}
	}
	private bool erroredout = false;
	public void Hittext(Texture2D Image)
	{
		hittext.Modulate = new Color(1f, 1f, 1f, 1f);
		hittext.Position = new Vector2(hittextoldpos.X, hittextoldpos.Y - 10);
		if (hittext.Texture != Image)
			hittext.Texture = Image;
		//tween.TweenProperty(hittext, "position", new Vector2(hittext.Position.X,hittext.Position.Y+10), 0.5f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		if (hittextani != null && hittextani.IsRunning())
		{
			hittextani.Stop();
		}

		hittextani = hittext.CreateTween();
		hittextani.Parallel().TweenProperty(hittext, "modulate", new Color(0f, 0f, 0f, 0f), 0.5).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		hittextani.Parallel().TweenProperty(hittext, "position", hittextoldpos, 0.5).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		hittextani.Play();
	}
	private Tween ComboTween { get; set; }

	private void ComboAnimation()
	{
		if (ComboCounter != null)
		{
			ComboCounter.Scale = new Vector2(1.0f, 1.2f);
			ComboTween?.Kill();
			ComboTween = CreateTween();
			ComboTween.TweenProperty(ComboCounter, "scale", new Vector2(1f, 1f), 0.5f)
				.SetTrans(Tween.TransitionType.Cubic)
				.SetEase(Tween.EaseType.Out);
			ComboTween.Play();
		}
	}
	private float _viewportHeight;
	/// <summary>
	/// Hit judgement for a press <paramref name="diff"/> ms from the note (negative = early).
	/// Returns 0 perfect, 1 great, 2 meh, 4 nothing (outside every window; the press is used up).
	/// The windows are shifted by nodeSize, as they always were. Mirrored in ppv2calc.py.
	/// </summary>
	public int JudgeDiff(int diff, NotesEn Note)
	{
		int x = diff + nodeSize;
		if (x > -SettingsOperator.PerfectJudge - 5 && x < SettingsOperator.PerfectJudge + 15)
		{
			SettingsOperator.Gameplaycfg.Max++;
			SettingsOperator.Gameplaycfg.Combo++;
			AddComboWeight(PerformanceCalculator.WPerfect);
			BadCombo = 0;
			Hittext(Skin.Element.JudgePerfect);
			ComboAnimation();
			HealthBar.Heal((5 * (SettingsOperator.Gameplaycfg.Combo / 100)) + 1);
			return 0;
		}
		if (x > -(SettingsOperator.GreatJudge / 2) - 5 && x < (SettingsOperator.GreatJudge / 2) + 15)
		{
			SettingsOperator.Gameplaycfg.Great++;
			SettingsOperator.Gameplaycfg.Combo++;
			AddComboWeight(PerformanceCalculator.WGreat);
			BadCombo = 0;
			Hittext(Skin.Element.JudgeGreat);
			ComboAnimation();
			HealthBar.Heal((3 * (SettingsOperator.Gameplaycfg.Combo / 300)) + 1);
			return 1;
		}
		if (x > -(SettingsOperator.MehJudge / 2) - 15 && x < (SettingsOperator.MehJudge / 2) + 15)
		{
			Hittext(Skin.Element.JudgeMeh);
			SettingsOperator.Gameplaycfg.Meh++;
			SettingsOperator.Gameplaycfg.Combo++;
			AddComboWeight(PerformanceCalculator.WMeh);
			BadCombo = 0;
			ComboAnimation();
			HealthBar.Heal((1 * (SettingsOperator.Gameplaycfg.Combo / 500)) + 1);
			return 2;
		}
		return 4;
	}
}
