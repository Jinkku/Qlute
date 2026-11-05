using Godot;

/// <summary>
/// One beatmap row in the lobby: a queue item (who picked it, edit / remove buttons), or with
/// <see cref="IsOwnDifficulty"/> the "Your difficulty" card (your difficulty + mods, edit only).
/// Instanced by <see cref="LobbyBeatmapPanel"/> from Panels/Multiplayer/Overlay/LobbyBeatmapCard.tscn.
/// </summary>
public partial class LobbyBeatmapCard : PanelContainer
{
	[Signal] public delegate void EditPressedEventHandler(int itemId);
	[Signal] public delegate void DeletePressedEventHandler(int itemId);

	[Export] public bool IsOwnDifficulty { get; set; }

	// Placeholder text shown in the editor; replaced by Setup() at runtime.
	[Export] private string SongTitle { get; set; } = "test Title";
	[Export] private string SongArtist { get; set; } = "test artist";
	[Export] private string SongDifficulty { get; set; } = "test difficulty";
	[Export] private string SongMapper { get; set; } = "test mapper";
	[Export] private string UserName { get; set; } = "idiot";

	[ExportGroup("Look")]
	[Export] public Color UpNextColor { get; set; } = new(0.42f, 0.65f, 1f, 0.22f);
	[Export] public Color MissingColor { get; set; } = new("#ffa94d");

	public int ItemId { get; private set; }

	private Label TitleNArtist { get; set; }
	private Label Mapper { get; set; }
	private Label User { get; set; }
	private Button EditButton { get; set; }
	private Button DeleteButton { get; set; }
	private StyleBoxFlat _box;
	private Color _idleBg;

	public override void _Ready()
	{
		TitleNArtist = GetNode<Label>("MarginContainer/HBoxContainer/VBoxContainer/Title");
		Mapper = GetNode<Label>("MarginContainer/HBoxContainer/VBoxContainer/Mapper");
		User = GetNode<Label>("MarginContainer/HBoxContainer/User");
		EditButton = GetNode<Button>("MarginContainer/HBoxContainer/Edit");
		DeleteButton = GetNode<Button>("MarginContainer/HBoxContainer/Delete");
		DeleteButton.Pressed += () => EmitSignal(SignalName.DeletePressed, ItemId);
		DeleteButton.Visible = !IsOwnDifficulty;

		// Own copy of the panel style so each card can highlight itself.
		if (GetThemeStylebox("panel")?.Duplicate() is StyleBoxFlat box)
		{
			_box = box;
			_idleBg = box.BgColor;
			AddThemeStyleboxOverride("panel", _box);
		}
		ApplyText();
	}

	/// <summary>Fills the card. <paramref name="map"/> null shows <paramref name="emptyText"/> instead.</summary>
	public void Setup(LanBeatmapRef map, int itemId, string user, bool canEdit, bool canDelete, bool upNext,
		string emptyText = "Nothing up next yet", bool missing = false)
	{
		ItemId = itemId;
		if (map == null)
		{
			SongTitle = emptyText;
			SongArtist = SongDifficulty = SongMapper = null;
		}
		else
		{
			SongTitle = map.Title;
			SongArtist = map.Artist;
			SongDifficulty = map.Version;
			SongMapper = map.Mapper;
		}
		UserName = user ?? "";
		if (!IsInsideTree()) return;

		ApplyText();
		EditButton.Visible = canEdit;
		DeleteButton.Visible = canDelete && !IsOwnDifficulty;
		if (_box != null) _box.BgColor = upNext ? UpNextColor : _idleBg;
		if (missing) Mapper.AddThemeColorOverride("font_color", MissingColor);
		else Mapper.RemoveThemeColorOverride("font_color");
		if (missing) Mapper.Text += "  (you don't have this one)";
	}

	private void ApplyText()
	{
		if (TitleNArtist == null) return;
		bool hasMap = SongArtist != null;
		TitleNArtist.Text = hasMap ? $"{SongArtist} - {SongTitle} [{SongDifficulty}]" : SongTitle;
		Mapper.Visible = hasMap;
		Mapper.Text = $"mapped by {SongMapper}";
		User.Text = UserName;
	}

	// Connected in the .tscn.
	private void EditBeatmap() => EmitSignal(SignalName.EditPressed, ItemId);
}
