using Godot;
using System;
using System.Collections.Generic;

public partial class Create : Control
{
	// Called when the node enters the scene tree for the first time.
	private Sprite2D NoteHighlight { get; set; } = null;
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
	private AudioStreamPlayer EditorPlayer {get; set;}
	private TextureRect EditorBackground { get; set; }
	private HSlider Seeker { get; set; }
	private Label TimeClock { get; set; }
	private Label SelectedPos { get; set; }
public override void _Ready()
	{
		if (AudioPlayer.Instance.Stream != null)
			AudioPlayer.Instance.StreamPaused = true;
		SelectedPos = GetNode<Label>("Compose/Info/ContextSections/SelectedPos");
		Seeker = GetNode<HSlider>("ControlPanel/Box/PlayerControl/Seek");
		EditorPlayer = GetNode<AudioStreamPlayer>("EditorPlayer");
		EditorBackground = GetNode<TextureRect>("EditorBackground");
		TimeClock = GetNode<Label>("ControlPanel/Box/TimeBox/Time");
		Time = GetNode<Label>("ControlPanel/Box/TimeBox/Time");
		NoteCount = GetNode<Label>("Compose/Info/ContextSections/NoteCount");
		InfoN = GetNode<Control>("Info");
		ComposeN = GetNode<Control>("Compose");
		Playfield = ComposeN.GetNode<Control>("Ground/Playfield");
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
	private void Place()
	{
		if (NoteID != 0)
		{
			var Notetmp = GD.Load<PackedScene>("res://Panels/GameplayElements/Static/note.tscn").Instantiate().GetNode<NoteSkinning>(".");
			Notetmp.NotePart = NoteID - 1;
			Notes.Add(Notetmp);
			Playfield.GetNode<Control>($"ChartSections/Section{NoteID}/Control").AddChild(Notetmp);
			Notetmp.Position = NoteHighlight.Position;
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

	private void ValueChanged(float value)
	{
		Time.Text = TimeSpan.FromMilliseconds(value * 1000).ToString(@"hh\:mm\:ss");
		foreach (var note in Notes)
		{
			
		}
	}
	public override void _Process(double delta)
	{
		EditorPlayer.VolumeDb = AudioPlayer.Instance.VolumeDb;
		NoteCount.Text = $"{Notes.Count:N0} Notes";
		if (NoteHighlight != null)
		{
			NoteHighlight.Position = new Vector2(0,  - 150);
			SelectedPos.Text = $"posy: {NoteHighlight.Position.Y}";
		}

	}
}
