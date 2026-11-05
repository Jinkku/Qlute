using Godot;
using System.Linq;
using System.Text;

/// <summary>
/// Centre column of the lobby: the "Add beatmap" button, the round-robin queue (everyone's picks, in play order),
/// the local player's own difficulty + mods for what's up next, and a missing-map row with download + progress.
/// Picking happens in song select (see <see cref="LanNetwork.BeginPick"/>); this panel only raises signals.
/// </summary>
public partial class LobbyBeatmapPanel : PanelContainer
{
	[Signal] public delegate void AddBeatmapRequestedEventHandler();
	[Signal] public delegate void EditItemRequestedEventHandler(int itemId);
	[Signal] public delegate void RemoveItemRequestedEventHandler(int itemId);
	[Signal] public delegate void EditDifficultyRequestedEventHandler();
	/// <summary>The local player asked for the up-next beatmap to be sent to them.</summary>
	[Signal] public delegate void DownloadRequestedEventHandler();

	[ExportGroup("Text")]
	[Export] public string EmptyQueueText { get; set; } = "The queue is empty. Add a beatmap!";
	[Export] public string QueueHeadingFormat { get; set; } = "Queue ({0})";

	[ExportGroup("Queue")]
	[Export] public Button AddButton { get; set; }
	[Export] public Label QueueHeading { get; set; }
	[Export] public Container QueueList { get; set; }
	[Export] public PackedScene CardScene { get; set; }

	[ExportGroup("Your difficulty")]
	[Export] public Label OwnHeading { get; set; }
	[Export] public LobbyBeatmapCard OwnCard { get; set; }

	[ExportGroup("Missing beatmap")]
	[Export] public Control MissingRow { get; set; }
	[Export] public Button DownloadButton { get; set; }
	[Export] public ProgressBar TransferBar { get; set; }

	private int _localIndex = -1;
	private string _localIndexChecksum;
	private int _localIndexLibraryCount = -1;
	private string _queueSignature;

	public override void _Ready()
	{
		CardScene ??= GD.Load<PackedScene>("res://Panels/Multiplayer/Overlay/LobbyBeatmapCard.tscn");
		if (OwnCard != null) OwnCard.EditPressed += _ => EmitSignal(SignalName.EditDifficultyRequested);
	}

	public void Refresh(LanRoomState s, LanNetwork net)
	{
		bool lobby = s.Phase == LanPhase.Lobby;
		var me = net.LocalPlayer;
		UpdateLocalIndex(net);

		int mine = net.QueueCountFor(net.LocalId);
		if (AddButton != null)
		{
			AddButton.Disabled = mine >= LanNetwork.MaxQueuePerPlayer;
			AddButton.TooltipText = AddButton.Disabled
				? $"You already have {LanNetwork.MaxQueuePerPlayer} beatmaps in the queue."
				: "Pick a beatmap in song select. Everyone's picks take turns.";
		}
		if (QueueHeading != null) QueueHeading.Text = string.Format(QueueHeadingFormat, s.Queue.Count);
		RebuildQueue(s, net);

		// Your difficulty + mods for what's up next (or being played).
		bool hasMap = s.Beatmap != null;
		if (OwnHeading != null) OwnHeading.Visible = hasMap;
		if (OwnCard != null)
		{
			OwnCard.Visible = hasMap;
			if (hasMap)
			{
				var mods = LanNetwork.ModAlias(me?.Mods);
				var room = LanNetwork.GlobalModLabel(s.CurrentMods, s.CurrentSpeed);
				var label = string.Join(" · ", new[] { room.Length > 0 ? $"room +{room}" : "", mods.Length > 0 ? "+" + mods : "" }.Where(x => x.Length > 0));
				bool canEdit = lobby && (_localIndex >= 0 || net.HasLocalSet());
				OwnCard.Setup(net.LocalChart, 0, label.Length == 0 ? "No mods" : label, canEdit, false, false,
					missing: _localIndex < 0);
			}
		}

		bool missing = hasMap && _localIndex < 0;
		MissingRow.Visible = missing;
		DownloadButton.Visible = missing && net.TransferProgress < 0;
	}

