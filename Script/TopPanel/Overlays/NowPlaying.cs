using Godot;
using System;

public partial class NowPlaying : PanelContainer
{
	public SettingsOperator SettingsOperator { get; set; }
	private TextureRect Background { get; set; }
	private Button Pause { get; set; }
	private Texture2D PauseIcon { get; set; }
	private Texture2D PlayIcon { get; set; }
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		SettingsOperator = GetNode<SettingsOperator>("/root/SettingsOperator");
		Background = GetNode<TextureRect>("V/Banner");
		Pause = GetNode<Button>("V/PlayerControls/Play");
		PlayIcon = GD.Load<Texture2D>("res://Resources/System/MusicPlayer/Play.png");
		PauseIcon = GD.Load<Texture2D>("res://Resources/System/MusicPlayer/Pause.png");
	}

	private void Play()
	{
		if (SettingsOperator.Beatmaps.Count > 0 && AudioPlayer.Instance.Stream != null)
		{
			AudioPlayer.Instance.StreamPaused = !AudioPlayer.Instance.StreamPaused;
		}
	}
	private void Prev()
	{
		
		if ((int)SettingsOperator.Sessioncfg["SongID"] - 1 >= 0 && (int)SettingsOperator.Sessioncfg["SongID"] != -1)
		{
			SettingsOperator.SelectSongID((int)SettingsOperator.Sessioncfg["SongID"] - 1);
		}
		else if ((int)SettingsOperator.Sessioncfg["SongID"] != -1)
		{
			SettingsOperator.SelectSongID((int)SettingsOperator.Beatmaps.Count - 1);
		}
	}
	private void Next()
	{
		if ((int)SettingsOperator.Sessioncfg["SongID"] + 1 < (int)SettingsOperator.Beatmaps.Count && (int)SettingsOperator.Sessioncfg["SongID"] != -1)
		{
			SettingsOperator.SelectSongID((int)SettingsOperator.Sessioncfg["SongID"] + 1);
		}
		else if ((int)SettingsOperator.Sessioncfg["SongID"] != -1)
		{
			SettingsOperator.SelectSongID(0);
		}
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		if (AudioPlayer.Instance.StreamPaused)
		{
			Pause.Icon = PlayIcon;
		}
		else
		{
			Pause.Icon = PauseIcon;
		}
		if (Background.Texture != SettingsOperator.Sessioncfg["background"] && (int)SettingsOperator.Sessioncfg["SongID"] != -1)
		{
			Background.Texture = (Texture2D)SettingsOperator.Sessioncfg["background"];
		}
	}
}
