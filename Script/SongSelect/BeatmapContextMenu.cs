using Godot;
using System;
using System.IO;

public partial class BeatmapContextMenu : PanelContainer
{
	private SettingsOperator SettingsOperator { get; set; }
	private Button EditNode { get; set; }
	public int SongID { get; set; } = -1;
	public override void _Ready()
	{
		SettingsOperator = GetNode<SettingsOperator>("/root/SettingsOperator");
		EditNode = GetNode<Button>("ContextSections/Edit");
		#if DEBUG
			EditNode.Visible = true;
		#else
			EditNode.Visible = false;
		#endif
	}

	private void _edit()
	{
		if (SongID != -1)
		{
			CreateEditor.EditorSongInfo.LoadFromBeatmap(SettingsOperator.Beatmaps[SongID]);
			GetNode<SceneTransition>("/root/Transition").Switch("res://Panels/Screens/Create.tscn");
		}
	}

	/// <summary>Uploads the whole set this difficulty belongs to (Script/Submission/BeatmapSubmit.cs).</summary>
	private void _submit()
	{
		if (SongID < 0 || SongID >= SettingsOperator.Beatmaps.Count)
		{
			Notify.Post("Pick a beatmap to submit first.", Type: NotificationIcons.NotificationType.Warning);
			return;
		}
		var beatmap = SettingsOperator.Beatmaps[SongID];
		var folder = !string.IsNullOrEmpty(beatmap.Path) && Directory.Exists(beatmap.Path)
			? beatmap.Path
			: System.IO.Path.GetDirectoryName(beatmap.Rawurl ?? "");
		_ = BeatmapSubmit.SubmitWithNotifications(folder, $"{beatmap.Artist} - {beatmap.Title}");
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
			SettingsOperator.SessionConfig.ReloadDB = true;

			if (SettingsOperator.SessionConfig.SongID >= SettingsOperator.Beatmaps.Count - 1)
			{
				SettingsOperator.SessionConfig.SongID = SettingsOperator.Beatmaps.Count - 1;
			}
			SettingsOperator.SelectSongID(SettingsOperator.SessionConfig.SongID);
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
			SettingsOperator.SessionConfig.ReloadDB = true;
			if (File.Exists(beatmap.Rawurl))
			{
				File.Delete(beatmap.Rawurl);
			}

			if (SettingsOperator.SessionConfig.SongID >= SettingsOperator.Beatmaps.Count - 1)
			{
				SettingsOperator.SessionConfig.SongID = SettingsOperator.Beatmaps.Count - 1;
			}
			SettingsOperator.SelectSongID(SettingsOperator.SessionConfig.SongID);
		}
		else
		{
			Notify.Post("Can't delete beatmap, it doesn't exist.", Type:NotificationIcons.NotificationType.Warning);
		}
	}
}
