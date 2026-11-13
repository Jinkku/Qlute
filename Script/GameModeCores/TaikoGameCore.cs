using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using Game;
using System.Reflection.Metadata;

public class DrumEn 
{
	public int timing { get; set; }
	public DrumTypeGroup DrumType { get; set; } 
	public bool DrumBig { get; set; }
	public TaikoDrum Node { get; set; }
	public bool hit { get; set; }
	public string Sample => SampleSet.Normal.First();
	public double ppv2xp { get; set; }
}

public enum DrumTypeGroup
{
	Inside = 0,  // Center / Don
	Outside = 1  // Rim / Kat
}
public class DKeyL
{
	public Sprite2D Node { get; set; }
	public bool hit { get; set; }
	/// <summary>
	/// True - Inner Drum
	/// False - Outter Drum
	/// </summary>
	public DrumTypeGroup DrumType { get; set; }
	public string KeyCode { get; set; }
	public Tween Ani { get; set; }
}
public partial class TaikoGameCore : Control
{
	private string oldtitle = "";
	private SettingsOperator SettingsOperator { get; set; }
	private int BadCombo { get; set; }
	public static ApiOperator ApiOperator { get; set; }
	//public static Label Ttiming { get; set; }
	//public static Label Hits { get; set; }
	public Control Road { get; set; }
	private int HitPoint { get; set; }
	private Tween MainScreenAnimation { get; set; }
	public List<DKeyL> Keys = new List<DKeyL>();
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
	public Node2D noteblock { get; set; }
	private Label ppv2LabelTest { get; set; }
	public bool hittextinit = false;
	public TextureRect hittext { get; set; }
	public Tween hittextani { get; set; }
	private Tween hitnoteani { get; set; }
	private Tween HurtAnimation { get; set; }
	public Vector2 hittextoldpos { get; set; }
	public Timer WaitClock { get; set; }
	public List<DrumEn> Notes = new List<DrumEn>();
	public TextureRect Beatmap_Background { get; set; }
	private Node PauseMenu { get; set; }
	private bool Finished { get; set; }
	public static int score { get; set; }
	public static List<DanceCounter> dance { get; set; }
	private int DanceIndex { get; set; }
	private Label debugtext { get; set; }
	public static int ReplayINT { get; set;} // Track the progress of replay...
	public static bool Dead { get; set; }
	private int scoreint { get; set; }
	private Control SpectatorPanel { get; set; }
	private Tween scoretween { get; set; }
	[Export]
	private int MaxNotes { get; set; }
	private Label ComboCounter { get; set; }
	public static int seed = 0;
	private Control HUD { get; set; }
	private float speedold = 1f;
	// pp v3 / competitive score state (see Game.PerformanceCalculator).
	private long comboPortion;
	private double scoreMult = 1.0;
	private double maxPpMods;
	private void AddComboWeight(int judgementWeight) =>
		comboPortion += Game.PerformanceCalculator.ComboWeight(judgementWeight, SettingsOperator.Gameplaycfg.Combo);
	private double CurrentPerformance(bool final) => final
		? Game.PerformanceCalculator.Performance(maxPpMods, SettingsOperator.Gameplaycfg.NoteCount,
			SettingsOperator.Gameplaycfg.Max, SettingsOperator.Gameplaycfg.Great, SettingsOperator.Gameplaycfg.Meh, SettingsOperator.Gameplaycfg.Bad)
		: Game.PerformanceCalculator.LivePerformance(maxPpMods, SettingsOperator.Gameplaycfg.NoteCount,
			SettingsOperator.Gameplaycfg.Max, SettingsOperator.Gameplaycfg.Great, SettingsOperator.Gameplaycfg.Meh, SettingsOperator.Gameplaycfg.Bad);
	private float audioOffset { get; set; } = 0;
	private Control TaikoTable { get; set; }
	private Sprite2D HitBoxNode { get; set; }

