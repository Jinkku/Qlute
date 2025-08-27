using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
public class NotesEn {
	public int timing {get;set;}
	public int NoteSection {get;set;}
	public Sprite2D Node {get;set;}
	public bool hit {get;set;}
}
public class KeyL
{
	public PanelContainer Node { get; set; }
	public bool hit { get; set; }
	public Tween Ani { get; set; }
}

public partial class Gameplay : Control
{
	// Called when the node enters the scene tree for the first time.
	private SettingsOperator SettingsOperator { get; set; }
	public static ApiOperator ApiOperator { get; set; }
	//public static Label Ttiming { get; set; }
	//public static Label Hits { get; set; }
	public HBoxContainer Chart { get; set; }
	private int HitPoint { get; set; }
	private Tween MainScreenAnimation { get; set; }
	public List<KeyL> Keys = new List<KeyL>();
	public int JudgeResult = -1;
	public int nodeSize = 54;
	public ColorRect meh { get; set; }
	public ColorRect great { get; set; }
	public ColorRect perfect { get; set; }
	public float mshit { get; set; }
	public float mshitold { get; set; }
	public long startedtime { get; set; } = 0;
	public bool songstarted = false;
	public Node2D noteblock { get; set; }
	public bool hittextinit = false;
	public Label hittext { get; set; }
	public Tween hittextani { get; set; }
	private Tween hitnoteani { get; set; }
	private Tween HurtAnimation { get; set; }
	public Vector2 hittextoldpos { get; set; }
	public Timer WaitClock { get; set; }
	public static List<NotesEn> Notes = new List<NotesEn>();
	public int Noteindex = 1;
	public TextureRect Beatmap_Background { get; set; }
	private Node PauseMenu { get; set; }
	private bool Finished { get; set; }
	private int score { get; set; }
	public static IEnumerable<DanceCounter> dance { get; set; }
	private int DanceIndex { get; set; }
	private Label debugtext { get; set; }
	public static int ReplayINT { get; set;} // Track the progress of replay...
	public static bool Dead { get; set; }
	private int scoreint { get; set; }
	private Control SpectatorPanel { get; set; }
	private Tween scoretween { get; set; }
	private void ShowPauseMenu()
	{
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
		MainScreenAnimation.Parallel().TweenProperty(this, "rotation", 0.25f, Interval).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		MainScreenAnimation.Parallel().TweenProperty(AudioPlayer.Instance, "pitch_scale", 0.01f, Interval).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		MainScreenAnimation.Parallel().TweenProperty(this, "scale", Scale * 0.9f, Interval).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		MainScreenAnimation.Connect("finished", new Callable(this, nameof(ShowPauseMenu)));
	}
	private int maxrndvalue { get; set; }

	private void CheckNPCValues()
	{
		if (ModsOperator.Mods["npc"])
		{
			Random rnd = new Random();
			for (int i = 0; i < ApiOperator.LeaderboardList.Count; i++)
			{
				var value = rnd.Next(1, maxrndvalue);
				var entry = ApiOperator.LeaderboardList[i];
				if (value > maxrndvalue * 0.0004 && entry.Active)
				{
					entry.MAX++;
					entry.rcombo++;
				}
				else if (value > maxrndvalue * 0.00003 && entry.Active)
				{
					entry.GOOD++;
					entry.rcombo++;
				}
				else if (value > maxrndvalue * 0.000002 && entry.Active)
				{
					entry.MEH++;
					entry.rcombo++;
				}
				else if (entry.Active)
				{
					entry.BAD++;
					entry.rcombo = 0;
				}
				entry.combo = Math.Max(entry.rcombo, entry.combo);
				entry.Accuracy = ReloadAccuracy(entry.MAX, entry.GOOD, entry.MEH, entry.BAD);

				entry.score = Get_Score(SettingsOperator.Get_ppvalue(entry.MAX, entry.GOOD, entry.MEH, entry.BAD, ModsMulti.multiplier, entry.combo), SettingsOperator.Gameplaycfg.maxpp, ModsMulti.multiplier) / (1 + i);
			}
		}
	}

