using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// The Filters panel in song select: sort, status, map mode, level range and collections (SongFilter / Collections).
/// Raises <see cref="Changed"/> whenever the list should be filtered again.
/// </summary>
public partial class SongFilterPanel : PanelContainer
{
	public event Action Changed;
	/// <summary>The difficulty selected in song select (for "Add selected" / "Remove selected").</summary>
	public Func<BeatmapLegend> Selected;

	private Label _count;
	private OptionButton _sort, _collection;
	private Button _direction;
	private readonly List<Button> _status = new(), _mode = new();
	private SpinBox _min, _max;
	private LineEdit _name;
	private Button _addSelected, _removeSelected, _rename, _delete;
	private Label _collectionNote;
	private int _seenCollections = -1;
	private bool _quiet; // while filling the controls from the saved state

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Stop;
		AddThemeStyleboxOverride("panel", PlayerOverlays.Style(new Color(0.09f, 0.075f, 0.105f, 0.97f), 15, 20, 16, new Color(1, 1, 1, 0.08f)));
		var v = ModUi.Column(10);
		AddChild(v);

		var head = ModUi.Row(10);
		var title = PlayerOverlays.Text("Filters", 22, bold: true);
		title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		head.AddChild(title);
		_count = PlayerOverlays.Text("", 15, PlayerOverlays.Dim);
		_count.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		head.AddChild(_count);
		var reset = ModUi.Button("Reset");
		reset.TooltipText = "Clear every filter (keeps the sort)";
		reset.Pressed += () => { SongFilter.Reset(); FillFromState(); Changed?.Invoke(); };
		head.AddChild(reset);
		v.AddChild(head);

		// Sort
		var sortRow = ModUi.Row(8);
		sortRow.AddChild(Caption("Sort"));
		_sort = ModUi.Options(SongFilter.Sorts.Select(s => s.name).ToArray());
		_sort.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		_sort.ItemSelected += i => { if (_quiet) return; SongFilter.Current.Sort = SongFilter.Sorts[(int)i].mode; Commit(); };
		sortRow.AddChild(_sort);
		_direction = ModUi.Button("Ascending");
		_direction.CustomMinimumSize = new Vector2(130, 0);
		_direction.Pressed += () => { SongFilter.Current.Descending = !SongFilter.Current.Descending; Commit(); };
		sortRow.AddChild(_direction);
		v.AddChild(sortRow);

		v.AddChild(SegmentRow("Status", SongFilter.Statuses, _status, value => SongFilter.Current.Status = value));
		v.AddChild(SegmentRow("Map mode", SongFilter.Modes, _mode, value => SongFilter.Current.Mode = value));

		// Level range
		var levelRow = ModUi.Row(8);
		levelRow.AddChild(Caption("Level"));
		_min = Spin(); _max = Spin();
		_min.ValueChanged += x => { if (_quiet) return; SongFilter.Current.LevelMin = (int)x; if (_max.Value < x) _max.Value = x; Commit(); };
		_max.ValueChanged += x => { if (_quiet) return; SongFilter.Current.LevelMax = (int)x; if (_min.Value > x) _min.Value = x; Commit(); };
		levelRow.AddChild(_min);
		levelRow.AddChild(PlayerOverlays.Text("to", 16, PlayerOverlays.Dim));
		levelRow.AddChild(_max);
		v.AddChild(levelRow);

		v.AddChild(new HSeparator());

		// Collections
		var colRow = ModUi.Row(8);
		colRow.AddChild(Caption("Collection"));
		_collection = ModUi.Options("All beatmaps");
		_collection.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		_collection.ItemSelected += i =>
		{
			if (_quiet) return;
			SongFilter.Current.Collection = i == 0 ? null : _collection.GetItemMetadata((int)i).AsString();
			Commit();
		};
		colRow.AddChild(_collection);
		v.AddChild(colRow);

		var actions = ModUi.Row(8);
		_addSelected = ModUi.Button("Add selected");
		_addSelected.TooltipText = "Add the selected difficulty to this collection";
		_addSelected.Pressed += () => ChangeSelected(add: true);
		_removeSelected = ModUi.Button("Remove selected");
		_removeSelected.TooltipText = "Take the selected difficulty out of this collection";
		_removeSelected.Pressed += () => ChangeSelected(add: false);
		actions.AddChild(_addSelected);
		actions.AddChild(_removeSelected);
		v.AddChild(actions);

		var nameRow = ModUi.Row(8);
		_name = ModUi.Input("Collection name", 60);
		_name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		_name.TextSubmitted += _ => CreateCollection();
		nameRow.AddChild(_name);
		var create = ModUi.Button("New", ModUi.Purple);
		create.TooltipText = "Make a new collection with this name";
		create.Pressed += CreateCollection;
		nameRow.AddChild(create);
		_rename = ModUi.Button("Rename");
		_rename.TooltipText = "Rename the collection picked above to this name";
		_rename.Pressed += RenameCollection;
		nameRow.AddChild(_rename);
		_delete = ModUi.ConfirmButton("Delete", "Really delete?", ModUi.Red, DeleteCollection);
		nameRow.AddChild(_delete);
		v.AddChild(nameRow);

		_collectionNote = PlayerOverlays.Text("Right-click a beatmap to add it to a collection.", 14, PlayerOverlays.Dim); // no wrap: it would size the panel at 0 width
		v.AddChild(_collectionNote);

