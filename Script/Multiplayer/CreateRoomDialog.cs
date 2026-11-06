using Godot;

/// <summary>
/// Create Room dialog (lounge): room name, who can see it (LAN / online) and the starting beatmap.
/// Choices live in <see cref="RoomDraft"/> so picking a beatmap in song select doesn't lose them.
/// </summary>
public partial class CreateRoomDialog : Control
{
	public const int VisibilityLan = 0, VisibilityOnline = 1;

	[Signal] public delegate void RoomCreatedEventHandler();
	[Signal] public delegate void ClosedEventHandler();

	[ExportGroup("Nodes")]
	[Export] public LineEdit NameEdit { get; set; }
	[Export] public OptionButton Visibility { get; set; }
	[Export] public Label VisibilityHint { get; set; }
	[Export] public Label BeatmapName { get; set; }
	[Export] public Button ChangeBeatmap { get; set; }
	[Export] public Button ClearBeatmap { get; set; }
	[Export] public Button CancelButton { get; set; }
	[Export] public Button CreateButton { get; set; }

	private static LanNetwork Net => LanNetwork.Instance;

	public override void _Ready()
	{
		foreach (var b in new[] { ChangeBeatmap, ClearBeatmap, CancelButton, CreateButton }) LanUi.Style(b);

		// First open: default to the song you were listening to.
		if (!RoomDraft.Reopen && RoomDraft.Name == null)
		{
			RoomDraft.Name = $"{LanNetwork.LocalName()}'s room";
			RoomDraft.Online = OnlineApi.Available;
			var current = SettingsOperator.SessionConfig.SongID;
			RoomDraft.BeatmapIndex = current >= 0 && current < SettingsOperator.Beatmaps.Count ? current : -1;
		}
		RoomDraft.Reopen = false;

		NameEdit.MaxLength = LanNetwork.MaxRoomNameLength;
		NameEdit.Text = RoomDraft.Name;
		NameEdit.TextChanged += t => RoomDraft.Name = t;
		NameEdit.TextSubmitted += _ => Create();

		Visibility.SetItemDisabled(VisibilityOnline, !OnlineApi.Available);
		Visibility.Selected = RoomDraft.Online && OnlineApi.Available ? VisibilityOnline : VisibilityLan;
		Visibility.ItemSelected += i => { RoomDraft.Online = i == VisibilityOnline; RefreshHint(); };

		ChangeBeatmap.Pressed += () => { RoomDraft.Name = NameEdit.Text; Net.BeginStartingBeatmapPick(); };
		ClearBeatmap.Pressed += () => { RoomDraft.BeatmapIndex = -1; RefreshBeatmap(); };
		CancelButton.Pressed += Close;
		CreateButton.Pressed += Create;

		RefreshHint();
		RefreshBeatmap();
		NameEdit.GrabFocus();
		NameEdit.CaretColumn = NameEdit.Text.Length;
	}

	public override void _UnhandledInput(InputEvent e)
	{
		if (e.IsActionPressed("ui_cancel") || e.IsActionPressed("pausemenu"))
		{
			GetViewport().SetInputAsHandled();
			Close();
		}
	}

	private void RefreshHint()
	{
		VisibilityHint.Text = Visibility.Selected == VisibilityOnline
			? "Listed on the Qlute server. Anyone logged in can join from anywhere. Beatmaps people are missing are shared through the server (up to 64 MB)."
			: OnlineApi.Available
				? "Only people on the same network (Wi-Fi / LAN) can see and join it. Works without internet."
				: "Only people on the same network (Wi-Fi / LAN) can see and join it. Log in to make online rooms.";
	}

	private void RefreshBeatmap()
	{
		int i = RoomDraft.BeatmapIndex;
		bool has = i >= 0 && i < SettingsOperator.Beatmaps.Count;
		if (has)
		{
			var b = SettingsOperator.Beatmaps[i];
			var mods = LanNetwork.GlobalModLabel(RoomDraft.Mods, RoomDraft.Speed);
			BeatmapName.Text = $"{b.Artist} - {b.Title} [{b.Version}]{(mods.Length > 0 ? "  " + mods : "")}";
		}
		else BeatmapName.Text = "None (pick one in the room)";
		BeatmapName.TooltipText = BeatmapName.Text;
		ClearBeatmap.Visible = has;
		ChangeBeatmap.Text = has ? "Change..." : "Pick...";
	}

	private void Create()
	{
		RoomDraft.Name = NameEdit.Text;
		bool online = Visibility.Selected == VisibilityOnline;
		if (!Net.HostRoom(NameEdit.Text, online, RoomDraft.BeatmapIndex, RoomDraft.Mods, RoomDraft.Speed)) return;
		RoomDraft.Reset();
		EmitSignal(SignalName.RoomCreated);
		QueueFree();
	}

	private void Close()
	{
		EmitSignal(SignalName.Closed);
		QueueFree();
	}
}
