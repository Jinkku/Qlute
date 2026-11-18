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
		BuildCollectionsEntry();
	}

	// ------------------------------------------------------------------ collections

	private PopupMenu _collectionsMenu;
	private const int NewCollectionId = 100000;

	/// <summary>"Collections..." (styled like the other entries): a checklist of collections, tick = in it.</summary>
	private void BuildCollectionsEntry()
	{
		var entry = (Button)EditNode.Duplicate(0); // the look only (no signals / script)
		entry.Name = "Collections";
		entry.Text = "Collections...";
		entry.TooltipText = "Add this difficulty to a collection, or take it out of one";
		entry.Visible = true;
		var sections = GetNode<VBoxContainer>("ContextSections");
		sections.AddChild(entry);
		sections.MoveChild(entry, GetNode("ContextSections/Delete").GetIndex());
		entry.Pressed += OpenCollectionsMenu;
	}

	/// <summary>
	/// The checklist popup, made when first needed. It lives on song select (this menu's parent), not on this panel:
	/// song select hides the context menu as soon as you click outside it. It used to go on GetTree().CurrentScene
	/// from _Ready, but during a scene switch that is still the previous screen, which is freed after the fade,
	/// taking the popup with it, so "Collections..." stopped doing anything.
	/// </summary>
	private PopupMenu CollectionsMenu()
	{
		if (IsInstanceValid(_collectionsMenu) && _collectionsMenu.IsInsideTree()) return _collectionsMenu;
		_collectionsMenu = new PopupMenu { HideOnCheckableItemSelection = false };
		_collectionsMenu.AddThemeFontSizeOverride("font_size", 18);
		_collectionsMenu.IdPressed += OnCollectionPicked;
		(GetParent() ?? this).AddChild(_collectionsMenu);
		return _collectionsMenu;
	}

	public override void _ExitTree()
	{
		if (IsInstanceValid(_collectionsMenu)) _collectionsMenu.QueueFree();
	}

	private BeatmapLegend Target => SongID >= 0 && SongID < SettingsOperator.Beatmaps.Count ? SettingsOperator.Beatmaps[SongID] : null;

	private void OpenCollectionsMenu()
	{
		var map = Target;
		if (map == null) return;
		CollectionsMenu().Clear();
		var names = Collections.Names;
		for (int i = 0; i < names.Count; i++)
		{
			_collectionsMenu.AddCheckItem(names[i], i);
			_collectionsMenu.SetItemChecked(_collectionsMenu.GetItemIndex(i), Collections.Contains(names[i], map));
		}
		if (names.Count > 0) _collectionsMenu.AddSeparator();
		_collectionsMenu.AddItem("New collection...", NewCollectionId);
		_collectionsMenu.ResetSize();
		_collectionsMenu.Popup(new Rect2I((Vector2I)GetGlobalMousePosition(), Vector2I.Zero));
	}

	private void OnCollectionPicked(long id)
	{
		var map = Target;
		if (map == null) return;
		if (id == NewCollectionId) { AskNewCollection(map); return; }
		var names = Collections.Names;
		if (id < 0 || id >= names.Count) return;
		var name = names[(int)id];
		int index = _collectionsMenu.GetItemIndex((int)id);
		bool inIt = Collections.Contains(name, map);
		if (inIt) Collections.Remove(name, new[] { map }); else Collections.Add(name, new[] { map });
		_collectionsMenu.SetItemChecked(index, !inIt);
		Notify.Post(inIt ? $"Removed from {name}" : $"Added to {name}", Description: $"{map.Artist} - {map.Title} [{map.Version}]");
	}

	/// <summary>Asks for a name, makes the collection and puts this difficulty in it.</summary>
	private void AskNewCollection(BeatmapLegend map)
	{
		var dialog = new ConfirmationDialog { Title = "New collection", OkButtonText = "Create", MinSize = new Vector2I(500, 0) };
		var name = new LineEdit { PlaceholderText = "Collection name", MaxLength = 60, CustomMinimumSize = new Vector2(450, 0) };
		dialog.AddChild(name);
		dialog.RegisterTextEnter(name);
		dialog.Confirmed += () =>
		{
			var error = Collections.Create(name.Text);
			if (error != null) { Notify.Post("Couldn't make the collection", Description: error, Type: NotificationIcons.NotificationType.Warning); return; }
			Collections.Add(name.Text.Trim(), new[] { map });
			Notify.Post($"Added to {name.Text.Trim()}", Description: $"{map.Artist} - {map.Title} [{map.Version}]");
		};
		dialog.Canceled += dialog.QueueFree;
		dialog.Confirmed += dialog.QueueFree;
		GetTree().Root.AddChild(dialog);
		dialog.PopupCentered();
		name.GrabFocus();
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
			ReselectAfterDelete();
		}
		else
		{
			Notify.Post("Can't delete beatmap, it doesn't exist.");
		}
	}
	/// <summary>
	/// After a delete the list shifted, so the same index is now a different beatmap: select it again for real
	/// (SelectSongID skips an index that's already selected, which left the old song's info on screen).
	/// </summary>
	private void ReselectAfterDelete()
	{
		for (int i = 0; i < SettingsOperator.Beatmaps.Count; i++) SettingsOperator.Beatmaps[i].ID = i;
		int target = Math.Clamp(SettingsOperator.SessionConfig.SongID, 0, Math.Max(0, SettingsOperator.Beatmaps.Count - 1));
		SettingsOperator.SessionConfig.SongID = -1;
		if (SettingsOperator.Beatmaps.Count > 0) SettingsOperator.SelectSongID(target);
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
			ReselectAfterDelete();
		}
		else
		{
			Notify.Post("Can't delete beatmap, it doesn't exist.", Type:NotificationIcons.NotificationType.Warning);
		}
	}
}
