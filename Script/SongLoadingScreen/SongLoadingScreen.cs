using Godot;
using System;
using System.Collections.Generic;

public partial class SongLoadingScreen : Control
{
	private Tween Animation {get;set;}
	private SettingsOperator SettingsOperator {get;set;}
	private Timer ArtificialLoad {get;set;}
	private Label SongTitle { get; set; }
	private Label SongArtist { get; set; }
	private Label SongMapper { get; set; }
	private Label SongDiff { get; set; }
	private Label SongRating { get; set; }
	private PanelContainer InfoBox { get; set; }
	private PanelContainer PicturePanel { get; set; }
	private TextureRect PictureObj { get; set; }
	
	int anistate = 0;

	public override void _Ready()
	{
		SettingsOperator = GetNode<SettingsOperator>("/root/SettingsOperator");
		// Read + parse the chart (and storyboard) on a worker thread while this screen shows, so the gameplay
		// scene doesn't freeze on a big map. Gameplay picks the result up (ChartPreload.Take*).
		int songId = SettingsOperator.SessionConfig.SongID;
		var beatmap = songId >= 0 && songId < SettingsOperator.Beatmaps.Count ? SettingsOperator.Beatmaps[songId] : null;
		ChartPreload.Start(SettingsOperator.SessionConfig.BeatmapURL, SettingsOperator.SessionConfig.GameMode, beatmap);
		_levelPending = beatmap != null && !beatmap.HasDetails(); // the level needs the map's notes: shown once the worker has them
		InfoBox = GetNode<PanelContainer>("InfoBox");
		PicturePanel = GetNode<PanelContainer>("Previewimage");
		PicturePanel.Position = new Vector2(-125, PicturePanel.Position.Y);
		PictureObj = GetNode<TextureRect>("Previewimage/a/Objimg");
		PictureObj.Size = new Vector2(PictureObj.Size.X+100, PictureObj.Size.Y);
		ArtificialLoad =GetNode<Timer>("./Timer");
		PicturePanel.Modulate = new Color(1f,1f,1f,0f);
		InfoBox.Modulate = new Color(1f,1f,1f,0f);

		// Fetch the existing EQ effect from the bus (slot 0)
		Animation = CreateTween();
		Animation.SetParallel(true);
		Animation.TweenProperty(PicturePanel, "position", new Vector2(0,PicturePanel.Position.Y), 0.5f).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
		Animation.TweenProperty(InfoBox, "modulate", new Color(1f,1f,1f,1f), 0.5f).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
		Animation.TweenProperty(PicturePanel, "modulate", new Color(1f,1f,1f,1f), 0.5f).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
		AudioPlayer.Instance.MuffledEQ();
		Animation.Play();
		Animation.Connect("finished", new Callable(this, nameof(_Animationf)));
		SongTitle = GetNode<Label>("InfoBox/Box/Title");
		SongArtist = GetNode<Label>("InfoBox/Box/Artist");
		SongDiff = GetNode<Label>("InfoBox/Box/Difficulty");
		SongMapper = GetNode<Label>("InfoBox/Box/Creator");
		SongRating = GetNode<Label>("InfoBox/Box/Infos/Sections/Level");
		SongTitle.Text = SettingsOperator.SessionConfig.BeatmapTitle?.ToString() ?? "No Beatmaps Selected";
		SongArtist.Text = SettingsOperator.SessionConfig.BeatmapArtist?.ToString() ?? "Please select a Beatmap!";
		SongMapper.Text = "Creator: " + SettingsOperator.SessionConfig.BeatmapMapper?.ToString();
		SongDiff.Text = SettingsOperator.SessionConfig.BeatmapDifficultyName?.ToString();
		SongRating.Text = _levelPending ? "Lv. ..." : "Lv. " + SettingsOperator.GetLevelWithMods();
		if (AudioPlayer.isMasterMuted()) Notify.Post("Your music is muted. Please unmute pls :<", Type: NotificationIcons.NotificationType.Warning);
	}
	private void _Animationf(){
		ArtificialLoad.Start();
	}
	
	private void _on_back(){
		_goWhenLoaded = false;
		_levelPending = false;
		ChartPreload.Clear();
		AudioPlayer.Instance.RevertEQ();
		if (LanNetwork.InMatch)
		{
			LanNetwork.Instance.ReportFinished(quit: true);
			GetNode<SceneTransition>("/root/Transition").Switch(LanNetwork.Instance.ReturnScene);
		}
		else if (SettingsOperator.Marathon) GetNode<SceneTransition>("/root/Transition").Switch("res://Panels/Screens/MarathonMode.tscn");
		else GetNode<SceneTransition>("/root/Transition").Switch("res://Panels/Screens/song_select.tscn");
		ArtificialLoad.Stop();
	}
	private bool _levelPending;

	/// <summary>The timer ran out but the chart is still loading: switch as soon as it's done (see _Process).</summary>
	private bool _goWhenLoaded;

	private void _Timer_load(){
		ArtificialLoad.Stop();
		if (ChartPreload.Busy) { _goWhenLoaded = true; return; } // a big map: keep this screen up (animating) until it's parsed
		StartGameplay();
	}

	private void StartGameplay(){
		_goWhenLoaded = false;
		AudioPlayer.Instance.RevertEQ();
		SettingsOperator.toppaneltoggle(false);
		ArtificialLoad.Stop();
		Animation?.Kill();
		GetNode<SceneTransition>("/root/Transition").Switch(SettingsOperator.ReturnGameModeTscn(SettingsOperator.SessionConfig.GameMode));
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		if (_levelPending && !ChartPreload.Busy)
		{
			_levelPending = false;
			SongRating.Text = "Lv. " + SettingsOperator.GetLevelWithMods();
		}
		if (_goWhenLoaded && !ChartPreload.Busy) StartGameplay();
	}
}
