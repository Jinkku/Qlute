using Godot;

/// <summary>
/// LAN room screen. Glues the lobby components (header, players, beatmap, chat) to <see cref="LanNetwork"/>.
///
/// Layout lives in Panels/Screens/MultiplayerLobby.tscn and the component scenes in Panels/Multiplayer/Lobby/.
/// Components only display state and raise signals; the signals are connected to the On* handlers below
/// in the .tscn (Node > Signals dock), so this script is the one place that talks to the network.
/// </summary>
public partial class MultiplayerLobby : Control
{
	private static LanNetwork Net => LanNetwork.Instance;

	[ExportGroup("Components")]
	[Export] public LobbyHeader Header { get; set; }
	[Export] public LobbyPlayerList Players { get; set; }
	[Export] public LobbyBeatmapPanel Beatmap { get; set; }
	[Export] public LobbyChatPanel Chat { get; set; }

	[ExportGroup("Dialogs")]
	/// <summary>Asks the host to confirm before handing the host controls to someone else.</summary>
	[Export] public ConfirmationDialog TransferHostDialog { get; set; }

	private int _seenState = -1, _seenChat = -1, _seenBeatmapCount = -1;
	private long _pendingHostTarget;
	private bool _leaving;

	public override void _Ready()
	{
		if (Net?.State == null)
		{
			_leaving = true;
			Callable.From(() => GetNode<SceneTransition>("/root/Transition").Switch(LanNetwork.LoungeScene)).CallDeferred();
			return;
		}
		GetNode<SettingsOperator>("/root/SettingsOperator").toppaneltoggle(true);
		Cursor.CursorVisible = true;
		Chat.SetPlayerName(LanNetwork.LocalName());
		Net.OnLobbyOpened(); // back from song select / results: sync mods + status, preview the next map
	}

	public override void _Process(double delta)
	{
		if (_leaving) return;
		var s = Net.State;
		if (s == null)
		{
			// Host closed the room (LanNetwork already notified the player).
			LeaveToLounge();
			return;
		}

		if (_seenBeatmapCount != SettingsOperator.Beatmaps.Count)
		{
			_seenBeatmapCount = SettingsOperator.Beatmaps.Count;
			Beatmap.RebuildList();
			_seenState = -1; // re-check the room's map against the new library
		}
		if (_seenState != Net.StateVersion)
		{
			_seenState = Net.StateVersion;
			Header.Refresh(s, Net);
			Players.Refresh(s, Net.LocalId, Net.IsHost);
			Beatmap.Refresh(s, Net);
		}
		if (_seenChat != Net.ChatVersion)
		{
			_seenChat = Net.ChatVersion;
			Chat.SetLines(Net.ChatLog);
		}

		Header.UpdateCountdown(s, Net.TimeLeftNow);
		LanUi.KeepNormalSpeed();
		Beatmap.UpdateTransfer(Net.TransferProgress);
	}

	private void LeaveToLounge()
	{
		_leaving = true;
		GetNode<SceneTransition>("/root/Transition").Switch(LanNetwork.LoungeScene);
	}

	// =====================================================================
	// Component signal handlers (connected in MultiplayerLobby.tscn)
	// =====================================================================

	private void OnReadyToggled(bool ready) => Net.SetReady(ready);

	private void OnLeavePressed()
	{
		Net.Leave();
		LeaveToLounge();
	}

	private void OnRenameSubmitted(string newName) => Net.HostRename(newName);
	private void OnTimerAdjusted(int deltaSeconds) => Net.HostAdjustTimer(deltaSeconds);
	private void OnStartNowPressed() => Net.HostStartNow();
	private void OnSpectatePressed() => Net.BeginSpectate();
	// Queue picks open song select, which comes back here when a beatmap is picked (or Back is pressed).
	private void OnAddBeatmapRequested() => Net.BeginPick(LanNetwork.PickMode.AddToQueue);
	private void OnEditItemRequested(int itemId) => Net.BeginPick(LanNetwork.PickMode.ReplaceItem, itemId);
	private void OnRemoveItemRequested(int itemId) => Net.RemoveQueueItem(itemId);
	private void OnEditDifficultyRequested() => Net.BeginPick(LanNetwork.PickMode.OwnDifficulty);
	private void OnDownloadRequested() => Net.RequestBeatmapFromHost();
	private void OnChatSubmitted(string text) => Net.SendChat(text);

	private void OnTransferHostRequested(long playerId)
	{
		var target = Net.State?.Players.Find(p => p.Id == playerId);
		if (target == null) return;
		_pendingHostTarget = playerId;
		if (TransferHostDialog == null) { OnTransferHostConfirmed(); return; }
		TransferHostDialog.DialogText = Net.IsServer
			? $"Make {target.Name} the host?\n\nThey'll run the timer and can remove anyone's queue picks. If you leave, the room moves to their game."
			: $"Make {target.Name} the host?\n\nYou won't be able to take it back unless they hand it to you.";
		TransferHostDialog.PopupCentered();
	}

	private void OnTransferHostConfirmed()
	{
		if (_pendingHostTarget != 0) Net.HostTransfer(_pendingHostTarget);
		_pendingHostTarget = 0;
	}
}