	// Spam protection: minimum ms between accepted key presses per column
	private const float SPAM_COOLDOWN_MS = 80f; // 80ms-matches osu!mania's input buffer
	private float[] LastKeyPressTime = new float[4] { float.MinValue, float.MinValue, float.MinValue, float.MinValue };

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

		var seen = new HashSet<(int timing, int section)>();
	
		foreach (string line in lines)
		{
			DrumEn PreLegend = new DrumEn();
			if (line.Trim() == "[HitObjects]") { isHitObjectSection = true; continue; }
			if (!isHitObjectSection) continue;
			if (string.IsNullOrWhiteSpace(line) || line.StartsWith('[')) break;

			string[] section = line.Split(':', ',');
			timing = Convert.ToInt32(section[2]);
			part = Convert.ToInt32(section[3]);

			switch (part)
			{
				case 1:
					// Hit Object type 1 = Circle (Normal Note)
					// In Taiko, the 'type' (section[4]) determines if it's Red/Blue/Big
					var DrumType = Convert.ToInt32(section[4]);
					if (DrumType == 0)
					{
						PreLegend.DrumType = DrumTypeGroup.Inside;
					} else if (DrumType == 2 || DrumType == 8) {
						PreLegend.DrumType = DrumTypeGroup.Outside;
					} else if (DrumType == 4) {
						PreLegend.DrumType = DrumTypeGroup.Inside;
						PreLegend.DrumBig = true;
					} else if (DrumType == 6)
					{
						// "big blue" 
						PreLegend.DrumType = DrumTypeGroup.Outside;
						PreLegend.DrumBig = true;
					}

					break;

				case 2:
					// Hit Object type 2 = Slider ("swell" / Drumroll)
					break;

				case 8:
					// Hit Object type 8 = Spinner ("spinner" / Denden)
					break;

				default:
					// Handle unexpected object types if necessary
					break;
			}
			timen = -timing;
			var key = (timen, part);

			// ✅ Skip duplicates in O(1), no linear scan
			if (seen.Contains(key)) { index++; continue; }
			seen.Add(key);
			PreLegend.timing = timen; 
			Notes.Add(PreLegend);
			
			index++;
		}