		FillFromState();
	}

	private static Label Caption(string text)
	{
		var l = PlayerOverlays.Text(text, 16, PlayerOverlays.Dim);
		l.CustomMinimumSize = new Vector2(110, 0);
		l.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		return l;
	}

	private static SpinBox Spin()
	{
		var s = new SpinBox { MinValue = 0, MaxValue = 100, Step = 1, CustomMinimumSize = new Vector2(110, 0), SelectAllOnFocus = true };
		s.GetLineEdit().AddThemeFontSizeOverride("font_size", 16);
		return s;
	}

	private Control SegmentRow(string caption, (int value, string name)[] options, List<Button> buttons, Action<int> set)
	{
		var row = ModUi.Row(8);
		row.AddChild(Caption(caption));
		row.AddChild(PlayerOverlays.SegmentBar(out var seg));
		foreach (var (value, name) in options)
		{
			var b = PlayerOverlays.SegmentButton(name);
			b.SetMeta("value", value);
			b.Pressed += () => { set(value); Commit(); };
			seg.AddChild(b);
			buttons.Add(b);
		}
		return row;
	}

	private void Commit()
	{
		SongFilter.Save();
		PaintState();
		Changed?.Invoke();
	}

	/// <summary>Puts the saved filters into the controls (on open, after Reset).</summary>
	public void FillFromState()
	{
		if (_sort == null) return;
		_quiet = true;
		var s = SongFilter.Current;
		_sort.Select(Math.Max(0, Array.FindIndex(SongFilter.Sorts, x => x.mode == s.Sort)));
		_min.Value = s.LevelMin;
		_max.Value = s.LevelMax;
		RebuildCollections();
		_quiet = false;
		PaintState();
	}

	private void PaintState()
	{
		var s = SongFilter.Current;
		_direction.Text = s.Descending ? "Descending" : "Ascending";
		foreach (var b in _status) PlayerOverlays.PaintSegment(b, (int)b.GetMeta("value") == s.Status);
		foreach (var b in _mode) PlayerOverlays.PaintSegment(b, (int)b.GetMeta("value") == s.Mode);
		bool picked = !string.IsNullOrEmpty(s.Collection);
		_addSelected.Disabled = _removeSelected.Disabled = _rename.Disabled = _delete.Disabled = !picked;
	}

	private void RebuildCollections()
	{
		_seenCollections = Collections.Version;
		bool wasQuiet = _quiet;
		_quiet = true;
		_collection.Clear();
		_collection.AddItem("All beatmaps");
		int select = 0;
		foreach (var name in Collections.Names)
		{
			_collection.AddItem($"{name}  ({Collections.CountInLibrary(name)})");
			_collection.SetItemMetadata(_collection.ItemCount - 1, name);
			if (string.Equals(name, SongFilter.Current.Collection, StringComparison.OrdinalIgnoreCase)) select = _collection.ItemCount - 1;
		}
		_collection.Select(select);
		_quiet = wasQuiet;
		_collectionNote.Text = Collections.Names.Count == 0
			? "No collections yet: type a name and press New."
			: "Right-click a beatmap to add it to a collection.";
	}

	/// <summary>Called by song select each frame it's open: result count + collection list changes.</summary>
	public void Refresh(int shown, int total)
	{
		if (_count == null) return;
		var text = shown == total ? $"{total:N0} beatmaps" : $"{shown:N0} of {total:N0}";
		if (_count.Text != text) _count.Text = text;
		if (_seenCollections != Collections.Version) { RebuildCollections(); PaintState(); }
	}

	private void CreateCollection()
	{
		var error = Collections.Create(_name.Text);
		if (error != null) { Notify.Post("Couldn't make the collection", Description: error, Type: NotificationIcons.NotificationType.Warning); return; }
		SongFilter.Current.Collection = _name.Text.Trim(); // show the new (empty) collection right away
		_name.Text = "";
		RebuildCollections();
		Commit();
	}

	private void RenameCollection()
	{
		var current = SongFilter.Current.Collection;
		if (string.IsNullOrEmpty(current)) return;
		var error = Collections.Rename(current, _name.Text);
		if (error != null) { Notify.Post("Couldn't rename the collection", Description: error, Type: NotificationIcons.NotificationType.Warning); return; }
		SongFilter.Current.Collection = _name.Text.Trim();
		_name.Text = "";
		RebuildCollections();
		Commit();
	}

	private void DeleteCollection()
	{
		var current = SongFilter.Current.Collection;
		if (string.IsNullOrEmpty(current)) return;
		Collections.Delete(current);
		SongFilter.Current.Collection = null;
		Notify.Post("Collection deleted", Description: current);
		RebuildCollections();
		Commit();
	}

	private void ChangeSelected(bool add)
	{
		var current = SongFilter.Current.Collection;
		var map = Selected?.Invoke();
		if (string.IsNullOrEmpty(current) || map == null) return;
		int n = add ? Collections.Add(current, new[] { map }) : Collections.Remove(current, new[] { map });
		Notify.Post(add ? (n > 0 ? $"Added to {current}" : $"Already in {current}") : (n > 0 ? $"Removed from {current}" : $"Not in {current}"),
			Description: $"{map.Artist} - {map.Title} [{map.Version}]");
		RebuildCollections();
		Changed?.Invoke();
	}
}
