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
	public override void _Ready()
	{
		SettingsOperator = GetNode<SettingsOperator>("/root/SettingsOperator");
		Control P = GetNode<Control>("Playfield");
		Ttiming = GetNode<Label>("Time");
		Hits = GetNode<Label>("Hits");
		foreach (int i in Enumerable.Range(1, 4)){
			Keys.Add(GetNode<ColorRect>("Playfield/KeyBoxes/Key"+i));
		}
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
				var note = new Sprite2D();
				GetNode<ColorRect>("Playfield/Chart").AddChild(note);
				note.Position = new Vector2((part * 100), -timing);
				note.Texture = GD.Load<Texture2D>("res://Skin/Game/note.png");
				note.Centered = false;
				note.Modulate = new Color("#70baff");
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
		long est = unixTimeMilliseconds-startedtime;
		//float est = AudioPlayer.Instance.GetPlaybackPosition()*1000;
		int Ttick = 0;
		int Keyx = 0;
		Ttiming.Text = "Time: "+est;
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
			GetTree().ChangeSceneToFile("res://Panels/Screens/song_select.tscn");
		}
		foreach (Sprite2D self in Notes)
		{
			self.Position = new Vector2(self.Position.X, NotesT[Ttick] + est + GetViewportRect().Size.Y/2);
			Ttick++;
		}
	}
}