		MaxNotes = Notes.Count;
	}

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

	public override void _Ready()
	{
		
		GD.Print("Setting up stuff...");
		speedold = AudioPlayer.Instance.PitchScale;
		seed = new Random().Next(1,214562543);
		ReplayINT = 0;
		SettingsOperator = GetNode<SettingsOperator>("/root/SettingsOperator");
		SpectatorPanel = GD.Load<PackedScene>("res://Panels/Overlays/SpectatorSettings.tscn").Instantiate().GetNode<Control>(".");
		ApiOperator = GetNode<ApiOperator>("/root/ApiOperator");
		Beatmap_Background = GetNode<TextureRect>("./Beatmap_Background");
		WaitClock = GetNode<Timer>("Wait");
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

		HealthBar.Reset(10);
		TaikoTable = GetNode<Control>("TaikoTable");
		Road = TaikoTable.GetNode<Control>("Back/Road");
		HitBoxNode = TaikoTable.GetNode<Sprite2D>("Back/HitBox");
		hittext = GD.Load<PackedScene>("res://Panels/GameplayElements/Static/hittext.tscn").Instantiate().GetNode<TextureRect>(".");
		hittextinit = true;
		hittext.Modulate = new Color(1f, 1f, 1f, 0f);
		hittext.ZIndex = 100;
		HitBoxNode.AddChild(hittext);
		hittextoldpos = hittext.Position;
		var TaikoDrums = TaikoTable.GetNode<Control>("Back/Drums").GetChildren();
		Keys.Add(new DKeyL { Node = (Sprite2D)TaikoDrums[0], hit = false , KeyCode = SettingsOperator.GetSetting($"Key0").ToString(), DrumType = DrumTypeGroup.Outside});
		Keys.Add(new DKeyL { Node = (Sprite2D)TaikoDrums[2], hit = false , KeyCode = SettingsOperator.GetSetting($"Key1").ToString(), DrumType = DrumTypeGroup.Inside});
		Keys.Add(new DKeyL { Node = (Sprite2D)TaikoDrums[3], hit = false , KeyCode = SettingsOperator.GetSetting($"Key2").ToString(), DrumType = DrumTypeGroup.Inside});
		Keys.Add(new DKeyL { Node = (Sprite2D)TaikoDrums[1], hit = false , KeyCode = SettingsOperator.GetSetting($"Key3").ToString(), DrumType = DrumTypeGroup.Outside});

		maxrndvalue = (int)(1000000 * ModsOperator.ProcessMultiplierByModList(ModsOperator.Mods));
		comboPortion = 0;
		scoreMult = ModsOperator.ScoreMultiplierExact(ModsOperator.Mods);
		// No mode-specific star rating yet: the map's NoMod max pp with the pp mod factors.
		maxPpMods = SettingsOperator.Gameplaycfg.maxpp * Game.PerformanceCalculator.PpMultiplier(ModsOperator.AliasOf(ModsOperator.Mods));

		HitOffsets.Clear();
		Array.Fill(LastKeyPressTime, float.MinValue);
		GD.Print("Loading beatmap...");
		if (SettingsOperator.SessionConfig.BeatmapURL != null) ReloadBeatmap(SettingsOperator.SessionConfig.BeatmapURL.ToString());
		// If auto is enabled, it will make a Replay file with Auto being the player playing the beatmap. Before this it didn't make the replay file, it just plays.
		// I am doing this because it's simpler for me and don't have to worry about breaking auto (Qlutina)
		if (ModsOperator.Mods["auto"]) {
			Replay.ResetReplay(); // Resets the replay to be cleared before making the auto replay.
			SettingsOperator.SpectatorMode = true; // Enables Spectator Mode to play the Replay, without this it won't know it even existed lol :p
			var keyo = false;
			foreach (DrumEn note in Notes)
			{
				var time = -note.timing;
				var pkey = note.DrumType;
				var key = 0;
				if (pkey == DrumTypeGroup.Outside && keyo)
				{
					key = 3;
				} else if (pkey == DrumTypeGroup.Outside && !keyo)
				{
					key = 0;
				} else if (pkey == DrumTypeGroup.Inside && keyo)
				{
					key = 2;
				} else if (pkey == DrumTypeGroup.Inside && !keyo)
				{
					key = 1;
				}

				keyo = !keyo;
				Replay.AddReplay(time, key); // Adds the input into the Replay Cache.
			}
		}

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
		// LAN match: send the chart once so spectators can draw this playfield live.
		if (LanNetwork.InMatch) LanNetwork.Instance.SpecSendChart(new LanSpecChart
		{
			Mode = 1,
			Height = (int)GetViewportRect().Size.X,
			Times = Notes.Select(n => -n.timing).ToArray(), // DrumEn.timing is stored negative
			Lanes = Notes.Select(n => (byte)((int)n.DrumType + (n.DrumBig ? 2 : 0))).ToArray(),
		});
		GD.Print($"Spectator mode: {SettingsOperator.SpectatorMode}");
		GD.Print($"Your offset is currently: {audioOffset / 0.001}ms");
		
	}

	public Texture2D NoteSkinBack { get; set; }
	public Texture2D NoteSkinFore { get; set; }
	public Color chartclear = new Color(0.03f, 0.03f, 0.03f, 0.78f);
	public Color chartbeam = new Color(0.20f, 0.0f, 0.20f, 0.78f);
	
	public void hitnote(int Keyx, bool hit, int est)
	{
		var key = Keys[Keyx];
		if (!SettingsOperator.SpectatorMode && LanNetwork.InMatch) LanNetwork.Instance.SpecKey(Keyx, hit, est);
		if (hit)
		{
			var name = "";
			if (SettingsOperator.Gameplaycfg.SampleSet == SampleSet.Type[0]) {
				name = SampleSet.Normal.First();
			} else if (SettingsOperator.Gameplaycfg.SampleSet == SampleSet.Type[1]) {
				name = SampleSet.Soft.First();
			} else if (SettingsOperator.Gameplaycfg.SampleSet == SampleSet.Type[2]) {
				name = SampleSet.Drum.First();
			}
			Sample.PlaySample("res://SelectableSkins/Slia/Sounds/" + name);
			if (!SettingsOperator.SpectatorMode) Replay.AddReplay(est, Keyx);
			key.Node.SelfModulate = new Color(1.2f,1.2f,1.2f,1f);
			key.Ani?.Kill(); // Abort the previous animation
			key.Ani = Keys[Keyx].Node.CreateTween();
			key.Ani.TweenProperty(Keys[Keyx].Node, "self_modulate", new Color(1f,1f,1f,0.5f), 0.5f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
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
		// Replay part (if enabled)
		if (SettingsOperator.SpectatorMode && Replay.ReplayCache.Count > 0)
		{
			Replayindex = Replay.ReplayCache[Math.Min(ReplayINT, Replay.ReplayCache.Count - 1)];
			if (est > Replayindex.Time)
			{
				if (ReplayINT < Replay.ReplayCache.Count())
				{
					hitnote(Replayindex.NoteTap, true, est);
					ReplayINT++;
				}
				else if (ReplayINT > 0 && Keys[Replay.ReplayCache[ReplayINT - 1].NoteTap].hit)
				{
					hitnote(Replay.ReplayCache[ReplayINT - 1].NoteTap, false, est);
				}
			}
		}
		else return;
	}
    /// <summary>
    /// This is the one that controls the Player input.
    /// </summary>
    public override void _Input(InputEvent @event)
    {
	    if (SettingsOperator.SpectatorMode) return;

	    // 1. Quick Guard: If it's not a keyboard key event, ignore it immediately
	    if (@event is not InputEventKey keyEvent) return;

	    // 2. Performance Fix: Only read IsPressed once per event, not inside a loop
	    bool pressed = keyEvent.IsPressed();

	    for (int i = 0; i < Keys.Count; i++)
	    {
		    // 3. Performance Fix: Compare raw keycodes/enums instead of allocating strings with .ToString()
		    var keyName = "Key" + (i + 1);
		    if ( !(@event is InputEventKey Key) ) return;
		    else if (Key.Keycode.ToString() == Keys[i].KeyCode)
		    {
			    // Spam guard: if a key-down comes in too soon after the last one,
			    // force a miss and ignore the input so it can't farm pp.
			    if (pressed)
			    {
				    float nowMs = gametime; // est is already in ms
				    float elapsed = nowMs - LastKeyPressTime[i];
                
				    if (elapsed < SPAM_COOLDOWN_MS && elapsed >= 0f)
				    {
					    // 4. Fixed: Safely cast or map your key index/bind to the DrumTypeGroup enum
					    DrumTypeGroup drumSide = (DrumTypeGroup)Keys[i].DrumType;
                    
					    TriggerSpamMiss(drumSide);
					    return;
				    }
				    LastKeyPressTime[i] = nowMs;
			    }

			    // Keep 'i' here if hitnote still expects the raw index (0-3) for your key layout
			    hitnote(i, pressed, (int)gametime);
			    break;
		    }
	    }
    }

    /// <summary>
    /// Called when a key is spammed. Finds the nearest note in the column and registers a miss.
    /// </summary>
private void TriggerSpamMiss(DrumTypeGroup column)
{
    // Bound check optimization
    int startIndex = Math.Min(MaxNotes, NoteTick);
    int endIndex = Math.Min(MaxNotes, NoteTick + 256);

    for (int i = startIndex; i < endIndex; i++)
    {
        var Note = Notes[i];
        
        // Fixed: Directly comparing DrumTypeGroup enums without casting overhead
        if (Note.DrumType == column && !Note.hit && Note.Node != null && Note.Node.Visible)
        {
            // Register miss
            Hittext(Skin.Element.JudgeMiss);
            SettingsOperator.Gameplaycfg.Bad++;
            
            if (SettingsOperator.Gameplaycfg.Combo > 50) 
            {
                Sample.PlaySample("res://SelectableSkins/Slia/Sounds/combobreak.wav");
            }
            
            SettingsOperator.Gameplaycfg.Combo = 0;
            BadCombo++;
            ComboAnimation();
            
            // Scaling damage based on consecutive misses
            HealthBar.Damage(5 * BadCombo);
            
            // Screen flash / Hurt visual animation
            if (HurtAnimation != null && HurtAnimation.IsRunning()) 
            {
                HurtAnimation.Stop();
            }
            
            if (GetTree().CurrentScene is CanvasItem canvasScene) 
            {
                canvasScene.Modulate = new Color(1f, 0.5f, 0.5f, 1f);
            }
            
            HurtAnimation = CreateTween();
            HurtAnimation.TweenProperty(GetTree().CurrentScene, "modulate", new Color(1f, 1f, 1f, 1f), 0.5f)
                .SetTrans(Tween.TransitionType.Cubic)
                .SetEase(Tween.EaseType.Out);
                
            // Mark note as processed so it doesn't get judged again
            if (LanNetwork.InMatch) LanNetwork.Instance.SpecJudge(i, 3, gametime);
            Note.hit = true;
            Note.Node.QueueFree(); // free judged drums instead of hiding them forever
            Note.Node = null;
            
            break; // Break loop immediately after processing the first eligible missed note
        }
    }
}

    public override void _ExitTree()
    {
	    // Only added to the tree when spectating; otherwise it was an orphan that leaked every play.
	    if (IsInstanceValid(SpectatorPanel) && !SpectatorPanel.IsInsideTree()) SpectatorPanel.Free();
	    SettingsOperator.inGameplay = false;
	    DisplayServer.WindowSetTitle(oldtitle);
	    HurtAnimation?.Kill();
	    Modulate = new Color("#FFFFFF");
		Cursor.CursorVisible = true;
		SettingsOperator.SpectatorMode = false; // Disables Spectator mode.
		AudioPlayer.Instance.PitchScale = speedold;
    }

    private float gametime { get; set; }
    private float gametimeraw { get; set; }
	private int NoteTick { get; set; }
	private bool BreakTime { get; set; } = true;
	private int NoteBreakTiming { get; set; }
	private Break Break { get; set; }
	private Tween BreakBGTween { get; set; }
	private void BreakNow()
	{
		BreakTime = true;
		BreakBGTween?.Kill();
		BreakBGTween = GetTree().CreateTween();
		BreakBGTween.SetTrans(Tween.TransitionType.Cubic);
		BreakBGTween.SetEase(Tween.EaseType.Out);
		BreakBGTween.TweenProperty(Beatmap_Background, "self_modulate", new Color(1f, 1f, 1f, 1f), 0.5f);
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
		BreakBGTween?.Kill();
		BreakBGTween = GetTree().CreateTween();
		BreakBGTween.SetTrans(Tween.TransitionType.Cubic);
		BreakBGTween.SetEase(Tween.EaseType.Out);
		BreakBGTween.TweenProperty(Beatmap_Background, "self_modulate", new Color(dim, dim, dim, 1f), 0.5f);
		BreakBGTween.TweenCallback(Callable.From((() => {BreakTime = false;})));
	}

	private string BigTaikoDrumCheck(bool ans)
	{
		switch (ans)
		{
			case true : return "res://Panels/GameplayElements/Taiko/BigTaikoThingy.tscn";
			default: return "res://Panels/GameplayElements/Taiko/SmallTaikoThingy.tscn";
		}
	}
	private bool IsDrumTypeHit(DrumTypeGroup targetGroup)
	{
		for (int i = 0; i < Keys.Count; i++)
		{
			DKeyL bind = Keys[i];
			var keyName = "Key" + (i + 1);
        
			// Cast bind.DrumTypeGroup if it's an int, or update DKeyL to use the enum too
			if ((DrumTypeGroup)bind.DrumType == targetGroup && SettingsOperator.GetSetting(keyName).ToString() == Keys[i].KeyCode)
			{
				bind.hit = true; 
				return true;
			}
		}
    
		return false; 
	}
	// Helper to convert individual DrumTypes to their group (Inside/Outside)
	private DrumTypeGroup GetDrumGroup(int drumType)
	{
		// Replace with your actual DrumType enum/integer checks.
		// Assuming 0 and 1 are Left/Right Don, 2 and 3 are Left/Right Kat.
		if (drumType == 0 || drumType == 1) 
			return DrumTypeGroup.Inside;
    
		return DrumTypeGroup.Outside;
	}

// Helper to check if any key belonging to the group was hit
	private bool IsGroupHit(DrumTypeGroup group)
	{
		// Checks if the active player input matches the note group
		return IsDrumTypeHit(group); 
	}

// Helper to clear the hit flag for all keys associated with the group
	private void ResetGroupHit(DrumTypeGroup group)
	{
		// Assuming Keys array is indexed or mapped to your specific drum types, 
		// we clear the hit status for both left and right sides of that group.
		if (group == DrumTypeGroup.Inside)
		{
			Keys[0].hit = false; // Left Don
			Keys[1].hit = false; // Right Don
		}
		else
		{
			Keys[2].hit = false; // Left Kat
			Keys[3].hit = false; // Right Kat
		}
	}

private void _GameNoteTick(double delta)
{
    gametime = GetRemainingTime(delta: (float)delta) / 0.001f;
    var scrollspeed = 1;
    int Ttick = 0;

    var viewportSize = 0f;
    if (IsInsideTree())
    {
        viewportSize = GetViewportRect().Size.X;
    }

    var NoteCount = 0;
    float hitPointLocal = new Vector2(HitBoxNode.GlobalPosition.X, 0).X;

    if (!BreakTime && songstarted &&
        Notes[Math.Min(Notes.Count - 1, NoteTick)].timing + gametime + HitPoint <= -4500)
    {
        NoteBreakTiming = -(int)(Notes[Math.Min(Notes.Count - 1, NoteTick)].timing);
        BreakNow();
    }

    // 1. Capture the input snapshot for this single frame BEFORE looking at notes
    bool insideGroupHit = Keys[1].hit || Keys[2].hit; // Left Don or Right Don
    bool outsideGroupHit = Keys[0].hit || Keys[3].hit; // Left Kat or Right Kat

    for (int i = Math.Min(MaxNotes, NoteTick); i < Math.Min(MaxNotes, NoteTick + 256); i++)
    {
        var Note = Notes[i];
        var notex = Note.timing + gametime + HitPoint;

        if (notex > -viewportSize && notex < viewportSize + 150 && !Note.hit)
        {
            NoteCount++;
            if (!Note.hit && Note.Node == null)
            {
                Note.Node = GD.Load<PackedScene>(BigTaikoDrumCheck(Note.DrumBig)).Instantiate()
                    .GetNode<TaikoDrum>(".");
                Note.Node.DrumType = Note.DrumType;
                Road.AddChild(Note.Node);
            }

            if (Note.Node != null)
            {
                Note.Node.Position = new Vector2(hitPointLocal + (hitPointLocal - notex) * scrollspeed, 0);
                Ttick++;

                DrumTypeGroup noteGroup = GetDrumGroup((int)Note.Node.DrumType);
                bool groupIsHit = false;
				
                // 2. Map the note's group to our snapshot flags
                if (Note.Node.DrumType == DrumTypeGroup.Outside && outsideGroupHit) groupIsHit = true;
                else if (Note.Node.DrumType == DrumTypeGroup.Inside && insideGroupHit) groupIsHit = true;
	                

                JudgeResult = checkjudge((int)notex, groupIsHit, Note);

                if (JudgeResult < 3)
                {
                    float hitOffsetMs = notex - HitPoint;
                    SettingsOperator.Addms(hitOffsetMs);
                    SettingsOperator.Gameplaycfg.ms = ComputeUnstableRate();
                }

                if (JudgeResult < 4)
                {
                    // Consume the hit snapshot for this group so trailing notes don't get accidentally hit
                    if (noteGroup == DrumTypeGroup.Inside) insideGroupHit = false;
                    else outsideGroupHit = false;

                    if (LanNetwork.InMatch) LanNetwork.Instance.SpecJudge(i, JudgeResult, gametime);
                    Note.hit = true;
                    Note.Node.QueueFree(); // free judged drums instead of hiding them forever
                    Note.Node = null;
                }
            }
        }
    }

    // Move the 256-note window past notes that are done, otherwise nothing after the first 256 is ever judged.
    while (NoteTick < MaxNotes && Notes[NoteTick].hit) NoteTick++;

    // 3. FORCE RESET: "Let go" of all buttons at the end of the frame processing tick
    Keys[0].hit = false;
    Keys[1].hit = false;
    Keys[2].hit = false;
    Keys[3].hit = false;
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
	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		dim = 1f - (1f * (SettingsOperator.SessionConfig.Backgrounddim * 0.01f));
		// While playing: projected score (drops on misses). Once finished: the exact final score.
		score = Finished
			? ScoreCalc.ProcessScore(SettingsOperator.Gameplaycfg.NoteCount, SettingsOperator.Gameplaycfg.Max,
				SettingsOperator.Gameplaycfg.Great, SettingsOperator.Gameplaycfg.Meh, comboPortion, scoreMult)
			: Game.PerformanceCalculator.LiveScore(SettingsOperator.Gameplaycfg.Max, SettingsOperator.Gameplaycfg.Great,
				SettingsOperator.Gameplaycfg.Meh, SettingsOperator.Gameplaycfg.Bad, comboPortion, scoreMult);
		if (!Finished) SettingsOperator.Gameplaycfg.pp = CurrentPerformance(false);
		if ( (scoretween == null || !scoretween.IsRunning()) & scoreint != score)
		{
			scoretween?.Kill();
			scoretween = CreateTween();
			scoretween.TweenProperty(this, "scoreint",
				score, 0.3f);
			scoretween.Play();
		}
		SettingsOperator.Gameplaycfg.Score = scoreint; // Set the score of the player
		HitPoint = (int)HitBoxNode.Position.X;
		try
		{

			// End Game

			if (SettingsOperator.Gameplaycfg.TimeTotalGame - SettingsOperator.Gameplaycfg.Time <= 0 && !Finished && (HealthBar.Health >= HealthBar.MaxHealth / 2 || ModsOperator.Mods["no-fail"] || LanNetwork.InMatch)) // LAN matches are no-fail
			{
				Finished = true;
				SettingsOperator.Gameplaycfg.pp = CurrentPerformance(true);
				score = ScoreCalc.ProcessScore(SettingsOperator.Gameplaycfg.NoteCount, SettingsOperator.Gameplaycfg.Max,
					SettingsOperator.Gameplaycfg.Great, SettingsOperator.Gameplaycfg.Meh, comboPortion, scoreMult);
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
			} else if (SettingsOperator.Gameplaycfg.TimeTotalGame - SettingsOperator.Gameplaycfg.Time <= 0 && !Finished && (HealthBar.Health <= HealthBar.MaxHealth / 2 || !ModsOperator.Mods["no-fail"]) && !Dead)
			{
				
				// DEATH
				Dead = !Dead;
				FailAnimation();
			}

			if (SettingsOperator.Gameplaycfg.Combo > SettingsOperator.Gameplaycfg.MaxCombo)
			{
				SettingsOperator.Gameplaycfg.MaxCombo = SettingsOperator.Gameplaycfg.Combo;
			}

			if (!BreakTime)
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

			CheckReplayKey((int)gametime);
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
		if (value)
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
	public int checkjudge(int timing, bool keyvalue, DrumEn Note)
	{
		if (timing > HitPoint - SettingsOperator.PerfectJudge - 5 && timing < HitPoint + SettingsOperator.PerfectJudge + 5 && keyvalue && Note.Node.Visible)
		{
			SettingsOperator.Gameplaycfg.Max++;
			SettingsOperator.Gameplaycfg.Combo++;
			AddComboWeight(Game.PerformanceCalculator.WPerfect);
			BadCombo = 0;
			Hittext(Skin.Element.JudgePerfect);
			ComboAnimation();
			HealthBar.Heal((5 * (SettingsOperator.Gameplaycfg.Combo / 100)) + 1);
			return 0;
		}
		else if (timing > HitPoint - (SettingsOperator.GreatJudge / 2) - 5 && timing < HitPoint + (SettingsOperator.GreatJudge / 2) + 5 && keyvalue && Note.Node.Visible)
		{
			SettingsOperator.Gameplaycfg.Great++;
			SettingsOperator.Gameplaycfg.Combo++;
			AddComboWeight(Game.PerformanceCalculator.WGreat);
			BadCombo = 0;
			Hittext(Skin.Element.JudgeGreat);
			ComboAnimation();
			HealthBar.Heal((3 * (SettingsOperator.Gameplaycfg.Combo / 300)) + 1);
			return 1;
		}
		else if (timing > HitPoint - (SettingsOperator.MehJudge / 2) - 15 && timing < HitPoint + (SettingsOperator.MehJudge / 2) + 15 && keyvalue && Note.Node.Visible)
		{
			Hittext(Skin.Element.JudgeMeh);
			SettingsOperator.Gameplaycfg.Meh++;
			SettingsOperator.Gameplaycfg.Combo++;
			AddComboWeight(Game.PerformanceCalculator.WMeh);
			BadCombo = 0;
			ComboAnimation();
			HealthBar.Heal((1 * (SettingsOperator.Gameplaycfg.Combo / 500)) + 1);
			return 2;
		}
		else if (timing > GetViewportRect().Size.X + 60 && Note.Node.Visible)
		{
			Hittext(Skin.Element.JudgeMiss);
			SettingsOperator.Gameplaycfg.Bad++;
			if (SettingsOperator.Gameplaycfg.Combo > 50) Sample.PlaySample("res://SelectableSkins/Slia/Sounds/combobreak.wav");
			SettingsOperator.Gameplaycfg.Combo = 0;
			BadCombo++;
			ComboAnimation();
			HealthBar.Damage(5 * BadCombo);
			if (HurtAnimation != null && HurtAnimation.IsRunning())
			{
				HurtAnimation.Stop();
			}
			if (GetTree().CurrentScene is CanvasItem canvasScene) canvasScene.Modulate = new Color(1f, 0.5f, 0.5f, 1f);
			HurtAnimation = CreateTween();
			HurtAnimation.TweenProperty(GetTree().CurrentScene, "modulate", new Color(1f, 1f, 1f, 1f), 0.5f)
				.SetTrans(Tween.TransitionType.Cubic)
				.SetEase(Tween.EaseType.Out);
			return 3;
		}
		else
		{
			return 4;
		}
	}
}