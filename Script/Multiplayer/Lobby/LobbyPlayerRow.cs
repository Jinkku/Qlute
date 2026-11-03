using Godot;

/// <summary>
/// One player in the lobby's player list: avatar, name, HOST / YOU badges, live score line,
/// a coloured status pill, and a "Make host" button the host sees on everyone else.
/// Instanced by <see cref="LobbyPlayerList"/> from Panels/Multiplayer/Lobby/LobbyPlayerRow.tscn.
/// </summary>
public partial class LobbyPlayerRow : PanelContainer
{
	[Signal] public delegate void MakeHostPressedEventHandler(long playerId);

	[ExportGroup("Look")]
	/// <summary>Theme type variation used for other players' rows.</summary>
	[Export] public StringName RowVariation { get; set; } = "LobbyRow";
	/// <summary>Theme type variation used for your own row.</summary>
	[Export] public StringName SelfRowVariation { get; set; } = "LobbyRowSelf";
	[Export] public Color ReadyColor { get; set; } = new("#5be37d");
	[Export] public Color NotReadyColor { get; set; } = new("#9aa3b5");
	[Export] public Color NoMapColor { get; set; } = new("#ffa94d");
	[Export] public Color PlayingColor { get; set; } = new("#7fc8ff");
	/// <summary>How strongly the status colour tints the pill background (0..1).</summary>
	[Export(PropertyHint.Range, "0,1,0.01")] public float PillTint { get; set; } = 0.18f;

	[ExportGroup("Nodes")]
	[Export] public Label NameLabel { get; set; }
	[Export] public Control HostBadge { get; set; }
	[Export] public Control YouBadge { get; set; }
	[Export] public Label DetailLabel { get; set; }
	[Export] public Label StatusPill { get; set; }
	[Export] public Button MakeHostButton { get; set; }

	public long PlayerId { get; private set; }

	private StyleBoxFlat _pillBox;

	public override void _Ready()
	{
		// Own copy of the pill style so each row can tint it with its status colour.
		if (StatusPill.GetThemeStylebox("normal")?.Duplicate() is StyleBoxFlat box)
		{
			_pillBox = box;
			StatusPill.AddThemeStyleboxOverride("normal", _pillBox);
		}
	}

	public void Setup(LanPlayer p, LanRoomState s, bool isLocal, bool canMakeHost)
	{
		PlayerId = p.Id;
		bool isHost = p.Id == s.HostId;
		bool playing = s.Phase == LanPhase.Playing && p.InMatch;

		ThemeTypeVariation = isLocal ? SelfRowVariation : RowVariation;
		NameLabel.Text = p.Name;
		HostBadge.Visible = isHost;
		YouBadge.Visible = isLocal;
		MakeHostButton.Visible = canMakeHost && !isHost && !isLocal;

		DetailLabel.Visible = playing;
		if (playing) DetailLabel.Text = $"{p.Score:N0}   {p.Accuracy:P2}   {p.Combo}x";

		var (status, color) = Status(p, s);
		StatusPill.Text = status;
		StatusPill.AddThemeColorOverride("font_color", color);
		if (_pillBox != null) _pillBox.BgColor = new Color(color, PillTint);
	}

	private (string, Color) Status(LanPlayer p, LanRoomState s)
	{
		if (s.Phase == LanPhase.Playing && p.InMatch)
			return (p.Finished ? (p.Quit ? "QUIT" : "DONE") : "PLAYING", PlayingColor);
		if (!p.HasMap && s.Beatmap != null) return ("NO MAP", NoMapColor);
		if (p.Ready) return ("READY", ReadyColor);
		return (s.Phase == LanPhase.Playing ? "WAITING" : "NOT READY", NotReadyColor);
	}

	private void OnMakeHostPressed() => EmitSignal(SignalName.MakeHostPressed, PlayerId);
}
