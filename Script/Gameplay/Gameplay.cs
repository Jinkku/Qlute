using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
public partial class Gameplay : Control
{
	// Called when the node enters the scene tree for the first time.
	public static SettingsOperator SettingsOperator { get; set; }
	public static Label Ttiming { get; set; }
	public static Label Hits { get; set; }
	public List<object> Notes = new List<object>();
	public List<object> Keys = new List<object>();
	public List<int> NotesT = new List<int>();
	public long startedtime {get ;set; }
	public TextureRect Beatmap_Background {get;set;}
	public void _perfect(Rid areaRid, Area2D area, long areaShapeIndex, long localShapeIndex){
		SettingsOperator.Gameplaycfg["max"] +=1;
		area.Visible = false;
	}
	public override void _Ready()
	{
		SettingsOperator = GetNode<SettingsOperator>("/root/SettingsOperator");
		Beatmap_Background = GetNode<TextureRect>("./Beatmap_Background");
		Beatmap_Background.SelfModulate = new Color(1f-(1f*(SettingsOperator.backgrounddim*0.01f)),1f-(1f*(SettingsOperator.backgrounddim*0.01f)),1f-(1f*(SettingsOperator.backgrounddim*0.01f)));
		Control P = GetNode<Control>("Playfield");
		Ttiming = GetNode<Label>("Time");
		Hits = GetNode<Label>("Hits");
		foreach (int i in Enumerable.Range(1, 4)){
			Keys.Add(GetNode<ColorRect>("Playfield/KeyBoxes/Key"+i));
		}
		
		var noteblock = GD.Load<PackedScene>("res://Panels/GameplayElements/Static/note.tscn");
        using var file = FileAccess.Open(SettingsOperator.Sessioncfg["beatmapurl"].ToString(), FileAccess.ModeFlags.Read);
        var text = file.GetAsText();
        var lines = text.Split("\n");
		var part = 0;
		var timing = 0;
		var t = "";
        var isHitObjectSection = false;
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
				var note = noteblock.Instantiate().GetNode<Area2D>(".");
				var notetexture = noteblock.Instantiate().GetNode<Sprite2D>("./Notetext");
				GetNode<ColorRect>("Playfield/Chart").AddChild(note);
				note.Position = new Vector2((part * 100), -timing);
				notetexture.Texture = GD.Load<Texture2D>("res://Skin/Game/note.svg");
				notetexture.Modulate = new Color("#70baff");
				Notes.Add(note);
				NotesT.Add(-timing);
            }
        }
		AudioPlayer.Instance.Play();
		startedtime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
//		GD.Print(timing+" "+part);
//		GetTree().Quit();
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	private static void OnGuiInput(InputEvent inputEvent) {
		GD.Print(inputEvent);
	}
	public override void _Process(double delta)
	{   
		long unixTimeMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
		float est = unixTimeMilliseconds-startedtime;
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
		Ttiming.Text = "Time: "+est+"\nSpeed:" + SettingsOperator.Sessioncfg["songspeed"];
		Hits.Text = "Hits:\n" + SettingsOperator.Gameplaycfg["max"] + "\n" + SettingsOperator.Gameplaycfg["great"] + "\n" + SettingsOperator.Gameplaycfg["meh"] + "\n" + SettingsOperator.Gameplaycfg["bad"] + "\n";
		foreach (ColorRect self in Keys)
		{
			if (Input.IsActionPressed("Key"+(Keyx+1)))
			{
				self.Color = new Color(0.32f,0.42f,0.74f);
			}
			else
			{
				self.Color = new Color(0.5f,0.5f,0.5f);
			}
			Keyx++;
		}
		if (Input.IsActionJustPressed("pausemenu")){
			SettingsOperator.toppaneltoggle();
			GetTree().ChangeSceneToFile("res://Panels/Screens/song_select.tscn");
		}
		var viewportSize = GetViewportRect().Size.Y;
		foreach (Area2D self in Notes)
		{
			float notex = NotesT[Ttick] + (long)est + viewportSize/2;
			if (notex > 0 && notex < viewportSize-100){
				self.Position = new Vector2(self.Position.X, notex);
				//self.Visible = true;
			} else {
				//self.Visible = false;
			}
			Ttick++;
		}
	}
}
