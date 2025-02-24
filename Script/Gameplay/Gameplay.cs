using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
public class NotesEn {
	public int timing {get;set;}
	public List<object> Notes = new List<object>();
	public List<Area2D> Nodes = new List<Area2D>();
	public List<bool> NotesHit = new List<bool>();
}
public partial class Gameplay : Control
{
	// Called when the node enters the scene tree for the first time.
	public static SettingsOperator SettingsOperator { get; set; }
	public static Label Ttiming { get; set; }
	public static Label Hits { get; set; }
	public static ColorRect Chart { get; set; }
	public List<object> Keys = new List<object>();
	public int Keyp = -1;
	public List<bool> KeyC = new List<bool>(
	);
	public int JudgeResult = -1;
	public int PerfectJudge = 50;
	public int nodeSize = 54;
	public int GreatJudge => PerfectJudge * 3;
	public int MehJudge => PerfectJudge * 6;
	public ColorRect meh {get;set;}
	public ColorRect great {get;set;}
	public ColorRect perfect {get;set;}
	

	public long startedtime {get ;set; }
	public bool songstarted = false;
	public Node2D noteblock {get;set;}
	public List<NotesEn> Notes = new List<NotesEn>();
	public TextureRect Beatmap_Background {get;set;}
	public override void _Ready()
	{
		SettingsOperator = GetNode<SettingsOperator>("/root/SettingsOperator");
		Beatmap_Background = GetNode<TextureRect>("./Beatmap_Background");
		AudioPlayer.Instance.Stop();
		Beatmap_Background.SelfModulate = new Color(1f-(1f*(SettingsOperator.backgrounddim*0.01f)),1f-(1f*(SettingsOperator.backgrounddim*0.01f)),1f-(1f*(SettingsOperator.backgrounddim*0.01f)));
		Control P = GetNode<Control>("Playfield");
		Chart = GetNode<ColorRect>("Playfield/Chart");

		meh = new ColorRect();
		meh.Size = new Vector2(400,MehJudge);
		meh.Position = new Vector2(0,-MehJudge/2);
		meh.Color = new Color(0.5f,0f,0f,0.4f);
		meh.Visible = false;
		GetNode<ColorRect>("Playfield/Chart/Guard").AddChild(meh);

		great = new ColorRect();
		great.Size = new Vector2(400,GreatJudge);
		great.Position = new Vector2(0,-GreatJudge/2);
		great.Color = new Color(0f,0.5f,0f,0.6f);
		great.Visible = false;
		GetNode<ColorRect>("Playfield/Chart/Guard").AddChild(great);
		
		perfect = new ColorRect();
		perfect.Size = new Vector2(400,PerfectJudge);
		perfect.Position = new Vector2(0,-PerfectJudge/2);
		perfect.Color = new Color(0f,0f,0.5f,1f);
		perfect.Visible = false;
		GetNode<ColorRect>("Playfield/Chart/Guard").AddChild(perfect);

		Ttiming = GetNode<Label>("Time");
		Hits = GetNode<Label>("Hits");
		foreach (int i in Enumerable.Range(1, 4)){
			Keys.Add(GetNode<ColorRect>("Playfield/KeyBoxes/Key"+i));
			KeyC.Add(false);
		}
		
		SettingsOperator.ResetScore();

		noteblock = GD.Load<PackedScene>("res://Panels/GameplayElements/Static/note.tscn").Instantiate().GetNode<Area2D>(".");
        using var file = FileAccess.Open(SettingsOperator.Sessioncfg["beatmapurl"].ToString(), FileAccess.ModeFlags.Read);
        var text = file.GetAsText();
        var lines = text.Split("\n");
		var part = 0;
		var timing = 0;
		var t = "";
		var timen = 0;
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
				if (part == 64){part = 0;}
				else if (part == 192){part = 1;}
				else if (part == 320){part = 2;}
				else if (part == 448){part = 3;}
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
	public override void _Process(double delta)
	{   
		long unixTimeMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
		float est = unixTimeMilliseconds-startedtime;
		if (est>=0 && !songstarted){
			songstarted = true;
			AudioPlayer.Instance.Play();
			startedtime = unixTimeMilliseconds;
		}

		// End Game

		if (SettingsOperator.Gameplaycfg["timetotal"]-SettingsOperator.Gameplaycfg["time"] < 0)
		{
			SettingsOperator.toppaneltoggle();
			GetTree().ChangeSceneToFile("res://Panels/Screens/ResultsScreen.tscn");
		}



		Beatmap_Background.SelfModulate = new Color(1f-(1f*(SettingsOperator.backgrounddim*0.01f)),1f-(1f*(SettingsOperator.backgrounddim*0.01f)),1f-(1f*(SettingsOperator.backgrounddim*0.01f)));
		if ((float)SettingsOperator.Sessioncfg["songspeed"] != 1.0f){
			est = est * (float)SettingsOperator.Sessioncfg["songspeed"];
		}
		SettingsOperator.Gameplaycfg["time"] = (int)est;
		//float est = AudioPlayer.Instance.GetPlaybackPosition()*1000;
		int Ttick = 0;
		int Keyx = 0;
		SettingsOperator.Gameplaycfg["score"] = (int)(((double)SettingsOperator.Gameplaycfg["pp"] / SettingsOperator.Gameplaycfg["maxpp"]) * 1000000);
		SettingsOperator.Gameplaycfg["pp"] = (int)((SettingsOperator.Gameplaycfg["max"]+(SettingsOperator.Gameplaycfg["great"]/2)+(SettingsOperator.Gameplaycfg["meh"]/3)/(SettingsOperator.Gameplaycfg["bad"]+1)) * SettingsOperator.ppbase);
		Hits.Text = "Hits:\n" + SettingsOperator.Gameplaycfg["max"] + "\n" + SettingsOperator.Gameplaycfg["great"] + "\n" + SettingsOperator.Gameplaycfg["meh"] + "\n" + SettingsOperator.Gameplaycfg["bad"] + "\n";
		// Key imputs
		foreach (ColorRect self in Keys)
		{
			if (ModsOperator.Mods["auto"]){
				self.Color = new Color(0.32f,0.42f,0.74f);
				KeyC[Keyx] = true;
			}
			else if (Input.IsActionPressed("Key"+(Keyx+1)))
			{
				self.Color = new Color(0.32f,0.42f,0.74f);
				KeyC[Keyx] = true;
				Keyp = Keyx;
			}
			else
			{
				self.Color = new Color(0.5f,0.5f,0.5f);
				KeyC[Keyx] = false;
				Keyp = -1;
			}
			Keyx++;
		}
		if (Input.IsActionJustPressed("pausemenu")){
			SettingsOperator.toppaneltoggle();
			GetTree().ChangeSceneToFile("res://Panels/Screens/song_select.tscn");
		}
		// Gamenotes


		var viewportSize = GetViewportRect().Size.Y;
		foreach (var Notebox in Notes){
			var notex = Notebox.timing + est + viewportSize/2 + ((60000/(int)SettingsOperator.Sessioncfg["beatmapbpm"]));
			if (Notebox.NotesHit.Any() && Notebox.Notes.Any() && !Notebox.Nodes.Any() && notex > -150 && notex < viewportSize+150 && delta/0.001 <4)
			{
				foreach (int part in Notebox.Notes){
					var node = GD.Load<PackedScene>("res://Panels/GameplayElements/Static/note.tscn").Instantiate().GetNode<Area2D>(".");
					Notebox.Nodes.Add(node);
					Chart.AddChild(node);
					node.Position = new Vector2(100 * part, notex);
				}
			} else if (Notebox.NotesHit.Any() && Notebox.Notes.Any() && Notebox.Nodes.Any() && notex > -150 && notex < viewportSize+150)
			{
				foreach (var node in Notebox.Nodes){
					node.Position = new Vector2(node.Position.X, notex);
					Ttick++;
					JudgeResult = checkjudge((int)notex,KeyC[(int)(node.Position.X / 100)],node,node.Visible);
					if (JudgeResult < 4){
						node.Visible = false;
					}
				}
			} else{
				foreach (var node in Notebox.Nodes){
					node.QueueFree();
				}
				Notebox.Nodes.Clear();
			}
			// Game Judge
			
			if (JudgeResult == 0){
				// Perfect
			} else if (JudgeResult == 4){
				// Bad
				//SettingsOperator.Gameplaycfg["miss"]++;
			}
//			if (notex > -100 && notex < viewportSize+100){

//			}




		}
		Ttiming.Text = "Time: " + est + "\n" + Ttick;

		//
	}
	public int checkjudge(int timing,bool keyvalue, Area2D node,bool visibility){
		if (timing+nodeSize > Chart.Size.Y-PerfectJudge/2 && timing+nodeSize < Chart.Size.Y+PerfectJudge/2 && keyvalue && visibility){
			SettingsOperator.Gameplaycfg["max"]++;
			node.Visible = false;
			return 0;
		} else if (timing+nodeSize > Chart.Size.Y-GreatJudge/2 && timing+nodeSize < Chart.Size.Y+GreatJudge/2 && keyvalue && visibility && !ModsOperator.Mods["auto"]){
			SettingsOperator.Gameplaycfg["great"]++;
			node.Visible = false;
			return 1;
		}else if (timing+nodeSize > Chart.Size.Y-MehJudge/2 && timing+nodeSize < Chart.Size.Y+MehJudge/2 && keyvalue && visibility && !ModsOperator.Mods["auto"]){
			SettingsOperator.Gameplaycfg["meh"]++;
			node.Visible = false;
			return 2;
		}else if (timing+nodeSize > GetViewportRect().Size.Y+60 && visibility){
			SettingsOperator.Gameplaycfg["bad"]++;
			node.Visible = false;
			return 2;
		}
		 else{
			return 4;
		}
	}
}

