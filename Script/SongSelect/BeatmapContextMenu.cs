using Godot;
using System;
using System.IO;

public partial class BeatmapContextMenu : PanelContainer
{
	private SettingsOperator SettingsOperator { get; set; }
	public int SongID { get; set; } = -1;
	public override void _Ready()
	{
		SettingsOperator = GetNode<SettingsOperator>("/root/SettingsOperator");
	}

	private void _edit()
	{
		if (SongID != -1)
		{
			GetNode<SceneTransition>("/root/Transition").Switch("res://Panels/Screens/Create.tscn");
		}
	}

	private void _deletebeatmap(){
		if (SongID != -1)
		{
			var beatmap = SettingsOperator.Beatmaps[SongID];
			var SetID = beatmap.SetID;
			if (beatmap != null)
			{
				string message = $"Deleted {beatmap.Artist} - {beatmap.Title}";
				Notify.Post(message);
			}
			if (Directory.Exists(beatmap.Path))
			{
				Directory.Delete(beatmap.Path, true);
			}
			SettingsOperator.Beatmaps.RemoveAll(beatmap => beatmap.SetID == SetID);
			SettingsOperator.Sessioncfg["reloaddb"] = true;

			if (SettingsOperator.SongID >= SettingsOperator.Beatmaps.Count - 1)
			{
				SettingsOperator.SongID = SettingsOperator.Beatmaps.Count - 1;
			}
			SettingsOperator.SelectSongID(SettingsOperator.SongID);
		}
		else
		{
			Notify.Post("Can't delete beatmap, it doesn't exist.");
		}
	}
	private void _delete()
	{
		if (SongID != -1)
		{
			var beatmap = SettingsOperator.Beatmaps[SongID];
			if (beatmap != null)
			{
				string message = $"Deleted {beatmap.Artist} - {beatmap.Title} [{beatmap.Version}]";
				Notify.Post(message);
			}
			SettingsOperator.Beatmaps.Remove(beatmap);
			SettingsOperator.Sessioncfg["reloaddb"] = true;
			if (File.Exists(beatmap.Rawurl))
			{
				File.Delete(beatmap.Rawurl);
			}

			if (SettingsOperator.SongID >= SettingsOperator.Beatmaps.Count - 1)
			{
				SettingsOperator.SongID = SettingsOperator.Beatmaps.Count - 1;
			}
			SettingsOperator.SelectSongID(SettingsOperator.SongID);
		}
		else
		{
			Notify.Post("Can't delete beatmap, it doesn't exist.");
		}
	}
}