	public static void reload_npcleaderboard()
	{
		ApiOperator.LeaderboardList.Clear();
		for (int i = 0; i < 51; i++)
		{
			ApiOperator.LeaderboardList.Add(new LeaderboardEntry
			{
				username = $"NPC-{i.ToString("N0")}",
				points = 0,
				score = 0,
				combo = 0,
				MAX = 0,
				GOOD = 0,
				MEH = 0,
				BAD = 0,
				mods = "",
				time = 0,
				Active = true
			});

		}
	}

	public static void ReloadBeatmap(string filepath)
	{
		Notes.Clear();
		using var file = FileAccess.Open(filepath, FileAccess.ModeFlags.Read);
		var text = file.GetAsText();
		var lines = text.Split("\n");
		var part = 0;
		var timing = 0;
		var t = "";
		var timen = -1;
		var isHitObjectSection = false;
		dance = (IEnumerable<DanceCounter>)SettingsOperator.Beatmaps[(int)SettingsOperator.Sessioncfg["SongID"]].Dance;
		foreach (string line in lines)
		{
			if (line.Trim() == "[HitObjects]")
			{
				isHitObjectSection = true;
				continue;
			}

			if (isHitObjectSection)
			{
				// Break if we reach an empty line or another section
				if (string.IsNullOrWhiteSpace(line) || line.StartsWith('['))
					break;
				t = line;
				timing = Convert.ToInt32(line.Split(",")[2]);
				part = Convert.ToInt32(line.Split(",")[0]);
				if (part == 64) { part = 0; }
				else if (part == 192) { part = 1; }
				else if (part == 320) { part = 2; }
				else if (part == 448) { part = 3; }
				else if (part < 128) { part = 0; }
				else if (part < 256) { part = 1; }
				else if (part < 384) { part = 2; }
				else if (part < 512) { part = 3; }
				timen = -timing;
				Notes.Add(new NotesEn { timing = timen, NoteSection = part });
			}
		}
	}
	public override void _Ready()
	{
		ReplayINT = 0;
		SettingsOperator = GetNode<SettingsOperator>("/root/SettingsOperator");
		SpectatorPanel = GD.Load<PackedScene>("res://Panels/Overlays/SpectatorSettings.tscn").Instantiate().GetNode<Control>(".");
		ApiOperator = GetNode<ApiOperator>("/root/ApiOperator");
		Beatmap_Background = GetNode<TextureRect>("./Beatmap_Background");
		WaitClock = GetNode<Timer>("Wait");
		AudioPlayer.Instance.Stop();
		ClipContents = true;

		Dead = false;
		Beatmap_Background.SelfModulate = new Color(1f - (1f * (SettingsOperator.backgrounddim * 0.01f)), 1f - (1f * (SettingsOperator.backgrounddim * 0.01f)), 1f - (1f * (SettingsOperator.backgrounddim * 0.01f)));
		BeatmapBackground.FlashEnable = false;


		HealthBar.Reset();
		Control P = GetNode<Control>("Playfield");
		Chart = GetNode<HBoxContainer>("Playfield/ChartSections");
		HitPoint = (int)Chart.Size.Y - 150;
		hittext = GD.Load<PackedScene>("res://Panels/GameplayElements/Static/hittext.tscn").Instantiate().GetNode<Label>(".");
		hittextinit = true;
		hittext.Modulate = new Color(1f, 1f, 1f, 0f);
		hittext.ZIndex = 100;
		GetNode<Control>("Playfield").AddChild(hittext);
		hittextoldpos = hittext.Position;

		SettingsOperator.PerfectJudge = 500 / (int)(SettingsOperator.Sessioncfg["beatmapaccuracy"]);
		SettingsOperator.GreatJudge = (int)(SettingsOperator.PerfectJudge * 4);
		SettingsOperator.MehJudge = (int)(SettingsOperator.PerfectJudge * 6);

		meh = new ColorRect();
		meh.Size = new Vector2(400, SettingsOperator.MehJudge);
		meh.Position = new Vector2(0, -SettingsOperator.MehJudge / 2);
		meh.Color = new Color(0.5f, 0f, 0f, 0.1f);
		meh.Visible = false;
		GetNode<ColorRect>("Playfield/Guard").AddChild(meh);

		great = new ColorRect();
		great.Size = new Vector2(400, SettingsOperator.GreatJudge);
		great.Position = new Vector2(0, -SettingsOperator.GreatJudge / 2);
		great.Color = new Color(0f, 0.5f, 0f, 0.1f);
		great.Visible = false;
		GetNode<ColorRect>("Playfield/Guard").AddChild(great);

		perfect = new ColorRect();
		perfect.Size = new Vector2(400, SettingsOperator.PerfectJudge * 2);
		perfect.Position = new Vector2(0, -SettingsOperator.PerfectJudge);
		perfect.Color = new Color(0f, 0f, 0.5f, 0.1f);
		perfect.Visible = false;
		GetNode<ColorRect>("Playfield/Guard").AddChild(perfect);


		foreach (int i in Enumerable.Range(1, 4))
		{
			var notet = GetNode<PanelContainer>("Playfield/ChartSections/Section" + i);
			Keys.Add(new KeyL { Node = notet, hit = false });
			notet.SelfModulate = idlehit;
		}


		maxrndvalue = (int)(1000000 * ModsMulti.multiplier);

		SettingsOperator.ResetScore();
		SettingsOperator.Resetms();
		ReloadBeatmap(SettingsOperator.Sessioncfg["beatmapurl"].ToString());

		// If auto is enabled, it will make a Replay file with Auto being the player playing the beatmap. Before this it didn't make the replay file, it just plays.
		// I am doing this because it's more simpler for me and don't have to worry about breaking auto (Qlutina)
		if (ModsOperator.Mods["auto"]) {
			Replay.ResetReplay(); // Resets the replay to be cleared before making the auto replay.
			SettingsOperator.SpectatorMode = true; // Enables Spectator Mode to play the Replay, without this it won't know it even existed lol :p
			foreach (NotesEn note in Notes)
			{
				var time = -note.timing - 50;
				var key = note.NoteSection;
				Replay.AddReplay(time, key); // Adds the input into the Replay Cache.
			}
		}



		if (SettingsOperator.SpectatorMode)
			{
				AddChild(SpectatorPanel);
			}
			else Replay.ResetReplay();
		GD.Print($"Spectator mode: {SettingsOperator.SpectatorMode}");
	}



