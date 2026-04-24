using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class Create : Control
{
	// Called when the node enters the scene tree for the first time.
	private NoteSkinning NoteHighlight { get; set; } = null;
	private int NoteID { get; set; } = 0;
	private Control Playfield { get; set; }
	private Label Time { get; set; }
	private Label NoteCount { get; set; }
	private Control InfoN { get; set; }
	private Control ComposeN { get; set; }
	private Control TimingN { get; set; }
	private Control VerifyN { get; set; }
	private int BPM { get; set; } = 180;
	private List<NoteSkinning> Notes { get; set; } = new List<NoteSkinning>();
	private AudioStreamPlayer EditorPlayer { get; set; }
	private TextureRect EditorBackground { get; set; }
	private TextureRect EditorBackgroundPreview { get; set; }
	private HSlider Seeker { get; set; }
	private Label TimeClock { get; set; }
	private Label SelectedPos { get; set; }
	private Control SectionSample { get; set; }
	private float SizeYSection { get; set; }
	private LineEdit SongTitle { get; set; }
	private LineEdit SongArtist { get; set; }
	private LineEdit SongDifficulty { get; set; }
	private ButtonFade PlayPause { get; set; }
	private string ParentPath { get; set; }

public override void _Ready()
	{
		if (AudioPlayer.Instance.Stream != null)
			AudioPlayer.Instance.StreamPaused = true;
		SelectedPos = GetNode<Label>("Compose/Info/ContextSections/SelectedPos");
		Seeker = GetNode<HSlider>("ControlPanel/Box/PlayerControl/Seek");
		EditorPlayer = GetNode<AudioStreamPlayer>("EditorPlayer");
		EditorBackground = GetNode<TextureRect>("EditorBackground");
		EditorBackgroundPreview = GetNode<TextureRect>("Info/Panel/Scroll/GridContainer/Setting5/Preview");
		TimeClock = GetNode<Label>("ControlPanel/Box/TimeBox/Time");
		Time = GetNode<Label>("ControlPanel/Box/TimeBox/Time");
		NoteCount = GetNode<Label>("Compose/Info/ContextSections/NoteCount");
		InfoN = GetNode<Control>("Info");
		ComposeN = GetNode<Control>("Compose");
		Playfield = ComposeN.GetNode<Control>("Ground/Playfield");
		SectionSample = NoteSectionSelect(1);
		SizeYSection = SectionSample.Size.Y;
		TimingN = GetNode<Control>("Timing");
		VerifyN = GetNode<Control>("Verify");
		InfoN.Modulate = new Color(1, 1, 1, 0f);
		InfoN.MouseFilter = MouseFilterEnum.Ignore;
		InfoN.Visible = true;
		ComposeN.Modulate = new Color(1, 1, 1, 1f);
		ComposeN.MouseFilter = MouseFilterEnum.Stop;
		ComposeN.Visible = true;
		TimingN.Modulate = new Color(1, 1, 1, 0f);
		TimingN.MouseFilter = MouseFilterEnum.Ignore;
		TimingN.Visible = true;
		VerifyN.Modulate = new Color(1, 1, 1, 0f);
		VerifyN.MouseFilter = MouseFilterEnum.Ignore;
		VerifyN.Visible = true;
		PlayPause = GetNode<ButtonFade>("ControlPanel/Box/Player/Columns/PlayPause");
		
		SongTitle = GetNode<LineEdit>("Info/Panel/Scroll/GridContainer/Setting1/Value");
		SongArtist = GetNode<LineEdit>("Info/Panel/Scroll/GridContainer/Setting2/Value");
		SongDifficulty = GetNode<LineEdit>("Info/Panel/Scroll/GridContainer/Setting3/Value");
		SongTitle.Text = CreateEditor.EditorSongInfo.SongTitle;
		SongArtist.Text = CreateEditor.EditorSongInfo.SongArtist;
		SongDifficulty.Text = CreateEditor.EditorSongInfo.SongDifficulty;
		EditorBackground.Texture = CreateEditor.EditorSongInfo.Background;
		EditorBackgroundPreview.Texture = EditorBackground.Texture;
		ParentPath = CreateEditor.EditorSongInfo.FilePath.TrimEnd(CreateEditor.EditorSongInfo.FilePath.Split("/").Last()).ToString();
		if (CreateEditor.EditorSongInfo.FilePath != null) 
			ReloadBeatmap(CreateEditor.EditorSongInfo.FilePath);

	}

	private void SectionResized()
	{
		SizeYSection =  SectionSample.Size.Y;
	}
	private Tween MenuTween { get; set; }
	/// <summary>
	/// 0 = Info
	/// 1 = Compose
	/// 2 = Timing
	/// 3 = Verify
	/// </summary>
	/// <param name="mode">What menu it should transition to.</param>
	private void Menu(int mode)
	{
		MenuTween?.Kill();
		MenuTween = CreateTween();
		MenuTween.SetEase(Tween.EaseType.Out);
		MenuTween.SetTrans(Tween.TransitionType.Cubic);
		MenuTween.SetParallel(true);
		var speed = 0.2f;
		if (mode == 0)
		{
			MenuTween.TweenProperty(InfoN, "modulate:a", 1f, speed);
			MenuTween.TweenCallback(Callable.From(() => InfoN.MouseFilter = MouseFilterEnum.Stop));
			MenuTween.TweenProperty(ComposeN, "modulate:a", 0f, speed);
			MenuTween.TweenCallback(Callable.From(() => ComposeN.MouseFilter = MouseFilterEnum.Ignore));
			MenuTween.TweenCallback(Callable.From(() => ComposeN.Hide())).SetDelay(speed);
			MenuTween.TweenProperty(TimingN, "modulate:a", 0f, speed);
			MenuTween.TweenCallback(Callable.From(() => TimingN.MouseFilter = MouseFilterEnum.Ignore));
			MenuTween.TweenProperty(VerifyN, "modulate:a", 0f, speed);
			MenuTween.TweenCallback(Callable.From(() => VerifyN.MouseFilter = MouseFilterEnum.Ignore));
		} else if (mode == 1)
		{
			MenuTween.TweenCallback(Callable.From(() => ComposeN.Show()));
			MenuTween.TweenProperty(InfoN, "modulate:a", 0f, speed);
			MenuTween.TweenCallback(Callable.From(() => InfoN.MouseFilter = MouseFilterEnum.Ignore));
			MenuTween.TweenProperty(ComposeN, "modulate:a", 1f, speed);
			MenuTween.TweenCallback(Callable.From(() => ComposeN.MouseFilter = MouseFilterEnum.Pass));
			MenuTween.TweenProperty(TimingN, "modulate:a", 0f, speed);
			MenuTween.TweenCallback(Callable.From(() => TimingN.MouseFilter = MouseFilterEnum.Ignore));
			MenuTween.TweenProperty(VerifyN, "modulate:a", 0f, speed);
			MenuTween.TweenCallback(Callable.From(() => VerifyN.MouseFilter = MouseFilterEnum.Ignore));
		} else if (mode == 2)
		{
			MenuTween.TweenCallback(Callable.From(() => ComposeN.Hide())).SetDelay(speed);
			MenuTween.TweenProperty(InfoN, "modulate:a", 0f, speed);
			MenuTween.TweenCallback(Callable.From(() => InfoN.MouseFilter = MouseFilterEnum.Ignore));
			MenuTween.TweenProperty(ComposeN, "modulate:a", 0f, speed);
			MenuTween.TweenCallback(Callable.From(() => ComposeN.MouseFilter = MouseFilterEnum.Ignore));
			MenuTween.TweenProperty(TimingN, "modulate:a", 1f, speed);
			MenuTween.TweenCallback(Callable.From(() => TimingN.MouseFilter = MouseFilterEnum.Stop));
			MenuTween.TweenProperty(VerifyN, "modulate:a", 0f, speed);
			MenuTween.TweenCallback(Callable.From(() => VerifyN.MouseFilter = MouseFilterEnum.Ignore));
		} else if (mode == 3)
		{
			MenuTween.TweenCallback(Callable.From(() => ComposeN.Hide())).SetDelay(speed);
			MenuTween.TweenProperty(InfoN, "modulate:a", 0f, speed);
			MenuTween.TweenCallback(Callable.From(() => InfoN.MouseFilter = MouseFilterEnum.Ignore));
			MenuTween.TweenProperty(ComposeN, "modulate:a", 0f, speed);
			MenuTween.TweenCallback(Callable.From(() => ComposeN.MouseFilter = MouseFilterEnum.Ignore));
			MenuTween.TweenProperty(TimingN, "modulate:a", 0f, speed);
			MenuTween.TweenCallback(Callable.From(() => TimingN.MouseFilter = MouseFilterEnum.Ignore));
			MenuTween.TweenProperty(VerifyN, "modulate:a", 1f, speed);
			MenuTween.TweenCallback(Callable.From(() => VerifyN.MouseFilter = MouseFilterEnum.Stop));
		}
		MenuTween.Play();
	}

	private void Info()
	{
		Menu(0);
	}
	private void Compose()
	{
		Menu(1);
	}
	private void Timing()
	{
		Menu(2);
	}
	private void Verify()
	{
		Menu(3);
	}

	private float smoothTime = 0f;
	public long startedtime { get; set; } = 0;
	private double EditorTime { get; set; } = 0;
	/// <summary>
	/// Game Clock
	/// </summary>
	public float GetRemainingTime(float delta = 1f)
	{
		if (AudioPlayer.Instance.Playing)
		{
			// increment smoothly
			smoothTime += delta * AudioPlayer.Instance.PitchScale;

			// occasional hard resync if drift gets big
			float truePos = AudioPlayer.Instance.GetPlaybackPosition()
			                + (float)AudioServer.GetTimeSinceLastMix();

			if (Mathf.Abs(truePos - smoothTime) > 0.05f) // 50ms tolerance
				smoothTime = truePos;
		}

		EditorTime = smoothTime;
		EditorTime -= startedtime;

		return (float)EditorTime;
	}

	private Control NoteSectionSelect(int NoteID) =>
		Playfield.GetNode<Control>($"ChartSections/Section{NoteID}/Control");
	private void Place()
	{
		if (NoteID != 0)
		{
			var Notetmp = GD.Load<PackedScene>("res://Panels/GameplayElements/Static/note.tscn").Instantiate().GetNode<NoteSkinning>(".");
			Notetmp.NotePart = NoteID - 1;
			Notes.Add(Notetmp);
			NoteSectionSelect(NoteID).AddChild(Notetmp);
			Notetmp.Time = NoteHighlight.Time;
			Notetmp.Position = new Vector2(NoteHighlight.Position.X, SizeYSection - Notetmp.Time - 200 + (float)SongProgress);
		}
	}

	public override void _ExitTree()
	{
		SettingsOperator.CreateSelectingBeatmap = false;
		if (AudioPlayer.Instance.Stream != null)
			AudioPlayer.Instance.StreamPaused = false;
	}

	private void _FileMenu(int ID)
	{
		if (ID == 1)
		{
			GetNode<SceneTransition>("/root/Transition").Switch("res://Panels/Screens/song_select.tscn");
			SettingsOperator.CreateSelectingBeatmap = true;
		} else if (ID == 5)
		{
			_on_back();
		}
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	private void _on_back(){
		GetNode<SceneTransition>("/root/Transition").Switch("res://Panels/Screens/home_screen.tscn");
	}
	private void _enter(int id)
	{
		id += 1;
		var s = ComposeN.GetNode<Label>("Info/ContextSections/Na");
		s.Text = id.ToString();
		NoteID = id;
		var Notetmp = GD.Load<PackedScene>("res://Panels/GameplayElements/Static/note.tscn").Instantiate().GetNode<NoteSkinning>(".");
		Notetmp.Modulate = new Color(1f, 1f, 1f, 0.5f);
		Notetmp.NotePart = NoteID - 1;
		if (NoteHighlight == null)
		{
			Playfield.GetNode<Control>($"ChartSections/Section{id.ToString()}/Control").AddChild(Notetmp);
			NoteHighlight = Notetmp;
		}
	}
	private void _off(int id)
	{
		NoteID = 0;
		var s = ComposeN.GetNode<Label>("Info/ContextSections/Na");
		s.Text = "off";
		NoteHighlight?.QueueFree();
		NoteHighlight = null;
	}
	private int MouseTicker { get; set; }

	public override void _Input(InputEvent @event)
	{
		if (@event is InputEventMouseButton mb &&
		    mb.ButtonIndex == MouseButton.Left)
		{
			if (mb.Pressed && MouseTicker == 0)
			{
				Place();
				MouseTicker = 1;
			}
			else if (!mb.Pressed && MouseTicker == 1)
			{
				MouseTicker = 0;
			}
		}
	}
	private double SongProgress => Seeker.Value * 1000;
	private void ValueChanged(float value)
	{
		// reference 
		//Notetmp.Time = NoteHighlight.Time;
		//Notetmp.Position = new Vector2(NoteHighlight.Position.X, SizeYSection - Notetmp.Time - 200);
		Time.Text = TimeSpan.FromMilliseconds(value * 1000).ToString(@"hh\:mm\:ss\.fff");
		foreach (var note in Notes)
		{
			note.Position = new Vector2(note.Position.X,SizeYSection - note.Time - 200 + (float)SongProgress);
		}
	}
	private List<DanceCounter> dance { get; set; }
	public void ReloadBeatmap(string filepath)
	{
		Notes.Clear();
		GD.Print(filepath);
		using var file = FileAccess.Open(filepath, FileAccess.ModeFlags.Read);
		var text = file.GetAsText();
		var lines = text.Split("\n");
		var part = 0;
		var timing = 0;
		var timen = -1;
		var isHitObjectSection = false;
		int index = 0;
		BeatmapLegend beatmap = SettingsOperator.Beatmaps[SettingsOperator.SessionConfig.SongID];
		dance = beatmap.Dance;

		var seen = new HashSet<(int timing, int section)>();

		foreach (string line in lines)
		{

			if (line.Contains(":")) 
			{
				var parts = line.Split(":", 2);
				var key = parts[0].Trim();
				var value = parts[1].Trim();
				switch (key)
				{
					case "AudioFilename": EditorPlayer.Stream = new AudioPlayer().AutoDetectFormat(ParentPath.PathJoin(value)); break;
				}
			} 
			if (line.Trim() == "[HitObjects]") { isHitObjectSection = true; continue; }
			if (string.IsNullOrWhiteSpace(line) || line.StartsWith('[')) continue;
			if (!isHitObjectSection) continue;

			
			string[] section = line.Split(':', ',');	
			timing = Convert.ToInt32(section[2]);
			part = Convert.ToInt32(section[0]);
			part = Math.Clamp((int)Math.Floor(part * 4 / 512.0), 0, 3);

			timen = -timing;
			var keyb = (timen, part);

			if (seen.Contains(keyb)) { index++; continue; }
			seen.Add(keyb);
			_enter(part);
			NoteHighlight.Time = -timen;
			
//			Notes.Add(new NotesEn #Disable for now
//			{
//				timing = timen,
//				NoteSection = part,
//				ppv2xp = beatmap.ppv2sets[index] * ModsMulti.multiplier
//			});
			//			Notetmp.NotePart = NoteID - 1;
			// Notetmp.Time = NoteHighlight.Time;
			Place();
			_off(NoteID);
			index++;
		}
		Seeker.MaxValue = -timen * 0.001f;
	}
	public override void _Process(double delta)
	{
		EditorPlayer.VolumeDb = AudioPlayer.Instance.VolumeDb;
		NoteCount.Text = $"{Notes.Count:N0} Notes";
		if (NoteHighlight != null)
		{
			NoteHighlight.Time = SizeYSection - SettingsOperator.MouseMovement.Y - NoteHighlight.Texture.GetSize().Y + (float)SongProgress;
			NoteHighlight.Position = new Vector2(NoteHighlight.Position.X, SizeYSection - NoteHighlight.Time - 200 + (float)SongProgress);
			SelectedPos.Text = $"posy: {NoteHighlight.Position.Y + SongProgress:N0} {NoteHighlight.Time:N0}";
		}
	}
}
