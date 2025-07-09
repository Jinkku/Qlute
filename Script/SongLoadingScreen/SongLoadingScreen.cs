using Godot;
using System;
using System.Collections.Generic;

public partial class SongLoadingScreen : Control
{
	// Called when the node enters the scene tree for the first time.
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
		Animation = CreateTween();
		Animation.SetParallel(true);
		Animation.TweenProperty(PicturePanel, "position", new Vector2(0,PicturePanel.Position.Y), 0.5f).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
		Animation.TweenProperty(InfoBox, "modulate", new Color(1f,1f,1f,1f), 0.5f).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
		Animation.TweenProperty(PicturePanel, "modulate", new Color(1f,1f,1f,1f), 0.5f).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
		Animation.Play();
		Animation.Connect("finished", new Callable(this, nameof(_Animationf)));
		SongTitle = GetNode<Label>("InfoBox/Box/Title");
		SongArtist = GetNode<Label>("InfoBox/Box/Artist");
		SongDiff = GetNode<Label>("InfoBox/Box/Difficulty");
		SongMapper = GetNode<Label>("InfoBox/Box/Creator");
		SongRating = GetNode<Label>("InfoBox/Box/Infos/Sections/Level");
		SongTitle.Text = SettingsOperator.Sessioncfg["beatmaptitle"]?.ToString() ?? "No Beatmaps Selected";
		SongArtist.Text = SettingsOperator.Sessioncfg["beatmapartist"]?.ToString() ?? "Please select a Beatmap!";
		SongMapper.Text = "Creator: " + SettingsOperator.Sessioncfg["beatmapmapper"]?.ToString();
		SongDiff.Text = SettingsOperator.Sessioncfg["beatmapdiff"]?.ToString();
		SongRating.Text = "Lv. " + ((Single)SettingsOperator.Sessioncfg["levelrating"]).ToString("N0");
	}
	private void _Animationf(){
		ArtificialLoad.Start();
	}
	private void _on_back(){
		if (SettingsOperator.Marathon) GetNode<SceneTransition>("/root/Transition").Switch("res://Panels/Screens/MarathonMode.tscn");
		else GetNode<SceneTransition>("/root/Transition").Switch("res://Panels/Screens/song_select.tscn");
	}
	private void _Timer_load(){
		SettingsOperator.toppaneltoggle();
		ArtificialLoad.Stop();
		GetNode<SceneTransition>("/root/Transition").Switch("res://Panels/Screens/Gameplay.tscn");
	}
	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		GetNode<PanelContainer>("DisclaimerInfo").Visible = SettingsOperator.Marathon;
	}
}
