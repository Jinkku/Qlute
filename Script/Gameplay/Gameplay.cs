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
public class KeyL {
	public PanelContainer Node {get;set;}
	public ColorRect Beam {get;set;}
	public bool hit {get;set;}
	public Tween Ani {get;set;}
}
public partial class Gameplay : Control
{
	// Called when the node enters the scene tree for the first time.
	public static SettingsOperator SettingsOperator { get; set; }
	public static ApiOperator ApiOperator { get; set; }
	//public static Label Ttiming { get; set; }
	//public static Label Hits { get; set; }
	public HBoxContainer Chart { get; set; }
	public List<KeyL> Keys = new List<KeyL>();
	public int JudgeResult = -1;
	public int PerfectJudge = 500;
	public int nodeSize = 54;
	public int GreatJudge {get;set;}
	public int MehJudge {get;set;}
	public ColorRect meh {get;set;}
	public ColorRect great {get;set;}
	public ColorRect perfect {get;set;}
	public double mshit {get;set;}
	public double mshitold {get;set;}
	public long startedtime {get ;set; }
	public bool songstarted = false;
	public Node2D noteblock {get;set;}
	public bool hittextinit = false;
	public Label hittext {get;set;}
	public Tween hittextani {get;set;}
	private Tween hitnoteani {get;set;}
	private Tween HurtAnimation {get;set;}
	public Vector2 hittextoldpos {get;set;}
	public List<NotesEn> Notes = new List<NotesEn>();
	public int Noteindex = 1;
	public TextureRect Beatmap_Background {get;set;}
	private Control PauseMenu {get;set;}
	private bool Finished {get;set;}
	private int score {get;set;}
	private int newscore {get;set;}
	private Tween scoretween {get;set;}
	private IEnumerable<DanceCounter> dance {get;set;}
	private int DanceIndex {get;set;}
	private Label debugtext {get;set;}
	public static bool Dead {get;set;}

