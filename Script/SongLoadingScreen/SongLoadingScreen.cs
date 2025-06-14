using Godot;
using System;

public partial class SongLoadingScreen : Control
{
	// Called when the node enters the scene tree for the first time.
	public AnimationPlayer Animation {get;set;}
	public SettingsOperator SettingsOperator {get;set;}
	public Timer ArtificialLoad {get;set;}
	public Label SongTitle { get; set; }
	public Label SongArtist { get; set; }
	public Label SongMapper { get; set; }
	public Label SongDiff { get; set; }
	public Label SongRating { get; set; }
	int anistate = 0;

	public override void _Ready()
	{
		SettingsOperator = GetNode<SettingsOperator>("/root/SettingsOperator");
		ArtificialLoad=GetNode<Timer>("./Timer");
		Animation=GetNode<AnimationPlayer>("./Wafuk");
		Animation.Play("AnimationSongTick");
		SongTitle = GetNode<Label>("InfoBox/H/Box/Title");
		SongArtist = GetNode<Label>("InfoBox/H/Box/Artist");
		SongDiff = GetNode<Label>("InfoBox/H/Box/Difficulty");
		SongMapper = GetNode<Label>("InfoBox/H/Box/Creator");
		SongRating = GetNode<Label>("InfoBox/H/Box/Infos/Sections/Level");
		SongTitle.Text = SettingsOperator.Sessioncfg["beatmaptitle"]?.ToString() ?? "No Beatmaps Selected";
		SongArtist.Text = SettingsOperator.Sessioncfg["beatmapartist"]?.ToString() ?? "Please select a Beatmap!";
		SongMapper.Text = "Creator: " + SettingsOperator.Sessioncfg["beatmapmapper"]?.ToString();
		SongDiff.Text = SettingsOperator.Sessioncfg["beatmapdiff"]?.ToString();
		int rating = (int)((double)SettingsOperator.Sessioncfg["levelrating"]);
		SongRating.Text = "Lv. " + rating.ToString("N0");
	}
	private void _Animationf(string ani){
		ArtificialLoad.Start();
		if (anistate <1){
			anistate++;
		}else{
			GetNode<SceneTransition>("/root/Transition").Switch("res://Panels/Screens/Gameplay.tscn");
		}
	}
	private void _on_back(){
		GetNode<SceneTransition>("/root/Transition").Switch("res://Panels/Screens/song_select.tscn");
	}
	private void _Timer_load(){
		Animation.PlayBackwards("AnimationSongTick");
		SettingsOperator.toppaneltoggle();
		ArtificialLoad.Stop();
	}
	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}
}
