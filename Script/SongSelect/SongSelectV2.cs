using Godot;
using System;
using System.Collections.Generic;

public partial class SongSelectV2 : Control
{
	private ScrollBar scrollBar;
	private HBoxContainer SongBoomBox;
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		scrollBar = GetNode<ScrollBar>("SongScroll");
		SongBoomBox = GetNode<HBoxContainer>("SongBoomBox");

		scrollBar.Value = (int)SettingsOperator.Sessioncfg["SongID"];
		foreach (BeatmapLegend beatmap in SettingsOperator.Beatmaps)
		{
			var Disc = GD.Load<PackedScene>("res://Panels/SongSelectV2/MusicDisc.tscn").Instantiate();//GetNode<>("MusicDisc").SetMeta(beatmap);
			Disc.SetMeta("songtitle", beatmap.Title);
			Disc.SetMeta("songartist", beatmap.Artist);
			Disc.SetMeta("songmapper", beatmap.Mapper);
			Disc.SetMeta("image", beatmap.Path + beatmap.Background);
			SongBoomBox.AddChild(Disc);
		}
	}
	private void _Exit()
	{
		GetNode<SceneTransition>("/root/Transition").Switch("res://Panels/Screens/home_screen.tscn");
	}
	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		scrollBar.MaxValue = SettingsOperator.Beatmaps.Count;
	}
}