	private void ShowPauseMenu(){
		PauseMenu = GD.Load<PackedScene>("res://Panels/Screens/PauseMenu.tscn").Instantiate().GetNode<Control>(".");
		PauseMenu.ZIndex = 200;
		AddChild(PauseMenu);
		GetTree().Paused = true;
	}
	public override void _Ready()
	{
		SettingsOperator = GetNode<SettingsOperator>("/root/SettingsOperator");
		ApiOperator = GetNode<ApiOperator>("/root/ApiOperator");
		Beatmap_Background = GetNode<TextureRect>("./Beatmap_Background");
		AudioPlayer.Instance.Stop();

		Dead = false;
		Beatmap_Background.SelfModulate = new Color(1f-(1f*(SettingsOperator.backgrounddim*0.01f)),1f-(1f*(SettingsOperator.backgrounddim*0.01f)),1f-(1f*(SettingsOperator.backgrounddim*0.01f)));
		BeatmapBackground.FlashEnable = false;
		

		HealthBar.Reset();
		Control P = GetNode<Control>("Playfield");
		Chart = GetNode<HBoxContainer>("Playfield/ChartSections");
		hittext = GD.Load<PackedScene>("res://Panels/GameplayElements/Static/hittext.tscn").Instantiate().GetNode<Label>(".");
		hittextinit = true;
		hittext.Modulate = new Color(1f,1f,1f,0f);
		hittext.ZIndex = 100;
		GetNode<Control>("Playfield").AddChild(hittext);
		hittextoldpos = hittext.Position;
		reloadSkin();
		PClock = 0;

		debugtext = new Label();
		AddChild(debugtext);
		debugtext.Position = new Vector2(100,300);

		PerfectJudge = PerfectJudge / (int)(SettingsOperator.Sessioncfg["beatmapaccuracy"]);
		GreatJudge = (int)(PerfectJudge*4);
		MehJudge = (int)(PerfectJudge*6);

		meh = new ColorRect();
		meh.Size = new Vector2(400,MehJudge);
		meh.Position = new Vector2(0,-MehJudge/2);
		meh.Color = new Color(0.5f,0f,0f,0.1f);
		meh.Visible = false;
		GetNode<ColorRect>("Playfield/Guard").AddChild(meh);

		great = new ColorRect();
		great.Size = new Vector2(400,GreatJudge);
		great.Position = new Vector2(0,-GreatJudge/2);
		great.Color = new Color(0f,0.5f,0f,0.1f);
		great.Visible = false;
		GetNode<ColorRect>("Playfield/Guard").AddChild(great);
		
		perfect = new ColorRect();
		perfect.Size = new Vector2(400,PerfectJudge*2);
		perfect.Position = new Vector2(0,-PerfectJudge);
		perfect.Color = new Color(0f,0f,0.5f,0.1f);
		perfect.Visible = false;
		GetNode<ColorRect>("Playfield/Guard").AddChild(perfect);

		//Ttiming = GetNode<Label>("Time");
		//Hits = GetNode<Label>("Hits");
		foreach (int i in Enumerable.Range(1, 4)){
			var notet = GetNode<PanelContainer>("Playfield/KeyBoxes/Key"+i);
			Keys.Add(new KeyL {Node = notet, hit = false, Beam = GetNode<ColorRect>($"Playfield/ChartSections/Section{i}")});
			notet.Modulate = idlehit;
		}
		
		SettingsOperator.ResetScore();
		SettingsOperator.Resetms();
        using var file = FileAccess.Open(SettingsOperator.Sessioncfg["beatmapurl"].ToString(), FileAccess.ModeFlags.Read);
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
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("["))
                    break;
				t=line;
				timing = Convert.ToInt32(line.Split(",")[2]);
				part = Convert.ToInt32(line.Split(",")[0]);
				if (part == 64){part = 0;}
				else if (part == 192){part = 1;}
				else if (part == 320){part = 2;}
				else if (part == 448){part = 3;}
				else if (part<128){part = 0;}
				else if (part<256){part = 1;}
				else if (part<384){part = 2;}
				else if (part<512){part = 3;}
				timen = -timing;
				Notes.Add(new NotesEn {timing = timen, NoteSection = part});
            }
        }
		startedtime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()+5000;
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	private static void OnGuiInput(InputEvent inputEvent) {
		GD.Print(inputEvent);
	}


	public Texture2D NoteSkinBack {get;set;}
	public Texture2D NoteSkinFore {get;set;}
	public Color chartclear = new Color(0.03f,0.03f,0.03f,0.78f);
	public Color chartbeam = new Color(0.20f,0.0f,0.20f,0.78f);
	public Color activehit = new Color(0.20f,0.0f,0.20f);
	public Color idlehit = new Color(0.03f,0.03f,0.03f);
	public void reloadSkin(){
		NoteSkinBack = GD.Load<Texture2D>("res://Skin/Game/Backgroundnote.svg");
		NoteSkinFore = GD.Load<Texture2D>("res://Skin/Game/Foregroundnote.svg");
		nodeSize = (int)NoteSkinBack.GetSize().Y;
	}
	public void hitnote(int Keyx,bool hit){
		var key = Keys[Keyx];
		if (hit){
			key.Node.Modulate = activehit;
			key.Beam.Color = chartbeam;
			if (key.Ani != null){
			key.Ani.Kill();} // Abort the previous animation
			key.Ani = Keys[Keyx].Node.CreateTween();
			key.Ani.Parallel().TweenProperty(Keys[Keyx].Node, "modulate", idlehit, 0.5f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
			key.Ani.Parallel().TweenProperty(Keys[Keyx].Beam, "color", chartclear, 0.5f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
			key.Ani.Play();
		}
		key.hit = hit;
		}
	public long Clock {get;set;}
	public static long PClock = 0;

	public const float MAX_TIME_RANGE = 11485;
    public static float ComputeScrollTime(float scrollSpeed) => MAX_TIME_RANGE / scrollSpeed;
	public override void _Process(double delta)
	{
		try
		{

			Clock = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - PClock;
			float est = Clock - startedtime + float.Parse(SettingsOperator.GetSetting("audiooffset").ToString());
			if (est >= 0 && !songstarted)
			{
				songstarted = true;
				AudioPlayer.Instance.Play();
				startedtime = Clock;
			}

			// DEATH

			if (HealthBar.Health == 0 && !Dead && !ModsOperator.Mods["no-fail"])
			{
				Dead = !Dead;
				ShowPauseMenu();
			}


			// End Game

			if (SettingsOperator.Gameplaycfg["timetotal"] - SettingsOperator.Gameplaycfg["time"] < -2000 && !Finished)
			{
				SettingsOperator.toppaneltoggle();
				BeatmapBackground.FlashEnable = true;
				Finished = true;
				if (!SettingsOperator.Marathon)
				{
					GetNode<SceneTransition>("/root/Transition").Switch("res://Panels/Screens/ResultsScreen.tscn");
					ApiOperator.SubmitScore();
				}
				else
				{
					SettingsOperator.MarathonID++;
					if (SettingsOperator.MarathonID >= SettingsOperator.MarathonMapPaths.Count) GetNode<SceneTransition>("/root/Transition").Switch("res://Panels/Screens/ResultsScreen.tscn");
					SettingsOperator.SelectSongID(SettingsOperator.MarathonMapPaths[SettingsOperator.MarathonID]);
					GetNode<SceneTransition>("/root/Transition").Switch("res://Panels/Screens/LoadingMarathonScreen.tscn");
				}
			}

			if (SettingsOperator.Gameplaycfg["combo"] > SettingsOperator.Gameplaycfg["maxcombo"])
			{
				SettingsOperator.Gameplaycfg["maxcombo"] = SettingsOperator.Gameplaycfg["combo"];
			}


			Beatmap_Background.SelfModulate = new Color(1f - (1f * (SettingsOperator.backgrounddim * 0.01f)), 1f - (1f * (SettingsOperator.backgrounddim * 0.01f)), 1f - (1f * (SettingsOperator.backgrounddim * 0.01f)));
			if (AudioPlayer.Instance.PitchScale != 1.0f)
			{
				est = est * AudioPlayer.Instance.PitchScale;
			}
			SettingsOperator.Gameplaycfg["accuracy"] = (SettingsOperator.Gameplaycfg["max"] + (SettingsOperator.Gameplaycfg["great"] / 2) + (SettingsOperator.Gameplaycfg["meh"] / 3)) / (SettingsOperator.Gameplaycfg["max"] + SettingsOperator.Gameplaycfg["great"] + SettingsOperator.Gameplaycfg["meh"] + SettingsOperator.Gameplaycfg["bad"]);
			SettingsOperator.Gameplaycfg["score"] = score;
			SettingsOperator.Gameplaycfg["pp"] = SettingsOperator.Get_ppvalue((int)SettingsOperator.Gameplaycfg["max"], (int)SettingsOperator.Gameplaycfg["great"], (int)SettingsOperator.Gameplaycfg["meh"], (int)SettingsOperator.Gameplaycfg["bad"], ModsMulti.multiplier, (int)SettingsOperator.Gameplaycfg["maxcombo"]);
			SettingsOperator.Gameplaycfg["time"] = (int)est;
			//float est = AudioPlayer.Instance.GetPlaybackPosition()*1000;
			int Ttick = 0;
			//maxc=hits[0]+hits[1]+hits[2]+hits[3]
			//    accuracy=round(((hits[0]+(hits[1]/2)+(hits[2]/3))/(maxc))*100,2)
			//Hits.Text = "Hits:\n" + SettingsOperator.Gameplaycfg["max"] + "\n" + SettingsOperator.Gameplaycfg["great"] + "\n" + SettingsOperator.Gameplaycfg["meh"] + "\n" + SettingsOperator.Gameplaycfg["bad"] + "\n"+"ms:"+(mshitold-mshit)+"ms " + mshitold + " " + mshit + " " + SettingsOperator.Getms();
			// Key imputs
			for (int i = 0; i < 4; i++)
			{
				if (Input.IsActionJustPressed("Key" + (i + 1)) && !ModsOperator.Mods["auto"])
				{
					hitnote(i, true);
				}
				else if (Input.IsActionJustReleased("Key" + (i + 1)) && !ModsOperator.Mods["auto"])
				{
					hitnote(i, false);
				}
			}
			if (Input.IsActionJustPressed("pausemenu") && !SettingsOperator.Marathon)
			{
				ShowPauseMenu();
			}
			else if (Input.IsActionJustPressed("pausemenu") && SettingsOperator.Marathon)
			{
				SettingsOperator.toppaneltoggle();
				BeatmapBackground.FlashEnable = true;
				GetNode<SceneTransition>("/root/Transition").Switch("res://Panels/Screens/MarathonMode.tscn");
			}
			else if (Input.IsActionJustPressed("retry") && !SettingsOperator.Marathon)
			{
				BeatmapBackground.FlashEnable = true;
				SettingsOperator.toppaneltoggle();
				GetNode<SceneTransition>("/root/Transition").Switch("res://Panels/Screens/SongLoadingScreen.tscn");
			}
			//debugtext.Text = $"est: {est}\nDanceIndex:{DanceIndex}\nTimeindex:{dance.ElementAt(DanceIndex).time}";
			if ((int)est > dance.ElementAt(DanceIndex).time)
			{
				BeatmapBackground.FlashEnable = dance.ElementAt(DanceIndex).flash;
				if (DanceIndex + 1 < dance.Count())
				{
					DanceIndex++;
				}
			}


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
				var notex = Note.timing + est + Chart.Size.Y;
				if (timepart != Note.timing)
				{
					Noteindex++;
					timepart = Note.timing;
				}
				if (notex > -150 && notex < viewportSize + 150 && !Note.hit)
				{
					if (!Note.hit && Note.Node == null)
					{
						var playfieldpart = GetNode<ColorRect>($"Playfield/ChartSections/Section{Note.NoteSection + 1}");
						Note.Node = GD.Load<PackedScene>("res://Panels/GameplayElements/Static/note.tscn").Instantiate().GetNode<Sprite2D>(".");
						Note.Node.SetMeta("part", Note.NoteSection);
						Note.Node.SelfModulate = new Color(0.83f, 0f, 1f);
						Note.Node.Texture = NoteSkinBack;
						var notefront = Note.Node.GetNode<Sprite2D>("NoteFront");
						notefront.Texture = NoteSkinFore;
						playfieldpart.AddChild(Note.Node);
					}
					if (Note.Node != null && (int)notex + nodeSize > Chart.Size.Y && (int)notex + nodeSize < Chart.Size.Y + MehJudge && ModsOperator.Mods["auto"])
					{
						hitnote((int)Note.Node.GetMeta("part"), true);
					}
					else if (ModsOperator.Mods["auto"])
					{
						hitnote((int)Note.Node.GetMeta("part"), false);
					}
					if (ModsOperator.Mods["slice"] && Note.Node != null)
					{
						Note.Node.SelfModulate = new Color(1f, 1f, 1f, Math.Min(Chart.Size.Y, Note.Node.Position.Y - 200) / Chart.Size.Y);
					}
					else if (ModsOperator.Mods["black-out"] && Note.Node != null)
					{
						Note.Node.SelfModulate = new Color(0f, 0f, 0f, 0f);
					}
					if (Note.Node != null)
					{
						Note.Node.Position = new Vector2(0, notex * scrollspeed - (Chart.Size.Y * (scrollspeed - 1)));
						Ttick++;
						JudgeResult = checkjudge((int)notex, Keys[(int)Note.Node.GetMeta("part")].hit, Note.Node, Note.Node.Visible);
						if (JudgeResult < 4)
						{
							mshitold = Chart.Size.Y + 5;
							Keys[(int)Note.Node.GetMeta("part")].hit = false;
							mshit = notex;
							SettingsOperator.Addms(mshitold - mshit - 50);
							var urnote = new ColorRect();
							urnote.Position = new Vector2(-5, (GetNode<ColorRect>("UR").Size.Y / 2) + (((float)(mshitold - mshit - 50) / MehJudge)) * 200);
							urnote.Size = new Vector2(15, 2);
							GetNode<ColorRect>("UR").AddChild(urnote);
							var urani = urnote.CreateTween();
							urani.TweenProperty(urnote, "color", new Color(1f, 1f, 1f, 0f), 1).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
							urani.Play();
							urani.TweenCallback(Callable.From(urnote.QueueFree));
							SettingsOperator.Gameplaycfg["ms"] = SettingsOperator.Getms();
							newscore = (int)(SettingsOperator.Gameplaycfg["pp"] / SettingsOperator.Gameplaycfg["maxpp"] * (1000000 * ModsMulti.multiplier));
							if (scoretween != null)
							{
								scoretween.Kill();
							}
							scoretween = CreateTween();
							scoretween.TweenProperty(this, "score", newscore, 0.3f);
							scoretween.Play();
							Note.hit = true;
							Note.Node.Visible = false;
							Note.Node.QueueFree();
							Note.Node = null;
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
	public int checkjudge(int timing,bool keyvalue, Sprite2D node,bool visibility){
		if (timing+nodeSize > Chart.Size.Y-PerfectJudge && timing+nodeSize < Chart.Size.Y+PerfectJudge && keyvalue && visibility){
			SettingsOperator.Gameplaycfg["max"]++;
			SettingsOperator.Gameplaycfg["combo"]++;
			Hittext("Perfect", new Color(0f,0.71f,1f));
			HealthBar.Heal(5);
			return 0;
		} else if (timing+nodeSize > Chart.Size.Y-GreatJudge/2 && timing+nodeSize < Chart.Size.Y+GreatJudge/2 && keyvalue && visibility){
			SettingsOperator.Gameplaycfg["great"]++;
			SettingsOperator.Gameplaycfg["combo"]++;
			Hittext("Great", new Color(0f,1f,0.03f));
			HealthBar.Heal(3);
			return 1;
		}else if (timing+nodeSize > Chart.Size.Y-MehJudge/2 && timing+nodeSize < Chart.Size.Y+MehJudge/2 && keyvalue && visibility){
			Hittext("Meh", new Color(1f,0.66f,0f));
			SettingsOperator.Gameplaycfg["meh"]++;
			SettingsOperator.Gameplaycfg["combo"]++;
			HealthBar.Heal(1);
			return 2;
		}else if (timing+nodeSize > GetViewportRect().Size.Y+60 && visibility ){
			Hittext("Miss", new Color(1f,0.28f,0f));
			SettingsOperator.Gameplaycfg["bad"]++;
			if (SettingsOperator.Gameplaycfg["combo"] > 50) Sample.PlaySample("res://Skin/Sounds/combobreak.wav");
			SettingsOperator.Gameplaycfg["combo"] = 0;
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
		 else{
			return 4;
		}
	}
}