	/// <summary>Called every frame with LanNetwork.TransferProgress (-1 when idle).</summary>
	public void UpdateTransfer(float progress)
	{
		bool receiving = progress >= 0;
		TransferBar.Visible = receiving;
		if (receiving) TransferBar.Value = progress * 100;
		else if (MissingRow.Visible) DownloadButton.Visible = true;
	}

	/// <summary>The beatmap library changed (e.g. a map arrived): re-check which picks we have.</summary>
	public void RebuildList()
	{
		_localIndexLibraryCount = -1;
		_queueSignature = null;
	}

	// ---- internals ----

	/// <summary>Finding a map in the library hashes files, so only redo it when something changed.</summary>
	private void UpdateLocalIndex(LanNetwork net)
	{
		var chart = net.LocalChart;
		if (chart?.Checksum == _localIndexChecksum && _localIndexLibraryCount == SettingsOperator.Beatmaps.Count) return;
		_localIndexChecksum = chart?.Checksum;
		_localIndexLibraryCount = SettingsOperator.Beatmaps.Count;
		_localIndex = net.FindLocalBeatmap(chart);
	}

	private void RebuildQueue(LanRoomState s, LanNetwork net)
	{
		if (QueueList == null || CardScene == null) return;
		bool playing = s.Phase == LanPhase.Playing && s.Beatmap != null;

		// State arrives several times a second during a match; only rebuild the cards when the queue really changed.
		var sig = new StringBuilder($"{s.Phase}|{net.LocalId}|{net.IsHost}|{SettingsOperator.Beatmaps.Count}|{s.CurrentItemId}|{s.Beatmap?.Checksum}|{string.Join(",", s.CurrentMods)}{s.CurrentSpeed}");
		foreach (var q in s.Queue) sig.Append($"|{q.Id}:{q.OwnerId}:{q.Beatmap?.Checksum}:{string.Join(",", q.Mods)}{q.Speed}");
		if (sig.ToString() == _queueSignature) return;
		_queueSignature = sig.ToString();

		foreach (var child in QueueList.GetChildren()) child.QueueFree();

		if (playing)
			AddCard(s.Beatmap, 0, WithMods($"now playing · {s.CurrentOwnerName}", s.CurrentMods, s.CurrentSpeed), false, false, true,
				net.FindLocalBeatmap(s.Beatmap) < 0);

		for (int i = 0; i < s.Queue.Count; i++)
		{
			var q = s.Queue[i];
			bool own = q.OwnerId == net.LocalId;
			bool upNext = !playing && i == 0;
			var who = WithMods(upNext ? $"up next · {q.OwnerName}" : q.OwnerName, q.Mods, q.Speed);
			AddCard(q.Beatmap, q.Id, who, own, own || net.IsHost, upNext, net.FindLocalBeatmap(q.Beatmap) < 0);
		}

		if (!playing && s.Queue.Count == 0)
			AddCard(null, 0, "", false, false, false, false);
	}

	private static string WithMods(string who, System.Collections.Generic.List<string> mods, float speed)
	{
		var label = LanNetwork.GlobalModLabel(mods, speed);
		return label.Length == 0 ? who : $"{who} · +{label}";
	}

	private void AddCard(LanBeatmapRef map, int itemId, string user, bool canEdit, bool canDelete, bool upNext, bool missing)
	{
		var card = CardScene.Instantiate<LobbyBeatmapCard>();
		QueueList.AddChild(card);
		card.Setup(map, itemId, user, canEdit, canDelete, upNext, EmptyQueueText, missing);
		if (itemId != 0)
		{
			card.EditPressed += id => EmitSignal(SignalName.EditItemRequested, id);
			card.DeletePressed += id => EmitSignal(SignalName.RemoveItemRequested, id);
		}
	}

	// ---- Signals (connected in the .tscn) ----

	private void Addbeatmap() => EmitSignal(SignalName.AddBeatmapRequested);

	private void OnDownloadPressed()
	{
		DownloadButton.Visible = false;
		EmitSignal(SignalName.DownloadRequested);
	}
}
