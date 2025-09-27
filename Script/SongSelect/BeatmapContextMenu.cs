using Godot;
using System;
using System.IO;

public partial class BeatmapContextMenu : PanelContainer
{
	private SettingsOperator SettingsOperator { get; set; }
	public override void _Ready()
	{
		SettingsOperator = GetNode<SettingsOperator>("/root/SettingsOperator");
	}

	private void _deletebeatmap(){
		if ((int)GetMeta("SongID") is int songID && songID != -1)
		{
			var beatmap = SettingsOperator.Beatmaps.Find(b => b.ID == songID);
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
	}
	private void _delete()
	{
		if ((int)GetMeta("SongID") is int songID && songID != -1)
		{
			var beatmap = SettingsOperator.Beatmaps.Find(b => b.ID == songID);
			if (beatmap != null)
			{
				string message = $"Deleted {beatmap.Artist} - {beatmap.Title} [{beatmap.Version}]";
				Notify.Post(message);
			}
			SettingsOperator.Beatmaps.RemoveAll(beatmap => beatmap.ID == songID);
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
	}
}
