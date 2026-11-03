using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Centre column of the lobby: a "hero" card for the room's beatmap (cover art, title, chips),
/// a missing-map row with a download button + progress, and the searchable beatmap library.
/// Only the host's picks are sent; for everyone else the list is browse-only.
/// </summary>
public partial class LobbyBeatmapPanel : PanelContainer
{
	/// <summary>The local player picked a beatmap (index into SettingsOperator.Beatmaps).</summary>
	[Signal] public delegate void BeatmapPickedEventHandler(int libraryIndex);
	/// <summary>The local player asked for the room's beatmap to be sent to them.</summary>
	[Signal] public delegate void DownloadRequestedEventHandler();

	[ExportGroup("Text")]
	[Export] public string HostSearchHint { get; set; } = "Search your beatmaps...";
	[Export] public string GuestSearchHint { get; set; } = "Browse beatmaps (the host picks what to play)";
	[Export] public string HostEmptyText { get; set; } = "Pick a beatmap from the list below.";
	[Export] public string GuestEmptyText { get; set; } = "Waiting for the host to pick a beatmap...";

	[ExportGroup("Hero")]
	[Export] public TextureRect Cover { get; set; }
	[Export] public Label MapTitle { get; set; }
	[Export] public Label MapArtist { get; set; }
	[Export] public Control Chips { get; set; }
	[Export] public Label DifficultyChip { get; set; }
	[Export] public Label LevelChip { get; set; }
	[Export] public Label LengthChip { get; set; }
	[Export] public Label MapperChip { get; set; }

	[ExportGroup("Missing beatmap")]
	[Export] public Control MissingRow { get; set; }
	[Export] public Button DownloadButton { get; set; }
	[Export] public ProgressBar TransferBar { get; set; }

	[ExportGroup("Library")]
	[Export] public LineEdit Search { get; set; }
	[Export] public ItemList MapList { get; set; }

	private readonly List<int> _filtered = new(); // list item -> library index
	private bool _ignoreSelection;
	private bool _canPick;
	private int _localIndex = -1;
	private string _localIndexChecksum;
	private int _localIndexLibraryCount = -1;
	private SettingsOperator _settings;

	public override void _Ready()
	{
		_settings = GetNodeOrNull<SettingsOperator>("/root/SettingsOperator");
		if (_settings != null) _settings.BackgroundChanged += UpdateCover;
	}

	public override void _ExitTree()
	{
		if (_settings != null) _settings.BackgroundChanged -= UpdateCover;
	}

	public void Refresh(LanRoomState s, LanNetwork net)
	{
		_canPick = net.IsHost && s.Phase == LanPhase.Lobby;
		Search.PlaceholderText = net.IsHost ? HostSearchHint : GuestSearchHint;
		UpdateLocalIndex(s, net);

		var b = s.Beatmap;
		Chips.Visible = b != null;
		if (b == null)
		{
			MapTitle.Text = net.IsHost ? HostEmptyText : GuestEmptyText;
			MapArtist.Text = "";
		}
		else
		{
			MapTitle.Text = b.Title;
			MapArtist.Text = b.Artist;
			DifficultyChip.Text = b.Version;
			LevelChip.Text = $"Lv. {b.Levelrating:N0}";
			LengthChip.Text = LanUi.FormatTime(b.LengthMs / 1000f);
			MapperChip.Text = $"mapped by {b.Mapper}";
		}

		bool missing = b != null && _localIndex < 0;
		MissingRow.Visible = missing;
		DownloadButton.Visible = missing && net.TransferProgress < 0;

		UpdateCover();
		HighlightSelected();
	}

	/// <summary>Called every frame with LanNetwork.TransferProgress (-1 when idle).</summary>
	public void UpdateTransfer(float progress)
	{
		bool receiving = progress >= 0;
		TransferBar.Visible = receiving;
		if (receiving) TransferBar.Value = progress * 100;
		else if (MissingRow.Visible) DownloadButton.Visible = true;
	}

	/// <summary>Rebuilds the library list (on open, when the search changes, or when maps are imported).</summary>
	public void RebuildList()
	{
		var query = Search.Text.Trim();
		_filtered.Clear();
		MapList.Clear();
		var maps = SettingsOperator.Beatmaps;
		for (int i = 0; i < maps.Count; i++)
		{
			var m = maps[i];
			var label = $"{m.Artist} - {m.Title} [{m.Version}]";
			if (query.Length > 0 && !(label + " " + m.Mapper).Contains(query, StringComparison.OrdinalIgnoreCase)) continue;
			_filtered.Add(i);
			int item = MapList.AddItem($"{label}   Lv. {m.Levelrating:N0}");
			MapList.SetItemTooltip(item, $"{label}\nMapped by {m.Mapper}");
		}
		_localIndexLibraryCount = -1; // library indices may have moved
		HighlightSelected();
	}

	// ---- internals ----

	/// <summary>Finding the room's map in the library hashes files, so only redo it when something changed.</summary>
	private void UpdateLocalIndex(LanRoomState s, LanNetwork net)
	{
		var checksum = s.Beatmap?.Checksum;
		if (checksum == _localIndexChecksum && _localIndexLibraryCount == SettingsOperator.Beatmaps.Count) return;
		_localIndexChecksum = checksum;
		_localIndexLibraryCount = SettingsOperator.Beatmaps.Count;
		_localIndex = net.FindLocalBeatmap(s.Beatmap);
	}

	private void UpdateCover()
	{
		// The selected song follows the room's pick, so its background is the room map's cover.
		var bg = _localIndex >= 0 && SettingsOperator.SessionConfig.SongID == _localIndex ? SettingsOperator.SessionConfig.Background : null;
		Cover.Texture = bg;
		Cover.Visible = bg != null;
	}

	private void HighlightSelected()
	{
		_ignoreSelection = true;
		MapList.DeselectAll();
		int item = _localIndex < 0 ? -1 : _filtered.IndexOf(_localIndex);
		if (item >= 0)
		{
			MapList.Select(item);
			MapList.EnsureCurrentIsVisible();
		}
		_ignoreSelection = false;
	}

	// ---- Signals (connected in the .tscn) ----

	private void OnSearchChanged(string _) => RebuildList();

	private void OnMapSelected(long item)
	{
		if (_ignoreSelection) return;
		if (_canPick && item >= 0 && item < _filtered.Count)
			EmitSignal(SignalName.BeatmapPicked, _filtered[(int)item]);
		else
			HighlightSelected(); // only the host picks; snap back to the room's beatmap
	}

	private void OnDownloadPressed()
	{
		DownloadButton.Visible = false;
		EmitSignal(SignalName.DownloadRequested);
	}
}
