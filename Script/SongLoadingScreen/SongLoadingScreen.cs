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
		InfoBox = GetNode<PanelContainer>("InfoBox");
		PicturePanel = GetNode<PanelContainer>("Previewimage");
		PicturePanel.Position = new Vector2(-100, PicturePanel.Position.Y);
		PictureObj = GetNode<TextureRect>("Previewimage/a/Objimg");
		PictureObj.Size = new Vector2(PictureObj.Size.X+80, PictureObj.Size.Y);
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
		SongRating.Text = "Lv. " + (SettingsOperator.SessionConfig.LevelRating * ModsMulti.multiplier).ToString("N0");
		if (AudioPlayer.isMasterMuted()) Notify.Post("Your music is muted. Please unmute pls :<", Type: NotificationIcons.NotificationType.Warning);
	}
	private void _Animationf(){
		ArtificialLoad.Start();
	}
	
	private void _on_back(){
		AudioPlayer.Instance.RevertEQ();
		if (SettingsOperator.Marathon) GetNode<SceneTransition>("/root/Transition").Switch("res://Panels/Screens/MarathonMode.tscn");
		else GetNode<SceneTransition>("/root/Transition").Switch("res://Panels/Screens/song_select.tscn");
		ArtificialLoad.Stop();
	}
	private void _Timer_load(){
		AudioPlayer.Instance.RevertEQ();
		SettingsOperator.toppaneltoggle(false);
		ArtificialLoad.Stop();
		Animation?.Kill();
		GetNode<SceneTransition>("/root/Transition").Switch(SettingsOperator.ReturnGameModeTscn(SettingsOperator.SessionConfig.GameMode));
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}
}