	public Texture2D NoteSkinBack { get; set; }
	public Texture2D NoteSkinFore { get; set; }
	public Color chartclear = new Color(0.03f, 0.03f, 0.03f, 0.78f);
	public Color chartbeam = new Color(0.20f, 0.0f, 0.20f, 0.78f);
	public Color activehit = new Color(0.20f, 0.0f, 0.20f);
	public Color idlehit = new Color(0.03f, 0.03f, 0.03f);
	public void hitnote(int Keyx, bool hit, int est)
	{
		var key = Keys[Keyx];
		if (hit)
		{
			if (!SettingsOperator.SpectatorMode) Replay.AddReplay(est, Keyx);
			key.Node.SelfModulate = activehit;
			key.Ani?.Kill(); // Abort the previous animation
			key.Ani = Keys[Keyx].Node.CreateTween();
			key.Ani.TweenProperty(Keys[Keyx].Node, "self_modulate", idlehit, 0.5f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
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

	public static void ReloadppCounter()
	{
		SettingsOperator.Gameplaycfg.pp = SettingsOperator.Get_ppvalue(SettingsOperator.Gameplaycfg.Max, SettingsOperator.Gameplaycfg.Great, SettingsOperator.Gameplaycfg.Meh, SettingsOperator.Gameplaycfg.Bad, ModsMulti.multiplier, SettingsOperator.Gameplaycfg.MaxCombo);
	}
	private float smoothedPlaybackPosition = 0f;

    /// <summary>
    /// Smooths position of the playback
    /// </summary>
	private float SmoothPosition(float current, ref float previous, float delta) =>
		previous = Mathf.Lerp(previous, current, delta * 10f);

    /// <summary>
    /// Game Clock
    /// </summary>
	public float GetRemainingTime(bool GameMode = false, float delta = 1f)
	{
		SettingsOperator.Gameplaycfg.Timeframe = -WaitClock.TimeLeft +
			SmoothPosition(AudioPlayer.Instance.GetPlaybackPosition(), ref smoothedPlaybackPosition, delta);

		var AudioOffset = GameMode ? SettingsOperator.AudioOffset * 0.001f : 0f;

		SettingsOperator.Gameplaycfg.Timeframe = SettingsOperator.Gameplaycfg.Timeframe - startedtime + AudioOffset;

		return (float)(SettingsOperator.Gameplaycfg.Timeframe);
	}
    /// <summary>
    /// Get's the Score calculated
    /// </summary>
	private int Get_Score(double pp, double maxpp, float multiplier) {
		double baseScore = (pp / maxpp) * 1000000;
		double finalScore = baseScore * multiplier;
		return (int)finalScore;
	}
    /// <summary>
    /// Starts Playback
    /// </summary>

	private void StartPlay()
	{
		songstarted = true;
		AudioPlayer.Instance.Play();
	}


    /// <summary>
    /// If SettingsOperator.ReplayMode is enabled, this will check at the specified index of the replay cache to simulate a keypress in Spectator Mode.
    /// </summary>
	private void CheckReplayKey(int est)
	{
		// Replay part (if enabled)
		if (SettingsOperator.SpectatorMode && Replay.ReplayCache.Count >0)
		{
			var Replayindex = Replay.ReplayCache[Math.Min(ReplayINT,Replay.ReplayCache.Count-1)];
			if (est > Replayindex.Time)
			{
				if (ReplayINT < Replay.ReplayCache.Count())
				{
					hitnote(Replayindex.NoteTap, true, est);
					ReplayINT++;
				} else if (ReplayINT > 0 && Keys[Replay.ReplayCache[ReplayINT-1].NoteTap].hit) {
					hitnote(Replay.ReplayCache[ReplayINT-1].NoteTap, false, est);
				}
			}
		}
	}
    /// <summary>
    /// This is the one that controls the Player input.
    /// </summary>

	private void PlayerKeyCheck(int est){
			for (int i = 0; i < 4; i++)
			{
				if (Input.IsActionJustPressed("Key" + (i + 1)) && !SettingsOperator.SpectatorMode)
				{
					hitnote(i, true, est);
				}
				else if (Input.IsActionJustReleased("Key" + (i + 1)) && !SettingsOperator.SpectatorMode)
				{
					hitnote(i, false, est);
				}
			}}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		SettingsOperator.Gameplaycfg.Score = scoreint; // Set the score of the player
		try
		{
			float est = GetRemainingTime(GameMode: true, delta: (float)delta) / 0.001f;


			// DEATH

			if (HealthBar.Health == 0 && !Dead && !ModsOperator.Mods["no-fail"])
			{
				Dead = !Dead;
				FailAnimation();
			}


			// End Game

			if (SettingsOperator.Gameplaycfg.TimeTotalGame - SettingsOperator.Gameplaycfg.Time < 0 && !Finished)
			{
				Finished = true;
				ApiOperator.SubmitScore();
				Replay.SaveReplay();
				if (!SettingsOperator.Marathon)
				{
					AddChild(GD.Load<PackedScene>("res://Panels/Screens/EndScreen.tscn").Instantiate());
				}
				else
				{
					SettingsOperator.MarathonID++;
					if (SettingsOperator.MarathonID >= SettingsOperator.MarathonMapPaths.Count) GetNode<SceneTransition>("/root/Transition").Switch("res://Panels/Screens/ResultsScreen.tscn");
					SettingsOperator.SelectSongID(SettingsOperator.MarathonMapPaths[SettingsOperator.MarathonID]);
					GetNode<SceneTransition>("/root/Transition").Switch("res://Panels/Screens/LoadingMarathonScreen.tscn");
				}
			}

			if (SettingsOperator.Gameplaycfg.Combo > SettingsOperator.Gameplaycfg.MaxCombo)
			{
				SettingsOperator.Gameplaycfg.MaxCombo = SettingsOperator.Gameplaycfg.Combo;
			}


			Beatmap_Background.SelfModulate = new Color(1f - (1f * (SettingsOperator.backgrounddim * 0.01f)), 1f - (1f * (SettingsOperator.backgrounddim * 0.01f)), 1f - (1f * (SettingsOperator.backgrounddim * 0.01f)));
			SettingsOperator.Gameplaycfg.Accuracy = ReloadAccuracy(SettingsOperator.Gameplaycfg.Max, SettingsOperator.Gameplaycfg.Great, SettingsOperator.Gameplaycfg.Meh, SettingsOperator.Gameplaycfg.Bad);
			ReloadppCounter();
			int Ttick = 0;
			if (Input.IsActionJustPressed("pausemenu") && SettingsOperator.SpectatorMode)
			{
				BeatmapBackground.FlashEnable = true;
				SettingsOperator.toppaneltoggle();
				GetNode<SceneTransition>("/root/Transition").Switch("res://Panels/Screens/song_select.tscn");
			}
			else if (Input.IsActionJustPressed("pausemenu") && !SettingsOperator.Marathon)
			{
				ShowPauseMenu();
			}
			else if (Input.IsActionJustPressed("pausemenu") && SettingsOperator.Marathon)
			{
				SettingsOperator.toppaneltoggle();
				BeatmapBackground.FlashEnable = true;
				GetNode<SceneTransition>("/root/Transition").Switch("res://Panels/Screens/MarathonMode.tscn");
				SettingsOperator.Marathon = false;
			}
			else if (Input.IsActionJustPressed("retry") && !SettingsOperator.Marathon)
			{
				BeatmapBackground.FlashEnable = true;
				SettingsOperator.toppaneltoggle();
				GetNode<SceneTransition>("/root/Transition").Switch("res://Panels/Screens/SongLoadingScreen.tscn");
			}
			//debugtext.Text = $"est: {est}\nDanceIndex:{DanceIndex}\nTimeindex:{dance.ElementAt(DanceIndex).time}";



			if ((int)est >= dance.ElementAt(DanceIndex).time + BeatmapBackground.bpm && BeatmapBackground.FlashEnable)
			{
				Transitioning(dance.ElementAt(DanceIndex).flash);
				IncreaseDanceIndex();
			}
			else if ((int)est >= dance.ElementAt(DanceIndex).time && !BeatmapBackground.FlashEnable)
			{
				Transitioning(dance.ElementAt(DanceIndex).flash);
				IncreaseDanceIndex();
			}

			PlayerKeyCheck((int)est);
			CheckReplayKey((int)est);
			// Gamenotes

			var viewportSize = 0f;
			if (IsInsideTree())
			{
				viewportSize = GetViewportRect().Size.Y;
			}
			var timepart = 0;
			//var scrollspeed = ComputeScrollTime(int.Parse(SettingsOperator.GetSetting("scrollspeed").ToString())); // Scroll speed for the notes
			var scrollspeed = 1;

			for (int i = 0; i < Notes.Count; i++)
			{
				var Note = Notes[i];
				var notex = Note.timing + est + HitPoint;
				if (timepart != Note.timing)
				{
					Noteindex++;
					timepart = Note.timing;
				}
				if (notex > -150 && notex < viewportSize + 150 && !Note.hit)
				{
					if (!Note.hit && Note.Node == null)
					{
						var playfieldpart = GetNode<Control>($"Playfield/ChartSections/Section{Note.NoteSection + 1}/Control");
						Note.Node = GD.Load<PackedScene>("res://Panels/GameplayElements/Static/note.tscn").Instantiate().GetNode<Sprite2D>(".");
						Note.Node.SetMeta("part", Note.NoteSection);
						Note.Node.SelfModulate = new Color(0.83f, 0f, 1f);
						playfieldpart.AddChild(Note.Node);
					}
					if (ModsOperator.Mods["slice"] && Note.Node != null)
					{
						Note.Node.Modulate = new Color(1f, 1f, 1f, Math.Min(HitPoint, Note.Node.Position.Y - 200) / HitPoint);
					}
					else if (ModsOperator.Mods["black-out"] && Note.Node != null)
					{
						Note.Node.Modulate = new Color(0f, 0f, 0f, 0f);
					}
					if (Note.Node != null)
					{
						Note.Node.Position = new Vector2(0, (notex * scrollspeed) - (HitPoint * (scrollspeed - 1)));
						Ttick++;
						JudgeResult = checkjudge((int)notex, Keys[(int)Note.Node.GetMeta("part")].hit, Note.Node, Note.Node.Visible);
						if (JudgeResult < 4)
						{
							// NPC Part
							CheckNPCValues();

							mshitold = HitPoint + 5;
							mshit = notex;
							SettingsOperator.Addms(mshitold - mshit - 50);
							SettingsOperator.Gameplaycfg.ms = SettingsOperator.Getms();
							Keys[(int)Note.Node.GetMeta("part")].hit = false;

							scoretween?.Kill();
							scoretween = CreateTween();
							scoretween.TweenProperty(this, "scoreint", Get_Score(SettingsOperator.Gameplaycfg.pp, SettingsOperator.Gameplaycfg.maxpp, ModsMulti.multiplier), 0.3f);
							scoretween.Play();
							Note.hit = true;
							Note.Node.Visible = false;
							Note.Node.QueueFree();
							Note.Node = null;
						}
						else
						{
							Keys[(int)Note.Node.GetMeta("part")].hit = false;
						}
					}
				}
			}

		}
		catch (Exception e)
		{
			if (erroredout == false)
			{
				erroredout = true;
				GD.PrintErr(e);
				Notify.Post("Can't play the bestmap because\n" + e.Message);
				SettingsOperator.toppaneltoggle();
				BeatmapBackground.FlashEnable = true;
				GetNode<SceneTransition>("/root/Transition").Switch("res://Panels/Screens/song_select.tscn");
			}
		}
	}

	private void IncreaseDanceIndex()
	{
		if (DanceIndex + 1 < dance.Count())
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
	public void Hittext(string word, Color wordcolor)
	{
		hittext.Modulate = wordcolor;
		hittext.Position = new Vector2(hittextoldpos.X, hittextoldpos.Y - 10);
		//tween.TweenProperty(hittext, "position", new Vector2(hittext.Position.X,hittext.Position.Y+10), 0.5f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		if (hittextani != null && hittextani.IsRunning())
		{
			hittextani.Stop();
		}

		hittextani = hittext.CreateTween();
		hittextani.Parallel().TweenProperty(hittext, "modulate", new Color(0f, 0f, 0f, 0f), 0.5).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		hittextani.Parallel().TweenProperty(hittext, "position", hittextoldpos, 0.5).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		hittextani.Play();
		hittext.Text = word;
	}
	public int checkjudge(int timing, bool keyvalue, Sprite2D node, bool visibility)
	{
		if (timing + nodeSize > HitPoint - SettingsOperator.PerfectJudge && timing + nodeSize < HitPoint + SettingsOperator.PerfectJudge && keyvalue && visibility)
		{
			SettingsOperator.Gameplaycfg.Max++;
			SettingsOperator.Gameplaycfg.Combo++;
			Hittext("Perfect", new Color(0f, 0.71f, 1f));
			HealthBar.Heal(5);
			return 0;
		}
		else if (timing + nodeSize > HitPoint - (SettingsOperator.GreatJudge / 2) && timing + nodeSize < HitPoint + (SettingsOperator.GreatJudge / 2) && keyvalue && visibility)
		{
			SettingsOperator.Gameplaycfg.Great++;
			SettingsOperator.Gameplaycfg.Combo++;
			Hittext("Great", new Color(0f, 1f, 0.03f));
			HealthBar.Heal(3);
			return 1;
		}
		else if (timing + nodeSize > HitPoint - (SettingsOperator.MehJudge / 2) && timing + nodeSize < HitPoint + (SettingsOperator.MehJudge / 2) && keyvalue && visibility)
		{
			Hittext("Meh", new Color(1f, 0.66f, 0f));
			SettingsOperator.Gameplaycfg.Meh++;
			SettingsOperator.Gameplaycfg.Combo++;
			HealthBar.Heal(1);
			return 2;
		}
		else if (timing + nodeSize > GetViewportRect().Size.Y + 60 && visibility)
		{
			Hittext("Miss", new Color(1f, 0.28f, 0f));
			SettingsOperator.Gameplaycfg.Bad++;
			if (SettingsOperator.Gameplaycfg.Combo > 50) Sample.PlaySample("res://Skin/Sounds/combobreak.wav");
			SettingsOperator.Gameplaycfg.Combo = 0;
			HealthBar.Damage(5);
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

