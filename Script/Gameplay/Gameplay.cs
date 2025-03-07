using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
public class NotesEn {
	public int timing {get;set;}
	public List<object> Notes = new List<object>();
	public List<Sprite2D> Nodes = new List<Sprite2D>();
	public List<bool> NotesHit = new List<bool>();
}
public partial class Gameplay : Control
{
	// Called when the node enters the scene tree for the first time.
	public static SettingsOperator SettingsOperator { get; set; }
	public static ApiOperator ApiOperator { get; set; }
	//public static Label Ttiming { get; set; }
	//public static Label Hits { get; set; }
	public static ColorRect Chart { get; set; }
	public List<ColorRect> Keys = new List<ColorRect>();
	public List<Tween> KeyAni = new List<Tween>();
	public List<bool> KeyC = new List<bool>(
	);
	public int JudgeResult = -1;
	public int PerfectJudge = 500;
	public int nodeSize = 54;
	public int GreatJudge {get;set;}
	public int MehJudge {get;set;}
	public ColorRect meh {get;set;}
	public ColorRect great {get;set;}
	public ColorRect perfect {get;set;}
	public Color activehit = new Color(0.32f,0.42f,0.74f);
	public Color idlehit = new Color(0.5f,0.5f,0.5f);
	public double mshit {get;set;}
	public double mshitold {get;set;}
	public long startedtime {get ;set; }
	public bool songstarted = false;
	public Node2D noteblock {get;set;}
	public bool hittextinit = false;
	public Label hittext {get;set;}
	public Tween hittextani {get;set;}
	private Tween hitnoteani {get;set;}
	public Vector2 hittextoldpos {get;set;}
	public List<NotesEn> Notes = new List<NotesEn>();
	public TextureRect Beatmap_Background {get;set;}
	private Control PauseMenu {get;set;}
	public override void _Ready()
	{
		SettingsOperator = GetNode<SettingsOperator>("/root/SettingsOperator");
		ApiOperator = GetNode<ApiOperator>("/root/ApiOperator");
		Beatmap_Background = GetNode<TextureRect>("./Beatmap_Background");
		AudioPlayer.Instance.Stop();


		Beatmap_Background.SelfModulate = new Color(1f-(1f*(SettingsOperator.backgrounddim*0.01f)),1f-(1f*(SettingsOperator.backgrounddim*0.01f)),1f-(1f*(SettingsOperator.backgrounddim*0.01f)));
		BeatmapBackground.FlashEnable = false;
		
		Control P = GetNode<Control>("Playfield");
		Chart = GetNode<ColorRect>("Playfield/Chart");
		hittext = GD.Load<PackedScene>("res://Panels/GameplayElements/Static/hittext.tscn").Instantiate().GetNode<Label>(".");
		hittextinit = true;
		hittext.Modulate = new Color(1f,1f,1f,0f);
		Chart.AddChild(hittext);
		hittextoldpos = hittext.Position;
		reloadSkin();
		PClock = 0;


		PerfectJudge = PerfectJudge / (int)(SettingsOperator.Sessioncfg["beatmapaccuracy"]);
		GreatJudge = (int)(PerfectJudge*3);
		MehJudge = (int)(PerfectJudge*6);

		meh = new ColorRect();
		meh.Size = new Vector2(400,MehJudge);
		meh.Position = new Vector2(0,-MehJudge/2);
		meh.Color = new Color(0.5f,0f,0f,0.1f);
		meh.Visible = false;
		GetNode<ColorRect>("Playfield/Chart/Guard").AddChild(meh);

		great = new ColorRect();
		great.Size = new Vector2(400,GreatJudge);
		great.Position = new Vector2(0,-GreatJudge/2);
		great.Color = new Color(0f,0.5f,0f,0.1f);
		great.Visible = false;
		GetNode<ColorRect>("Playfield/Chart/Guard").AddChild(great);
		
		perfect = new ColorRect();
		perfect.Size = new Vector2(400,PerfectJudge);
		perfect.Position = new Vector2(0,-PerfectJudge/2);
		perfect.Color = new Color(0f,0f,0.5f,0.1f);
		perfect.Visible = false;
		GetNode<ColorRect>("Playfield/Chart/Guard").AddChild(perfect);

		//Ttiming = GetNode<Label>("Time");
		//Hits = GetNode<Label>("Hits");
		foreach (int i in Enumerable.Range(1, 4)){
			var notet = GetNode<ColorRect>("Playfield/KeyBoxes/Key"+i);
			Keys.Add(notet);;
			KeyC.Add(false);
			notet.Color = idlehit;
		}
		
		SettingsOperator.ResetScore();
		SettingsOperator.Resetms();
		noteblock = GD.Load<PackedScene>("res://Panels/GameplayElements/Static/note.tscn").Instantiate().GetNode<Sprite2D>(".");
        using var file = FileAccess.Open(SettingsOperator.Sessioncfg["beatmapurl"].ToString(), FileAccess.ModeFlags.Read);
        var text = file.GetAsText();
        var lines = text.Split("\n");
		var part = 0;
		var timing = 0;
		var t = "";
		var timen = -1;
		var notel = -1;
        var isHitObjectSection = false;
		//var note = noteblock.GetNode<Area2D>(".");
		//var notetexture = noteblock.GetNode<Sprite2D>("./Notetext");
		//notetexture.Texture = GD.Load<Texture2D>("res://Skin/Game/note.svg");
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
				var party = Convert.ToInt32(line.Split(",")[1]); // if converting
				if (part == 64){part = 0;}
				else if (part == 192){part = 1;}
				else if (part == 320){part = 2;}
				else if (part == 448){part = 3;}
				else if (part<128){part = 0;}
				else if (part<256){part = 1;}
				else if (part<384){part = 2;}
				else if (part<512){part = 3;}
				if (timen != -timing)
				{
					timen = -timing;
					Notes.Add(new NotesEn {timing = timen, Notes = new List<object>(), NotesHit = new List<bool>()});
					notel++;
				}
				Notes[notel].Notes.Add(part);
				Notes[notel].NotesHit.Add(false);
            }
        }
		startedtime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()+5000;
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	private static void OnGuiInput(InputEvent inputEvent) {
		GD.Print(inputEvent);
	}


	public Texture2D NoteSkin {get;set;}
	public void reloadSkin(){
		NoteSkin = GD.Load<Texture2D>("res://Skin/Game/note.svg");
	}
	public void hitnote(int Keyx,bool hit){
		if (hit){
		Keys[Keyx].Color = activehit;
		if (hitnoteani != null){
        hitnoteani.Kill();} // Abort the previous animation
		hitnoteani = Keys[Keyx].CreateTween();
		hitnoteani.TweenProperty(Keys[Keyx], "color", idlehit, 0.5f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		hitnoteani.Play();
		}
		KeyC[Keyx] = hit;
		}
	public long Clock {get;set;}
	public static long PClock = 0;
	public override void _Process(double delta)
	{   
		Clock =DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - PClock;
		float est = Clock-startedtime+float.Parse(SettingsOperator.GetSetting("audiooffset").ToString());
		if (est>=0 && !songstarted){
			songstarted = true;
			AudioPlayer.Instance.Play();
			startedtime = Clock;
		}

		// End Game

		if (SettingsOperator.Gameplaycfg["timetotal"]-SettingsOperator.Gameplaycfg["time"] < -2000)
		{
			SettingsOperator.toppaneltoggle();
			BeatmapBackground.FlashEnable = true;
			SettingsOperator.Sessioncfg["localpp"] = (double)SettingsOperator.Sessioncfg["localpp"] + SettingsOperator.Gameplaycfg["pp"];
			//ApiOperator.SubmitScore(); Commenting this as this is not finished yet :/
			GetTree().ChangeSceneToFile("res://Panels/Screens/ResultsScreen.tscn");
		}

		if (SettingsOperator.Gameplaycfg["combo"] > SettingsOperator.Gameplaycfg["maxcombo"]){
			SettingsOperator.Gameplaycfg["maxcombo"] = SettingsOperator.Gameplaycfg["combo"];
		}

		
		Beatmap_Background.SelfModulate = new Color(1f-(1f*(SettingsOperator.backgrounddim*0.01f)),1f-(1f*(SettingsOperator.backgrounddim*0.01f)),1f-(1f*(SettingsOperator.backgrounddim*0.01f)));
		if (AudioPlayer.Instance.PitchScale != 1.0f){
			est = est * AudioPlayer.Instance.PitchScale;
		}
		SettingsOperator.Gameplaycfg["accuracy"] = (SettingsOperator.Gameplaycfg["max"] + (SettingsOperator.Gameplaycfg["great"]/2) + (SettingsOperator.Gameplaycfg["meh"]/3)) / (SettingsOperator.Gameplaycfg["max"] +SettingsOperator.Gameplaycfg["great"] + SettingsOperator.Gameplaycfg["meh"] + SettingsOperator.Gameplaycfg["bad"]);
		SettingsOperator.Gameplaycfg["score"] = (SettingsOperator.Gameplaycfg["pp"] / SettingsOperator.Gameplaycfg["maxpp"]) * (1000000*ModsMulti.multiplier);
		SettingsOperator.Gameplaycfg["pp"] = SettingsOperator.Get_ppvalue((int)SettingsOperator.Gameplaycfg["max"],(int)SettingsOperator.Gameplaycfg["great"],(int)SettingsOperator.Gameplaycfg["meh"],(int)SettingsOperator.Gameplaycfg["bad"],ModsMulti.multiplier,(int)SettingsOperator.Gameplaycfg["maxcombo"]);
		SettingsOperator.Gameplaycfg["time"] = (int)est;
		//float est = AudioPlayer.Instance.GetPlaybackPosition()*1000;
		int Ttick = 0;
		int Keyx = 0;
		//maxc=hits[0]+hits[1]+hits[2]+hits[3]
        //    accuracy=round(((hits[0]+(hits[1]/2)+(hits[2]/3))/(maxc))*100,2)
		//Hits.Text = "Hits:\n" + SettingsOperator.Gameplaycfg["max"] + "\n" + SettingsOperator.Gameplaycfg["great"] + "\n" + SettingsOperator.Gameplaycfg["meh"] + "\n" + SettingsOperator.Gameplaycfg["bad"] + "\n"+"ms:"+(mshitold-mshit)+"ms " + mshitold + " " + mshit + " " + SettingsOperator.Getms();
		// Key imputs
		foreach (ColorRect self in Keys)
		{
			if (Input.IsActionJustPressed("Key"+(Keyx+1)) && !ModsOperator.Mods["auto"])
			{
				hitnote(Keyx,true);
			}
			else if (Input.IsActionJustReleased("Key"+(Keyx+1)) && !ModsOperator.Mods["auto"])
			{
				hitnote(Keyx,false);
			}
			Keyx++;
		}
		if (Input.IsActionJustPressed("pausemenu")){
			PauseMenu = GD.Load<PackedScene>("res://Panels/Screens/PauseMenu.tscn").Instantiate().GetNode<Control>(".");
			AddChild(PauseMenu);
			GetTree().Paused = true;
		}else if (Input.IsActionJustPressed("retry")){
			BeatmapBackground.FlashEnable = true;
			SettingsOperator.toppaneltoggle();
			GetTree().ChangeSceneToFile("res://Panels/Screens/SongLoadingScreen.tscn");
}
		// Gamenotes


		var viewportSize = 0f;
		if (IsInsideTree())
		{
			viewportSize = GetViewportRect().Size.Y;
		}
		foreach (var Notebox in Notes){
			var notex = Notebox.timing + est + Chart.Size.Y;
			if (!Notebox.Nodes.Any() && notex > -150 && notex < viewportSize+150 && delta/0.001 <4)
			{
				foreach (int part in Notebox.Notes){
					var node = new Sprite2D();
					node.Texture = NoteSkin;
					Notebox.Nodes.Add(node);
					Chart.AddChild(node);
					node.Centered = false;
					node.Position = new Vector2(100 * part, notex);
					node.SetMeta("part",part);
				}
			} else if (Notebox.NotesHit.Any() && Notebox.Notes.Any() && Notebox.Nodes.Any() && notex > -150 && notex < viewportSize+150)
			{
				foreach (var node in Notebox.Nodes){
					if ((int)notex+nodeSize > Chart.Size.Y && (int)notex+nodeSize < Chart.Size.Y+MehJudge  && ModsOperator.Mods["auto"]){
						KeyC[(int)node.GetMeta("part")] = true;
					}else if (ModsOperator.Mods["auto"]){
						KeyC[(int)node.GetMeta("part")] = false;
					}
					node.Position = new Vector2(node.Position.X, notex);
					if (ModsOperator.Mods["slice"]){
						node.SelfModulate = new Color(1f,1f,1f, Math.Min(Chart.Size.Y,node.Position.Y-200)/ Chart.Size.Y );
					}
					Ttick++;
					JudgeResult = checkjudge((int)notex,KeyC[(int)node.GetMeta("part")],node,node.Visible);
					if (JudgeResult < 4){
						mshitold = Chart.Size.Y+5;
						KeyC[(int)(node.Position.X / 100)] = false;
						mshit = notex;
						SettingsOperator.Addms(mshitold-mshit-50);
						var urnote = new ColorRect();
						urnote.Position = new Vector2(-5,(GetNode<ColorRect>("UR").Size.Y/2)+(((float)(mshitold-mshit-50)/MehJudge))*200);
						urnote.Size = new Vector2(15,2);
						GetNode<ColorRect>("UR").AddChild(urnote);
						var urani = urnote.CreateTween();
						urani.TweenProperty(urnote, "color", new Color(1f,1f,1f,0f), 1).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
						urani.Play();
						urani.TweenCallback(Callable.From(urnote.QueueFree));

						SettingsOperator.Gameplaycfg["ms"] = SettingsOperator.Getms();
					}
				}
			} else{
				foreach (var node in Notebox.Nodes){
					node.QueueFree();
				}
				Notebox.Nodes.Clear();
			}




		}
		//Ttiming.Text = "Time: " + est + "\n" + Ttick;

		//
	}
	public void Hittext(string word){
		hittext.Modulate = new Color(1f,1f,1f,1f);
		hittext.Position = new Vector2(hittextoldpos.X,hittextoldpos.Y-10);
		//tween.TweenProperty(hittext, "position", new Vector2(hittext.Position.X,hittext.Position.Y+10), 0.5f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		if (hittextani != null && hittextani.IsRunning()) {
			hittextani.Stop();
		}

		hittextani = hittext.CreateTween();
		hittextani.Parallel().TweenProperty(hittext, "modulate", new Color(1f,1f,1f,0f), 0.5).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		hittextani.Parallel().TweenProperty(hittext, "position", hittextoldpos, 0.5).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		hittextani.Play();
		hittext.Text = word;
	}
	public int checkjudge(int timing,bool keyvalue, Sprite2D node,bool visibility){
		if (timing+nodeSize > Chart.Size.Y-PerfectJudge/2 && timing+nodeSize < Chart.Size.Y+PerfectJudge/2 && keyvalue && visibility){
			SettingsOperator.Gameplaycfg["max"]++;
			SettingsOperator.Gameplaycfg["combo"]++;
			Hittext("Perfect");
			node.Visible = false;
			return 0;
		} else if (timing+nodeSize > Chart.Size.Y-GreatJudge/2 && timing+nodeSize < Chart.Size.Y+GreatJudge/2 && keyvalue && visibility){
			SettingsOperator.Gameplaycfg["great"]++;
			SettingsOperator.Gameplaycfg["combo"]++;
			Hittext("Great");
			node.Visible = false;
			return 1;
		}else if (timing+nodeSize > Chart.Size.Y-MehJudge/2 && timing+nodeSize < Chart.Size.Y+MehJudge/2 && keyvalue && visibility){
			Hittext("Meh");
			SettingsOperator.Gameplaycfg["meh"]++;
			SettingsOperator.Gameplaycfg["combo"]++;
			node.Visible = false;
			return 2;
		}else if (timing+nodeSize > GetViewportRect().Size.Y+60 && visibility ){
			Hittext("Miss");
			SettingsOperator.Gameplaycfg["bad"]++;
			SettingsOperator.Gameplaycfg["combo"] = 0;
			node.Visible = false;
			return 3;
		}
		 else{
			return 4;
		}
	}
}

