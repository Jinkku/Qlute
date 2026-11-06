using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

/// <summary>
/// LAN multiplayer (autoload "LanNetwork").
///
/// - Discovery: a hosting game broadcasts a small JSON beacon over UDP every second; lounges listen and list rooms.
/// - Session: ENet. The host is authoritative and owns the room state; it pushes the whole state to everyone
///   when it changes. Clients send requests (ready, chat, scores...) to the host.
/// - Rooms have no player cap beyond ENet's own limit (4095 peers), so effectively anyone can join.
/// - Flow (round robin): everyone adds beatmaps to a shared queue -> the server orders it so players take turns ->
///   the head of the queue is "up next" and the "time till play" countdown runs -> drops to 5s once everyone is ready ->
///   ready players load in (each on their own difficulty of that set, with their own allowed mods) -> live scores stream
///   to the host -> each player sees the results screen -> back to lobby, the played item leaves the queue.
/// - "Server" vs "host": the player who created the room is the server (peer 1) for its whole life. The *host*
///   is whoever holds the room controls (<see cref="LanRoomState.HostId"/>) and can be handed to anyone;
///   their requests go through the server like everything else.
/// </summary>
public partial class LanNetwork : Node
{
	public const string BeaconTag = "qlute-lan";
	public const int Protocol = 3; // 3: queue items carry a game mode, spectator charts carry mode + positions
	public const int MaxRoomNameLength = 40;
	public const int DiscoveryPort = 8911;
	public const int DefaultGamePort = 8912;
	public const int MaxPeers = 4095; // ENet's hard limit; rooms are otherwise unlimited.
	public const double AllReadyCountdown = 5;
	public const int MinLobbyTimer = 15, MaxLobbyTimer = 600;
	public const string LobbyScene = "res://Panels/Screens/MultiplayerLobby.tscn";
	public const string LoungeScene = "res://Panels/Screens/MultiplayerList.tscn";
	public const string SongSelectScene = "res://Panels/Screens/song_select.tscn";
	public const string ResultsScene = "res://Panels/Screens/ResultScreenv2.tscn";
	public const string SpectateScene = "res://Panels/Screens/MultiplayerSpectate.tscn";
	/// <summary>How often a watched player sends their live play (song time, key presses, judgements).</summary>
	public const double SpectateTickSeconds = 0.05;
	private const int MaxSpectatePacket = 64 * 1024;
	private const int MaxChartNotes = 100_000;
	private const int MaxChartBytes = 8 * 1024 * 1024;
	public const int MaxQueuePerPlayer = 5;
	public const int MaxQueueTotal = 50;

	/// <summary>Mods players may pick in a LAN match. Anything that changes song speed or plays for you
	/// (auto, DT, HT...) would make scores unfair, so it's left out.</summary>
	public static readonly HashSet<string> AllowedMods = new() { "no-fail", "random", "slice", "black-out" };
	/// <summary>Mods the person adding a beatmap can put on the queue item. They apply to everyone, so the
	/// match stays fair, and can't be picked per player (your own difficulty / mods).</summary>
	public static readonly HashSet<string> GlobalMods = new() { "dt", "ht" };
	public const float MinDtSpeed = 1.1f, MaxDtSpeed = 2f, MinHtSpeed = 0.1f, MaxHtSpeed = 0.9f;

	private static readonly Dictionary<string, string> ModAliases = new()
	{
		{ "auto", "AT" }, { "no-fail", "NF" }, { "dt", "DT" }, { "ht", "HT" },
		{ "random", "RND" }, { "slice", "SL" }, { "black-out", "BT" }, { "am", "AM" },
	};
	private const int ChunkSize = 60 * 1024;
	private const long MaxShareBytes = 200L * 1024 * 1024;
	/// <summary>Online, shared beatmaps go through the server, so keep them smaller.</summary>
	private const long MaxOnlineShareBytes = 64L * 1024 * 1024;
	private const int MaxChatLines = 200;

	public static LanNetwork Instance { get; private set; }
	/// <summary>True while the local player is playing a LAN match.</summary>
	public static bool InMatch => Instance != null && Instance.LocalInMatch;

	/// <summary>Song select is open to pick something for the LAN room (see <see cref="BeginPick"/>).</summary>
	public enum PickMode { None, AddToQueue, ReplaceItem, OwnDifficulty, StartingBeatmap }
	public static PickMode Picking { get; private set; } = PickMode.None;
	/// <summary>Set when the results screen was opened after a LAN match, so it returns to the room.</summary>
	public static bool ShowingMatchResults { get; set; }
	private int _pickItemId;

	// ---- Session state (read by the UI; mutate only through the methods below) ----
	public LanRoomState State { get; private set; }
	/// <summary>True on the machine running the room (peer 1). Leaving closes the room for everyone.</summary>
	public bool IsServer { get; private set; }
	/// <summary>True when the local player holds the host controls.</summary>
	public bool IsHost => State != null && LocalId != 0 && State.HostId == LocalId;
	public bool LocalInMatch { get; private set; }
	public int StateVersion { get; private set; }
	public int GamePort { get; private set; }
	public long LocalId => Multiplayer.MultiplayerPeer is ENetMultiplayerPeer or RelayMultiplayerPeer ? Multiplayer.GetUniqueId() : 0;
	/// <summary>True when the current room goes through the online server instead of the LAN.</summary>
	public bool IsOnline { get; private set; }
	/// <summary>"online" / "LAN", for messages.</summary>
	public string RoomKind => IsOnline ? "online" : "LAN";
	private RelayMultiplayerPeer Relay => Multiplayer.MultiplayerPeer as RelayMultiplayerPeer;
	public LanPlayer LocalPlayer => State?.Players.FirstOrDefault(p => p.Id == LocalId);
	public string ReturnScene => State != null ? LobbyScene : LoungeScene;
	/// <summary>Countdown smoothed locally between host updates.</summary>
	public double TimeLeftNow { get; private set; }

	public readonly List<string> ChatLog = new();
	public int ChatVersion { get; private set; }

	/// <summary>-1 when idle, otherwise 0..1 progress of a beatmap being received from the host.</summary>
	public float TransferProgress { get; private set; } = -1;

	// ---- Spectating (live: charts + inputs, drawn locally by LanPlayfieldView) ----
	/// <summary>Live play of each player in the match, filled while we're spectating.</summary>
	public readonly Dictionary<long, LanSpectateFeed> Feeds = new();
	private readonly LanSpectateFeed.Writer _specOut = new();
	private double _specTimer;
	/// <summary>Server: everyone's chart for the current match, so late spectators can still get it.</summary>
	private readonly Dictionary<long, byte[]> _specCharts = new(); // encoded LanSpecChart per player

	// ---- Discovery ----
	public readonly Dictionary<string, LanRoomInfo> Rooms = new();
	public int RoomsVersion { get; private set; }

	private UdpClient _discoveryRx;
	private UdpClient _beaconTx;
	private List<IPAddress> _broadcastAddrs;
	private double _broadcastAddrsAge = double.MaxValue;
	private double _beaconTimer;

	private bool _joining;
	private bool _dirty;
	private double _syncTimer;
	private double _scoreTimer;
	private int _lastBeatmapCount = -1;
	private readonly Dictionary<string, string> _checksumCache = new();

	private class OutgoingTransfer { public long Peer; public string FileName; public byte[] Data; public int Next; public int Total; }
	private readonly List<OutgoingTransfer> _outgoing = new();
	private MemoryStream _incoming;
	private long _incomingFrom;

	public override void _Ready()
	{
		Instance = this;
		ProcessMode = ProcessModeEnum.Always; // keep networking alive while the tree is paused
		Multiplayer.PeerConnected += OnPeerConnected;
		Multiplayer.PeerDisconnected += OnPeerDisconnected;
		Multiplayer.ConnectedToServer += OnConnectedToServer;
		Multiplayer.ConnectionFailed += OnConnectionFailed;
		Multiplayer.ServerDisconnected += OnServerDisconnected;
		LanSelfTest.Attach(this);
	}

	public override void _ExitTree()
	{
		StopDiscovery();
		StopOnlineBrowse();
		_beaconTx?.Close();
		Leave(silent: true);
	}

	// =====================================================================
	// Public API used by the lounge / lobby / gameplay
	// =====================================================================

	/// <summary>Same name as the LAN chat: the account name, or the offline nickname when not logged in.</summary>
	public static string LocalName() => LanChat.LocalName();

	public static string GameVersion() => ProjectSettings.GetSetting("application/config/version").AsString();

	/// <summary>Create a room and become its host.</summary>
	public bool Host(string roomName)
	{
		Leave(silent: true);
		var peer = new ENetMultiplayerPeer();
		Error err = Error.Failed;
		int port = DefaultGamePort;
		for (; port < DefaultGamePort + 20; port++)
		{
			err = peer.CreateServer(port, MaxPeers);
			if (err == Error.Ok) break;
		}
		if (err != Error.Ok)
		{
			Notify.Post($"Couldn't create a LAN room ({err}).", Type: NotificationIcons.NotificationType.Warning);
			return false;
		}
		Multiplayer.MultiplayerPeer = peer;
		GamePort = port;
		StartHosting(roomName, online: false, $"Room created on port {port}.");
		GD.Print($"[LAN] Hosting \"{State.Name}\" on port {port}");
		return true;
	}

	/// <summary>Create a room on the online server (needs a logged-in account). Same room logic as LAN.</summary>
	public bool HostOnline(string roomName)
	{
		Leave(silent: true);
		if (!OnlineApi.Available)
		{
			Notify.Post("Log in to host online rooms.", Type: NotificationIcons.NotificationType.Warning);
			return false;
		}
		var name = Clean(roomName, MaxRoomNameLength, $"{LocalName()}'s room");
		var peer = new RelayMultiplayerPeer();
		var err = peer.Host(name, Protocol, GameVersion());
		if (err != Error.Ok)
		{
			Notify.Post(peer.CloseReason ?? $"Couldn't create an online room ({err}).", Type: NotificationIcons.NotificationType.Warning);
			return false;
		}
		Multiplayer.MultiplayerPeer = peer;
		StartHosting(name, online: true, "Online room created. Anyone on the server can join from the lounge.");
		GD.Print($"[ONLINE] Hosting \"{State.Name}\"");
		return true;
	}

	private void StartHosting(string roomName, bool online, string firstLine)
	{
		IsServer = true;
		IsOnline = online;
		State = new LanRoomState
		{
			Name = Clean(roomName, MaxRoomNameLength, $"{LocalName()}'s room"),
			HostId = 1,
			HostName = LocalName(),
			Players = { new LanPlayer { Id = 1, Name = LocalName() } },
		};
		ChatLog.Clear();
		AddChatLine(null, $"{firstLine} Everyone can add beatmaps to the queue; picks take turns (round robin).");
		_beaconTimer = 0;
		MarkDirty();
	}

	/// <summary>Join a room. The lobby scene opens once the host sends the room state.</summary>
	public bool Join(string address, int port)
	{
		Leave(silent: true);
		var peer = new ENetMultiplayerPeer();
		var err = peer.CreateClient(address, port);
		if (err != Error.Ok)
		{
			Notify.Post($"Couldn't connect to {address}:{port} ({err}).", Type: NotificationIcons.NotificationType.Warning);
			return false;
		}
		Multiplayer.MultiplayerPeer = peer;
		IsServer = false;
		_joining = true;
		Notify.Post($"Connecting to {address}:{port}...");
		GD.Print($"[LAN] Joining {address}:{port}");
		return true;
	}

	/// <summary>Join an online room by its id (from <see cref="OnlineRooms"/>).</summary>
	public bool JoinOnline(long roomId)
	{
		Leave(silent: true);
		if (!OnlineApi.Available)
		{
			Notify.Post("Log in to join online rooms.", Type: NotificationIcons.NotificationType.Warning);
			return false;
		}
		var peer = new RelayMultiplayerPeer();
		var err = peer.Join(roomId);
		if (err != Error.Ok)
		{
			Notify.Post(peer.CloseReason ?? $"Couldn't reach the server ({err}).", Type: NotificationIcons.NotificationType.Warning);
			return false;
		}
		Multiplayer.MultiplayerPeer = peer;
		IsServer = false;
		IsOnline = true;
		_joining = true;
		Notify.Post("Joining online room...");
		GD.Print($"[ONLINE] Joining room {roomId}");
		return true;
	}

	/// <summary>Leave the current room (closes it for everyone if we're the host).</summary>
	public void Leave(bool silent = false)
	{
		if (Multiplayer.MultiplayerPeer is ENetMultiplayerPeer or RelayMultiplayerPeer)
		{
			Multiplayer.MultiplayerPeer.Close();
			if (!silent) Notify.Post(IsServer ? $"Closed the {RoomKind} room." : $"Left the {RoomKind} room.");
		}
		Multiplayer.MultiplayerPeer = new OfflineMultiplayerPeer();
		ResetSession();
	}

	public void SetReady(bool ready)
	{
		if (State == null) return;
		if (IsServer) ServerSetReady(1, ready);
		else RpcId(1, MethodName.RpcSetReady, ready);
	}

	public void SendChat(string text)
	{
		if (State == null || string.IsNullOrWhiteSpace(text)) return;
		if (IsServer) ServerChat(1, text);
		else RpcId(1, MethodName.RpcChat, text);
	}

	// ---- Round-robin queue + per-player loadout (anyone in the room) ----

	/// <summary>Put a beatmap (index into the local library) in the queue.</summary>
	public void AddToQueue(int index)
	{
		if (State == null) return;
		var map = BuildRef(index);
		if (map == null) return;
		var (mods, speed) = LocalGlobalMods();
		int mode = SettingsOperator.GetSelectedGameMode();
		if (IsServer) ServerAddToQueue(1, map, mods.ToArray(), speed, mode);
		else RpcId(1, MethodName.RpcAddToQueue, JsonSerializer.Serialize(map), mods.ToArray(), speed, mode);
	}

	/// <summary>Swap the beatmap of one of your own queue items.</summary>
	public void ReplaceQueueItem(int itemId, int index)
	{
		if (State == null) return;
		var map = BuildRef(index);
		if (map == null) return;
		var (mods, speed) = LocalGlobalMods();
		int mode = SettingsOperator.GetSelectedGameMode();
		if (IsServer) ServerReplaceQueueItem(1, itemId, map, mods.ToArray(), speed, mode);
		else RpcId(1, MethodName.RpcReplaceQueueItem, itemId, JsonSerializer.Serialize(map), mods.ToArray(), speed, mode);
	}

	/// <summary>Remove a queue item (your own, or anyone's if you're the host).</summary>
	public void RemoveQueueItem(int itemId)
	{
		if (State == null) return;
		if (IsServer) ServerRemoveQueueItem(1, itemId);
		else RpcId(1, MethodName.RpcRemoveQueueItem, itemId);
	}

	/// <summary>Play a different difficulty of the up-next beatmap set (-1 = back to the queue item's own).</summary>
	public void SetDifficulty(int index)
	{
		if (State?.Beatmap == null) return;
		var map = index < 0 ? null : BuildRef(index);
		if (index >= 0 && map == null) return;
		if (map != null && !SameSet(map, State.Beatmap))
		{
			Notify.Post("Pick a difficulty from the same song as the one up next.", Type: NotificationIcons.NotificationType.Warning);
			return;
		}
		var json = map == null ? "" : JsonSerializer.Serialize(map);
		if (IsServer) ServerSetDifficulty(1, json);
		else RpcId(1, MethodName.RpcSetDifficulty, json);
	}

	/// <summary>Sends the local mod selection (allowed mods only) to the room if it changed.</summary>
	public void SendLocalMods()
	{
		if (State == null) return;
		var mods = LocalAllowedMods();
		var me = LocalPlayer;
		if (me != null && me.Mods.SequenceEqual(mods)) return;
		if (IsServer) ServerSetMods(1, mods.ToArray());
		else RpcId(1, MethodName.RpcSetMods, mods.ToArray());
	}

	public void SetActivity(LanActivity activity)
	{
		if (State == null || LocalPlayer?.Activity == activity) return;
		if (IsServer) ServerSetActivity(1, (int)activity);
		else RpcId(1, MethodName.RpcSetActivity, (int)activity);
	}

	/// <summary>The chart the local player will play: their own difficulty pick, or the up-next item's.</summary>
	public LanBeatmapRef LocalChart => ChartFor(LocalPlayer);

	public LanBeatmapRef ChartFor(LanPlayer p) => p?.Difficulty ?? State?.Beatmap;

	/// <summary>Number of queue items the player owns.</summary>
	public int QueueCountFor(long playerId) => State?.Queue.Count(q => q.OwnerId == playerId) ?? 0;

	public static bool IsModAllowed(string mod) => AllowedMods.Contains(mod);

	/// <summary>Whether song select's mod screen should let this mod be turned on right now.</summary>
	public static bool IsModSelectable(string mod) =>
		IsModAllowed(mod) || ((Picking is PickMode.AddToQueue or PickMode.ReplaceItem or PickMode.StartingBeatmap) && GlobalMods.Contains(mod));

	/// <summary>"DT 1.50x" style label for room-wide mods ("" when none).</summary>
	public static string GlobalModLabel(List<string> mods, float speed)
	{
		var alias = ModAlias(mods);
		return alias.Length == 0 ? "" : $"{alias} {speed:0.##}x";
	}

	public static string ModAlias(IEnumerable<string> mods)
	{
		var parts = mods?.Select(m => ModAliases.TryGetValue(m, out var a) ? a : m).ToList();
		return parts == null || parts.Count == 0 ? "" : string.Join(" ", parts);
	}

	/// <summary>Turns off mods that aren't allowed in LAN matches.</summary>
	public static void StripDisallowedMods()
	{
		bool changed = false;
		foreach (var key in ModsOperator.Mods.Keys.ToList())
		{
			if (!ModsOperator.Mods[key] || IsModAllowed(key)) continue;
			ModsOperator.Mods[key] = false;
			changed = true;
		}
		if (changed) ModsOperator.Refresh();
		ModsOperator.GetModAlias(); // refreshes ModsEnabled
	}

	// ---- Song select as a picker ----

	/// <summary>Opens song select to pick a beatmap for the room. Song select calls <see cref="FinishPick"/>.</summary>
	public void BeginPick(PickMode mode, int itemId = 0)
	{
		if (State == null || mode == PickMode.None) return;
		if (mode == PickMode.AddToQueue && QueueCountFor(LocalId) >= MaxQueuePerPlayer)
		{
			Notify.Post($"You can have up to {MaxQueuePerPlayer} beatmaps in the queue.", Type: NotificationIcons.NotificationType.Warning);
			return;
		}
		Picking = mode;
		_pickItemId = itemId;
		SettingsOperator.MultiSelectingBeatmap = true;
		StripDisallowedMods();
		if (mode == PickMode.ReplaceItem && State.Queue.FirstOrDefault(q => q.Id == itemId) is { } editing)
			ApplyGlobalMods(editing.Mods, editing.Speed); // start from the item's current DT / HT
		if (LocalPlayer?.Ready == true) SetReady(false); // don't get pulled into a match from song select
		SetActivity(LanActivity.Selecting);

		// Start song select on something that makes sense for what we're picking.
		int focus = mode switch
		{
			PickMode.OwnDifficulty => FindLocalBeatmap(LocalChart),
			PickMode.ReplaceItem => FindLocalBeatmap(State.Queue.FirstOrDefault(q => q.Id == itemId)?.Beatmap),
			_ => -1,
		};
		if (focus >= 0 && SettingsOperator.SessionConfig.SongID != focus) SelectSong(focus);
		GetNode<SceneTransition>("/root/Transition").Switch(SongSelectScene);
	}

	/// <summary>Opens song select to choose the first beatmap of a room that doesn't exist yet (Create Room dialog).</summary>
	public void BeginStartingBeatmapPick()
	{
		Picking = PickMode.StartingBeatmap;
		SettingsOperator.MultiSelectingBeatmap = true;
		StripDisallowedMods();
		ApplyGlobalMods(RoomDraft.Mods, RoomDraft.Speed);
		if (RoomDraft.BeatmapIndex >= 0 && RoomDraft.BeatmapIndex < SettingsOperator.Beatmaps.Count && SettingsOperator.SessionConfig.SongID != RoomDraft.BeatmapIndex)
			SelectSong(RoomDraft.BeatmapIndex);
		GetNode<SceneTransition>("/root/Transition").Switch(SongSelectScene);
	}

	/// <summary>Create a room from the Create Room dialog: LAN or online, then queue its starting beatmap.</summary>
	public bool HostRoom(string name, bool online, int startingBeatmap, List<string> mods, float speed)
	{
		if (!(online ? HostOnline(name) : Host(name))) return false;
		if (startingBeatmap >= 0 && startingBeatmap < SettingsOperator.Beatmaps.Count)
		{
			ApplyGlobalMods(mods ?? new List<string>(), speed); // room-wide DT / HT on the first item
			AddToQueue(startingBeatmap);
			StripDisallowedMods();
		}
		return true;
	}

	/// <summary>Song select is done: <paramref name="index"/> is the picked library index, or null if cancelled.</summary>
	public void FinishPick(int? index)
	{
		var mode = Picking;
		Picking = PickMode.None;
		SettingsOperator.MultiSelectingBeatmap = false;
		if (mode == PickMode.StartingBeatmap)
		{
			if (index is int picked && picked >= 0 && picked < SettingsOperator.Beatmaps.Count)
			{
				RoomDraft.BeatmapIndex = picked;
				(RoomDraft.Mods, RoomDraft.Speed) = LocalGlobalMods();
			}
			RoomDraft.Reopen = true;
			StripDisallowedMods();
			GetNode<SceneTransition>("/root/Transition").Switch(LoungeScene);
			return;
		}
		if (State != null && index is int i && i >= 0 && i < SettingsOperator.Beatmaps.Count)
		{
			switch (mode)
			{
				case PickMode.AddToQueue: AddToQueue(i); break;
				case PickMode.ReplaceItem: ReplaceQueueItem(_pickItemId, i); break;
				case PickMode.OwnDifficulty: SetDifficulty(FindLocalBeatmap(State.Beatmap) == i ? -1 : i); break;
			}
		}
		StripDisallowedMods(); // DT / HT went onto the queue item; they aren't part of your own loadout
		GetNode<SceneTransition>("/root/Transition").Switch(ReturnScene);
	}

	/// <summary>Which library entries song select lists while picking (only the up-next set when changing difficulty).</summary>
	public bool PickFilter(int index)
	{
		return Picking != PickMode.OwnDifficulty || InUpNextSet(index);
	}

	private bool InUpNextSet(int index)
	{
		if (State?.Beatmap == null || index < 0 || index >= SettingsOperator.Beatmaps.Count) return false;
		var b = SettingsOperator.Beatmaps[index];
		var r = State.Beatmap;
		return (r.BeatmapSetID > 0 && b.BeatmapSetID == r.BeatmapSetID) || (b.Title == r.Title && b.Artist == r.Artist);
	}

	/// <summary>True when the local library has any difficulty of the up-next set.</summary>
	public bool HasLocalSet()
	{
		if (State?.Beatmap == null) return false;
		if (FindLocalBeatmap(LocalChart) >= 0) return true;
		for (int i = 0; i < SettingsOperator.Beatmaps.Count; i++)
			if (InUpNextSet(i)) return true;
		return false;
	}

	/// <summary>The lobby screen opened (from song select, results or a match): sync mods, status and the preview song.</summary>
	public void OnLobbyOpened()
	{
		if (State == null) return;
		Picking = PickMode.None;
		SettingsOperator.MultiSelectingBeatmap = false;
		ShowingMatchResults = false;
		StripDisallowedMods();
		SendLocalMods();
		SetWatchTarget(-1);
		Feeds.Clear();
		SetActivity(LanActivity.Lobby);
		int index = FindLocalBeatmap(LocalChart);
		if (index >= 0 && State.Phase == LanPhase.Lobby && SettingsOperator.SessionConfig.SongID != index) SelectSong(index);
	}

	// ---- Spectating ----

	/// <summary>Can the local player watch the match right now? (it's running and we're not playing in it)</summary>
	public bool CanSpectate => State?.Phase == LanPhase.Playing && LocalPlayer is { } me && (!me.InMatch || me.Finished)
		&& State.Players.Any(p => p.InMatch && p.Id != LocalId);

	public void BeginSpectate()
	{
		if (!CanSpectate) return;
		Feeds.Clear();
		SetWatchTarget(0);
		GetNode<SceneTransition>("/root/Transition").Switch(SpectateScene);
	}

	/// <summary>-1 stop watching, 0 watch everyone (grid), or a player id (one by one).</summary>
	public void SetWatchTarget(long target)
	{
		if (State == null || LocalPlayer == null || LocalPlayer.WatchTarget == target) return;
		if (IsServer) ServerSetWatchTarget(1, target);
		else RpcId(1, MethodName.RpcSetWatchTarget, target);
	}

	/// <summary>The local match ended: report it and show the results screen.</summary>
	public void FinishMatchToResults()
	{
		ReportFinished(quit: false);
		ShowingMatchResults = true;
		SetActivity(LanActivity.Results);
		GetNode<SceneTransition>("/root/Transition").Switch(ResultsScene);
	}

	/// <summary>Host only: change the lobby countdown length.</summary>
	public void HostAdjustTimer(int deltaSeconds)
	{
		if (!IsHost) return;
		if (IsServer) ServerAdjustTimer(1, deltaSeconds);
		else RpcId(1, MethodName.RpcAdjustTimer, deltaSeconds);
	}

	/// <summary>Host only: skip the rest of the countdown (ready players start in 5 seconds).</summary>
	public void HostStartNow()
	{
		if (!IsHost) return;
		if (IsServer) ServerStartNow(1);
		else RpcId(1, MethodName.RpcStartNow);
	}

	/// <summary>Host only: rename the room (shows up in everyone's lobby and the LAN room list).</summary>
	public void HostRename(string name)
	{
		if (!IsHost) return;
		if (IsServer) ServerRename(1, name);
		else RpcId(1, MethodName.RpcRename, name ?? "");
	}

	/// <summary>Host only: hand the host controls to another player in the room.</summary>
	public void HostTransfer(long playerId)
	{
		if (!IsHost || playerId == LocalId) return;
		if (IsServer) ServerTransferHost(1, playerId);
		else RpcId(1, MethodName.RpcTransferHost, playerId);
	}

	/// <summary>Ask the room for the selected beatmap; the server or the host sends it over the LAN.</summary>
	public void RequestBeatmapFromHost()
	{
		if (State?.Beatmap == null || TransferProgress >= 0 || FindLocalBeatmap(State.Beatmap) >= 0) return;
		TransferProgress = 0;
		if (IsServer) ServerRouteBeatmapRequest(1);
		else RpcId(1, MethodName.RpcRequestBeatmap);
	}

	/// <summary>Called when the local player's gameplay ends (finished or quit).</summary>
	public void ReportFinished(bool quit)
	{
		if (!LocalInMatch) return;
		LocalInMatch = false;
		// Don't clear ApiOperator.LeaderboardList here: the gameplay scene is still fading out and reads it.
		var c = (SettingsOperator.Gameplaycfg.Score, SettingsOperator.Gameplaycfg.MaxCombo, Accuracy: LocalAccuracy(),
			SettingsOperator.Gameplaycfg.Max, SettingsOperator.Gameplaycfg.Great, SettingsOperator.Gameplaycfg.Meh, SettingsOperator.Gameplaycfg.Bad);
		if (State == null) return; // host vanished mid-match
		if (IsServer) ServerFinished(1, c.Score, c.MaxCombo, c.Accuracy, c.Max, c.Great, c.Meh, c.Bad, quit);
		else RpcId(1, MethodName.RpcFinished, c.Score, c.MaxCombo, c.Accuracy, c.Max, c.Great, c.Meh, c.Bad, quit);
	}

	// ---- Discovery ----

	public void StartDiscovery()
	{
		if (_discoveryRx != null) return;
		try
		{
			_discoveryRx = new UdpClient { ExclusiveAddressUse = false };
			// ReuseAddress lets several game instances on one PC listen at once (handy for testing).
			_discoveryRx.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
			_discoveryRx.Client.Bind(new IPEndPoint(IPAddress.Any, DiscoveryPort));
		}
		catch (Exception e)
		{
			GD.PrintErr($"[LAN] Couldn't listen for rooms on UDP {DiscoveryPort}: {e.Message}");
			Notify.Post($"Can't search for LAN rooms (UDP port {DiscoveryPort} unavailable).", Type: NotificationIcons.NotificationType.Warning);
			_discoveryRx?.Close();
			_discoveryRx = null;
		}
	}

	public void StopDiscovery()
	{
		_discoveryRx?.Close();
		_discoveryRx = null;
		Rooms.Clear();
		RoomsVersion++;
	}

	public void ClearRooms()
	{
		Rooms.Clear();
		RoomsVersion++;
	}

	// =====================================================================
	// Frame update
	// =====================================================================

	public override void _Process(double delta)
	{
		PollDiscovery();
		PollOnlineBrowser(delta);
		RelayMultiplayerPeer.PollLingering();

		if (State == null) return;

		if (State.CountdownActive && State.Phase == LanPhase.Lobby)
			TimeLeftNow = Math.Max(0, TimeLeftNow - delta);

		if (IsServer)
		{
			ServerTick(delta);
			if (IsOnline) UpdateOnlineListing(delta);
			else SendBeacon(delta);
			if (IsOnline && Relay?.GetConnectionStatus() == MultiplayerPeer.ConnectionStatus.Disconnected)
			{
				LostOnlineHost(Relay.CloseReason ?? "Lost the connection to the server.");
				return;
			}
		}
		PumpOutgoingTransfers(); // anyone holding the map may be asked to share it

		// Beatmap library changed (e.g. a map arrived from the host) -> re-check whether we can play.
		if (_lastBeatmapCount != SettingsOperator.Beatmaps.Count)
		{
			_lastBeatmapCount = SettingsOperator.Beatmaps.Count;
			RecheckLocalMap();
		}

		PumpSpectateFeed(delta);

		// Stream our live score to the host while playing.
		if (LocalInMatch && SettingsOperator.inGameplay)
		{
			_scoreTimer -= delta;
			if (_scoreTimer <= 0)
			{
				_scoreTimer = 0.25;
				var g = (SettingsOperator.Gameplaycfg.Score, SettingsOperator.Gameplaycfg.Combo, SettingsOperator.Gameplaycfg.MaxCombo,
					Accuracy: LocalAccuracy(), SettingsOperator.Gameplaycfg.Max, SettingsOperator.Gameplaycfg.Great,
					SettingsOperator.Gameplaycfg.Meh, SettingsOperator.Gameplaycfg.Bad);
				if (IsServer) ServerScore(1, g.Score, g.Combo, g.MaxCombo, g.Accuracy, g.Max, g.Great, g.Meh, g.Bad);
				else RpcId(1, MethodName.RpcScore, g.Score, g.Combo, g.MaxCombo, g.Accuracy, g.Max, g.Great, g.Meh, g.Bad);
			}
		}
	}

	// =====================================================================
	// Host logic (authoritative)
	// =====================================================================

	private void ServerTick(double delta)
	{
		var s = State;
		if (s.Phase == LanPhase.Lobby)
		{
			if (s.Beatmap == null || s.Players.Count == 0)
			{
				if (s.CountdownActive) { s.CountdownActive = false; MarkDirty(); }
			}
			else
			{
				if (!s.CountdownActive)
				{
					s.CountdownActive = true;
					s.TimeLeft = s.LobbyTimer;
					MarkDirty();
				}

				if (s.Players.All(p => p.Ready) && s.TimeLeft > AllReadyCountdown)
				{
					s.TimeLeft = AllReadyCountdown;
					ServerChat(0, $"Everyone is ready! Starting in {AllReadyCountdown:0} seconds.");
					MarkDirty();
				}

				int before = (int)Math.Ceiling(s.TimeLeft);
				s.TimeLeft -= delta;
				if ((int)Math.Ceiling(s.TimeLeft) != before) MarkDirty(); // keep clients' timers honest once a second

				if (s.TimeLeft <= 0)
				{
					var players = s.Players.Where(p => p.Ready && p.HasMap).ToList();
					if (players.Count == 0)
					{
						s.TimeLeft = s.LobbyTimer;
						ServerChat(0, "Nobody was ready, so the timer restarted.");
						MarkDirty();
					}
					else
					{
						ServerStartMatch(players);
					}
				}
			}
		}

		_syncTimer -= delta;
		if (_dirty && _syncTimer <= 0)
		{
			_syncTimer = s.Phase == LanPhase.Playing ? 0.25 : 0.1;
			BroadcastState();
		}
	}

	private void ServerStartMatch(List<LanPlayer> players)
	{
		var s = State;
		s.Phase = LanPhase.Playing;
		s.MatchId++;
		s.CountdownActive = false;
		var item = s.Queue.FirstOrDefault(q => q.Id == s.CurrentItemId);
		if (item != null)
		{
			s.Queue.Remove(item); // played items leave the queue; the rotation moves on from its owner
			s.LastPickOwnerId = item.OwnerId;
		}
		foreach (var p in s.Players)
		{
			p.InMatch = players.Contains(p);
			p.Ready = false;
			p.Finished = false;
			p.Quit = false;
			p.Score = p.Combo = p.MaxCombo = p.Max = p.Great = p.Meh = p.Bad = 0;
			p.Accuracy = 1f;
		}
		_specCharts.Clear();
		ServerChat(0, $"Match started with {players.Count} player{(players.Count == 1 ? "" : "s")}: {string.Join(", ", players.Select(p => p.Name))}");
		BroadcastState(); // reliable + ordered: clients get the InMatch flags before the start signal
		Rpc(MethodName.RpcStartMatch, s.MatchId);
		OnStartMatch(s.MatchId);
	}

	private void ServerEndMatchIfDone()
	{
		var s = State;
		if (s == null || s.Phase != LanPhase.Playing) return;
		if (!s.Players.Where(p => p.InMatch).All(p => p.Finished)) return;

		var ranked = s.Players.Where(p => p.InMatch)
			.OrderBy(p => p.Quit).ThenByDescending(p => p.Score).ToList();
		s.LastResults = ranked.Select((p, i) => new LanResult
		{
			Rank = i + 1, Name = p.Name, Score = p.Score, Accuracy = p.Accuracy, MaxCombo = p.MaxCombo, Quit = p.Quit,
			Version = ChartFor(p)?.Version, Mods = ModAlias(s.CurrentMods.Concat(p.Mods)),
		}).ToList();
		// Players who left mid-match aren't in s.Players anymore, so results only list who stayed.

		s.Phase = LanPhase.Lobby;
		s.CountdownActive = false;
		foreach (var p in s.Players) { p.InMatch = false; p.Ready = false; }

		ServerChat(0, "Match finished! Results:");
		foreach (var r in s.LastResults)
		{
			var extra = $"[{r.Version}]{(string.IsNullOrEmpty(r.Mods) ? "" : " +" + r.Mods)}";
			ServerChat(0, r.Quit
				? $"#{r.Rank} {r.Name} - quit"
				: $"#{r.Rank} {r.Name} - {r.Score:N0} ({r.Accuracy:P2}, {r.MaxCombo}x) {extra}");
		}
		ServerQueueChanged(); // next pick in the rotation is up
		MarkDirty();
	}

	private void ServerSetReady(long id, bool ready)
	{
		var p = FindPlayer(id);
		if (p == null || State.Phase != LanPhase.Lobby) return;
		if (ready && !p.HasMap) return;
		if (p.Ready == ready) return;
		p.Ready = ready;
		MarkDirty();
	}

	private void ServerSetHasMap(long id, bool has, string checksum)
	{
		var p = FindPlayer(id);
		if (p == null) return;
		if (checksum != ChartFor(p)?.Checksum) return; // stale answer for a previous beatmap / difficulty
		p.HasMap = has;
		if (!has) p.Ready = false;
		MarkDirty();
	}

	// ---- Host controls (validated against State.HostId, whoever sent them) ----

	private bool SentByHost(long sender) => State != null && sender == State.HostId && FindPlayer(sender) != null;

	// ---- Queue (anyone may add; owners edit/remove their own, the host may remove anything) ----

	private LanBeatmapRef SanitizeMap(LanBeatmapRef map)
	{
		if (string.IsNullOrEmpty(map?.Checksum) || map.Checksum.Length < 8) return null;
		map.Title = Clean(map.Title, 200, "Unknown");
		map.Artist = Clean(map.Artist, 200, "Unknown");
		map.Version = Clean(map.Version, 100, "?");
		map.Mapper = Clean(map.Mapper, 100, "?");
		if (!float.IsFinite(map.Levelrating)) map.Levelrating = 0;
		if (!float.IsFinite(map.LengthMs)) map.LengthMs = 0;
		return map;
	}

	private void ServerAddToQueue(long sender, LanBeatmapRef map, string[] mods, float speed, int mode)
	{
		var p = FindPlayer(sender);
		map = SanitizeMap(map);
		if (p == null || map == null) return;
		if (State.Queue.Count(q => q.OwnerId == sender) >= MaxQueuePerPlayer || State.Queue.Count >= MaxQueueTotal)
		{
			SendNotice(sender, "The queue is full for you right now.");
			return;
		}
		var (cleanMods, cleanSpeed) = CleanGlobalMods(mods, speed);
		mode = CleanGameMode(mode);
		State.Queue.Add(new LanQueueItem { Id = State.NextQueueId++, OwnerId = sender, OwnerName = p.Name, Beatmap = map, Mods = cleanMods, Speed = cleanSpeed, GameMode = mode });
		var label = GlobalModLabel(cleanMods, cleanSpeed);
		ServerChat(0, $"{p.Name} added {map.Display} [{GameModeName(mode)}]{(label.Length > 0 ? " +" + label : "")} to the queue.");
		ServerQueueChanged();
	}

	private void ServerReplaceQueueItem(long sender, int itemId, LanBeatmapRef map, string[] mods, float speed, int mode)
	{
		var item = State.Queue.FirstOrDefault(q => q.Id == itemId);
		map = SanitizeMap(map);
		if (item == null || map == null || item.OwnerId != sender) return;
		var (cleanMods, cleanSpeed) = CleanGlobalMods(mods, speed);
		mode = CleanGameMode(mode);
		if (item.Beatmap?.Checksum == map.Checksum && item.Mods.SequenceEqual(cleanMods) && item.Speed == cleanSpeed && item.GameMode == mode) return;
		item.Beatmap = map;
		item.Mods = cleanMods;
		item.Speed = cleanSpeed;
		item.GameMode = mode;
		var label = GlobalModLabel(cleanMods, cleanSpeed);
		ServerChat(0, $"{item.OwnerName} changed their pick to {map.Display} [{GameModeName(mode)}]{(label.Length > 0 ? " +" + label : "")}.");
		ServerQueueChanged();
	}

	private void ServerRemoveQueueItem(long sender, int itemId)
	{
		var item = State.Queue.FirstOrDefault(q => q.Id == itemId);
		if (item == null || (item.OwnerId != sender && !SentByHost(sender))) return;
		State.Queue.Remove(item);
		var by = item.OwnerId == sender ? item.OwnerName : $"The host ({FindPlayer(sender)?.Name})";
		ServerChat(0, $"{by} removed {item.Beatmap?.Display} from the queue.");
		ServerQueueChanged();
	}

	private void ServerSetDifficulty(long sender, string json)
	{
		var p = FindPlayer(sender);
		if (p == null || State.Phase != LanPhase.Lobby || State.Beatmap == null) return;
		LanBeatmapRef map = null;
		if (!string.IsNullOrEmpty(json))
		{
			try { map = SanitizeMap(JsonSerializer.Deserialize<LanBeatmapRef>(json)); }
			catch { return; }
			if (map == null || !SameSet(map, State.Beatmap)) return;
			if (map.Checksum == State.Beatmap.Checksum) map = null; // same as the item's own difficulty
		}
		if (p.Difficulty?.Checksum == map?.Checksum) return;
		p.Difficulty = map;
		p.Ready = false;
		p.HasMap = map != null; // they picked it from their library; otherwise they re-report for the default
		if (sender == 1) RecheckLocalMap(force: true); // clients recheck when the new state arrives
		MarkDirty();
	}

	private void ServerSetMods(long sender, string[] mods)
	{
		var p = FindPlayer(sender);
		if (p == null || mods == null) return; // fine mid-match too: mods are applied when the next match starts
		var clean = mods.Where(IsModAllowed).Distinct().ToList();
		if (clean.Contains("black-out")) clean.Remove("slice"); // these two don't stack
		if (p.Mods.SequenceEqual(clean)) return;
		p.Mods = clean;
		MarkDirty();
	}

	private void ServerSetActivity(long sender, int activity)
	{
		var p = FindPlayer(sender);
		if (p == null || !Enum.IsDefined(typeof(LanActivity), activity)) return;
		p.Activity = (LanActivity)activity;
		MarkDirty();
	}

	private void ServerSetWatchTarget(long sender, long target)
	{
		var p = FindPlayer(sender);
		if (p == null || target < -1 || target == sender) return;
		bool wasWatching = p.WatchTarget >= 0;
		p.WatchTarget = target;
		if (target >= 0 && !wasWatching)
			foreach (var (id, c) in _specCharts) SendChartTo(sender, id, c); // joined mid-song
		if (target >= 0) p.Activity = LanActivity.Spectating;
		else if (p.Activity == LanActivity.Spectating) p.Activity = LanActivity.Lobby;
		MarkDirty();
	}

	/// <summary>Passes a player's live play on to whoever is watching them.</summary>
	private void ServerRelaySpec(long from, byte[] packet)
	{
		var p = FindPlayer(from);
		if (p == null || !p.InMatch || packet == null || packet.Length == 0 || packet.Length > MaxSpectatePacket) return;
		foreach (var v in State.Players)
		{
			if (v.Id == from || (v.WatchTarget != 0 && v.WatchTarget != from)) continue;
			if (v.Id == LocalId) OnSpecTick(from, packet);
			else RpcId(v.Id, MethodName.RpcSpecTickFrom, from, packet);
		}
	}

	private void ServerStoreChart(long from, byte[] chart)
	{
		var p = FindPlayer(from);
		if (p == null || !p.InMatch || chart == null || chart.Length > MaxChartBytes) return;
		if (LanSpecChart.Decode(chart, MaxChartNotes) == null) return; // malformed: don't relay it
		_specCharts[from] = chart;
		foreach (var v in State.Players)
			if (v.Id != from && v.WatchTarget >= 0) SendChartTo(v.Id, from, chart);
	}

	private void SendChartTo(long viewer, long player, byte[] chart)
	{
		if (viewer == LocalId) OnSpecChart(player, chart);
		else RpcId(viewer, MethodName.RpcSpecChartFrom, player, chart);
	}

	/// <summary>Re-sorts the queue round-robin	/// <summary>Re-sorts the queue round-robin and, in the lobby, makes its head the beatmap up next.</summary>
	private void ServerQueueChanged()
	{
		ServerReorderQueue();
		if (State.Phase == LanPhase.Lobby) ServerSyncHead();
		MarkDirty();
	}

	/// <summary>
	/// Round robin: players take turns in join order, starting with whoever comes after the owner of the last
	/// played item. Round 1 is everyone's first pick, round 2 everyone's second pick, and so on; each player's
	/// own picks keep the order they added them in. Once the countdown is in its last seconds the head stays put.
	/// </summary>
	private void ServerReorderQueue()
	{
		var s = State;
		var order = s.Players.Select(p => p.Id).ToList();
		int last = order.IndexOf(s.LastPickOwnerId);
		if (last >= 0) order = order.Skip(last + 1).Concat(order.Take(last + 1)).ToList();

		var byOwner = order.ToDictionary(id => id, id => s.Queue.Where(q => q.OwnerId == id).OrderBy(q => q.Id).ToList());
		var sorted = new List<LanQueueItem>(s.Queue.Count);
		for (int round = 0; ; round++)
		{
			bool any = false;
			foreach (var id in order)
			{
				if (round >= byOwner[id].Count) continue;
				sorted.Add(byOwner[id][round]);
				any = true;
			}
			if (!any) break;
		}
		sorted.AddRange(s.Queue.Where(q => !sorted.Contains(q)).OrderBy(q => q.Id)); // owner gone: play them last

		bool lockHead = s.Phase == LanPhase.Lobby && s.CountdownActive && s.TimeLeft <= AllReadyCountdown;
		var head = lockHead ? sorted.FirstOrDefault(q => q.Id == s.CurrentItemId) : null;
		if (head != null) { sorted.Remove(head); sorted.Insert(0, head); }
		s.Queue = sorted;
	}

	/// <summary>Points <see cref="LanRoomState.Beatmap"/> at the head of the queue, resetting readiness if it changed.</summary>
	private void ServerSyncHead()
	{
		var s = State;
		var head = s.Queue.FirstOrDefault();
		if (head?.Id == s.CurrentItemId && head?.Beatmap?.Checksum == s.Beatmap?.Checksum && (head != null || s.Beatmap == null)
			&& (head == null || (head.Mods.SequenceEqual(s.CurrentMods) && head.Speed == s.CurrentSpeed && head.GameMode == s.CurrentGameMode))) return;

		s.CurrentItemId = head?.Id ?? 0;
		s.CurrentMods = head?.Mods.ToList() ?? new List<string>();
		s.CurrentSpeed = head?.Speed ?? 1f;
		s.CurrentGameMode = head?.GameMode ?? 0;
		s.CurrentOwnerId = head?.OwnerId ?? 0;
		s.CurrentOwnerName = head?.OwnerName;
		s.Beatmap = head?.Beatmap;
		foreach (var p in s.Players)
		{
			p.Ready = false;
			p.Difficulty = null;
			p.HasMap = head != null && p.Id == head.OwnerId; // everyone else re-reports once they've checked their library
		}
		s.CountdownActive = false; // restarts the "time till play" timer
		var upNextMods = head == null ? "" : GlobalModLabel(head.Mods, head.Speed);
		if (head != null) ServerChat(0, $"Up next: {head.Beatmap.Display} [{GameModeName(head.GameMode)}]{(upNextMods.Length > 0 ? " +" + upNextMods : "")}, picked by {head.OwnerName}.");
		RecheckLocalMap(force: true); // the server player needs the map too
		MarkDirty();
	}

	private void SendNotice(long peer, string text)
	{
		if (peer == LocalId) Notify.Post(text, Type: NotificationIcons.NotificationType.Warning);
		else RpcId(peer, MethodName.RpcKicked, text); // RpcKicked just shows a warning
	}

	private static bool SameSet(LanBeatmapRef a, LanBeatmapRef b) =>
		a != null && b != null && ((a.BeatmapSetID > 0 && a.BeatmapSetID == b.BeatmapSetID) || (a.Title == b.Title && a.Artist == b.Artist));

	private void ServerAdjustTimer(long sender, int deltaSeconds)
	{
		if (!SentByHost(sender)) return;
		State.LobbyTimer = Math.Clamp(State.LobbyTimer + deltaSeconds, MinLobbyTimer, MaxLobbyTimer);
		if (State.CountdownActive && State.TimeLeft > State.LobbyTimer) State.TimeLeft = State.LobbyTimer;
		MarkDirty();
	}

	private void ServerStartNow(long sender)
	{
		if (!SentByHost(sender) || !State.CountdownActive || State.TimeLeft <= AllReadyCountdown) return;
		State.TimeLeft = AllReadyCountdown;
		ServerChat(0, $"Host is starting the match in {AllReadyCountdown:0} seconds!");
		MarkDirty();
	}

	private void ServerRename(long sender, string name)
	{
		if (!SentByHost(sender)) return;
		name = Clean(name, MaxRoomNameLength, null);
		if (name == null || name == State.Name) return;
		State.Name = name;
		_beaconTimer = 0; // let the LAN room list pick the new name up right away
		ServerChat(0, $"Room renamed to \"{name}\".");
		MarkDirty();
	}

	private void ServerTransferHost(long sender, long target)
	{
		if (!SentByHost(sender) || sender == target) return;
		var to = FindPlayer(target);
		if (to == null) return;
		SetHost(to, $"{FindPlayer(sender)?.Name} made {to.Name} the host.");
	}

	private void SetHost(LanPlayer to, string announcement)
	{
		State.HostId = to.Id;
		State.HostName = to.Name;
		_beaconTimer = 0;
		ServerChat(0, announcement);
		if (to.Id == 1) Notify.Post("You're the host now!");
		MarkDirty();
	}

	/// <summary>Finds someone who can send the selected beatmap to <paramref name="requester"/>.</summary>
	private void ServerRouteBeatmapRequest(long requester)
	{
		if (State.Beatmap == null) { SendUnavailable(requester, "No beatmap is selected yet."); return; }
		if (requester != 1 && FindLocalBeatmap(State.Beatmap) >= 0) { SendBeatmapTo(requester); return; }

		// The server doesn't have it: ask the host first, then anyone else who does.
		var source = State.Players
			.Where(p => p.Id != requester && p.Id != 1 && p.HasMap)
			.OrderByDescending(p => p.Id == State.HostId)
			.FirstOrDefault();
		if (source != null) RpcId(source.Id, MethodName.RpcSendBeatmapTo, requester);
		else SendUnavailable(requester, "Nobody in the room can share this beatmap right now.");
	}

	private void SendUnavailable(long peer, string reason)
	{
		if (peer == LocalId) OnBeatmapUnavailable(reason);
		else RpcId(peer, MethodName.RpcBeatmapUnavailable, reason);
	}

	private void OnBeatmapUnavailable(string reason)
	{
		TransferProgress = -1;
		_incoming = null;
		Notify.Post(reason, Type: NotificationIcons.NotificationType.Warning);
	}

	private void ServerChat(long id, string text)
	{
		text = Clean(text, 300, null);
		if (text == null) return;
		string name = null;
		if (id != 0)
		{
			var p = FindPlayer(id);
			if (p == null) return;
			name = p.Name;
		}
		AddChatLine(name, text);
		Rpc(MethodName.RpcChatLine, name ?? "", text);
	}

	private void ServerScore(long id, int score, int combo, int maxCombo, float acc, int max, int great, int meh, int bad)
	{
		var p = FindPlayer(id);
		if (p == null || !p.InMatch || p.Finished) return;
		p.Score = score; p.Combo = combo; p.MaxCombo = maxCombo; p.Accuracy = float.IsFinite(acc) ? Math.Clamp(acc, 0f, 1f) : 1f;
		p.Max = max; p.Great = great; p.Meh = meh; p.Bad = bad;
		MarkDirty();
	}

	private void ServerFinished(long id, int score, int maxCombo, float acc, int max, int great, int meh, int bad, bool quit)
	{
		var p = FindPlayer(id);
		if (p == null || !p.InMatch || p.Finished) return;
		p.Score = score; p.MaxCombo = maxCombo; p.Accuracy = float.IsFinite(acc) ? Math.Clamp(acc, 0f, 1f) : 1f;
		p.Max = max; p.Great = great; p.Meh = meh; p.Bad = bad;
		p.Finished = true;
		p.Quit = quit;
		ServerChat(0, quit ? $"{p.Name} quit the match." : $"{p.Name} finished with {score:N0}.");
		ServerEndMatchIfDone();
		MarkDirty();
	}

	/// <summary>Zips the selected beatmap's folder and streams it to <paramref name="peer"/>. Works on any player that has it.</summary>
	private void SendBeatmapTo(long peer)
	{
		var s = State;
		int index = s?.Beatmap == null ? -1 : FindLocalBeatmap(s.Beatmap);
		if (index < 0) index = FindLocalBeatmap(LocalPlayer?.Difficulty); // same set, so the folder has it too
		if (index < 0)
		{
			SendUnavailable(peer, "That beatmap couldn't be found to share.");
			return;
		}
		if (_outgoing.Any(t => t.Peer == peer)) return;

		var b = SettingsOperator.Beatmaps[index];
		var folder = b.Path.TrimEnd('/', '\\');
		var checksum = s.Beatmap.Checksum;
		var safeFolder = string.Concat(Path.GetFileName(folder).Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch));
		var fileName = $"lan_{checksum[..8]}_{safeFolder}.osz";
		var zipPath = Path.Combine(SettingsOperator.tempdir, fileName);

		long maxBytes = IsOnline ? MaxOnlineShareBytes : MaxShareBytes;
		// Zipping can take a moment for big maps, so do it off the main thread.
		Task.Run(() =>
		{
			try
			{
				if (!File.Exists(zipPath))
				{
					var tmp = zipPath + ".part";
					if (File.Exists(tmp)) File.Delete(tmp);
					System.IO.Compression.ZipFile.CreateFromDirectory(folder, tmp, System.IO.Compression.CompressionLevel.Fastest, includeBaseDirectory: false);
					File.Move(tmp, zipPath, overwrite: true);
				}
				var info = new FileInfo(zipPath);
				if (info.Length > maxBytes) return (byte[])null;
				return File.ReadAllBytes(zipPath);
			}
			catch (Exception e)
			{
				GD.PrintErr($"[LAN] Couldn't package beatmap: {e.Message}");
				return null;
			}
		}).ContinueWith(t => Callable.From(() => QueueTransfer(peer, fileName, t.Result)).CallDeferred());
	}

	private void QueueTransfer(long peer, string fileName, byte[] data)
	{
		if (State == null || FindPlayer(peer) == null) return;
		if (data == null)
		{
			SendUnavailable(peer, "That beatmap couldn't be packaged for sharing.");
			return;
		}
		_outgoing.Add(new OutgoingTransfer
		{
			Peer = peer, FileName = fileName, Data = data,
			Total = Math.Max(1, (data.Length + ChunkSize - 1) / ChunkSize),
		});
		GD.Print($"[LAN] Sending {fileName} ({data.Length / 1024} KB) to peer {peer}");
	}

	private void PumpOutgoingTransfers()
	{
		// A few chunks per frame keeps the host smooth even while it's playing.
		for (int i = _outgoing.Count - 1; i >= 0; i--)
		{
			var t = _outgoing[i];
			if (State == null || FindPlayer(t.Peer) == null) { _outgoing.RemoveAt(i); continue; }
			for (int n = 0; n < 4 && t.Next < t.Total; n++, t.Next++)
			{
				int offset = t.Next * ChunkSize;
				int len = Math.Min(ChunkSize, t.Data.Length - offset);
				var chunk = new byte[len];
				Buffer.BlockCopy(t.Data, offset, chunk, 0, len);
				RpcId(t.Peer, MethodName.RpcBeatmapChunk, t.FileName, t.Next, t.Total, chunk);
			}
			if (t.Next >= t.Total) _outgoing.RemoveAt(i);
		}
	}

	private void BroadcastState()
	{
		_dirty = false;
		StateVersion++;
		TimeLeftNow = State.TimeLeft;
		if (Multiplayer.GetPeers().Length > 0)
			Rpc(MethodName.RpcState, JsonSerializer.Serialize(State));
		UpdateMatchLeaderboard();
	}

	private void MarkDirty() => _dirty = true;

	// =====================================================================
	// Client side
	// =====================================================================

	private void OnStartMatch(int matchId)
	{
		var me = LocalPlayer;
		if (me == null || !me.InMatch) return; // not ready: watch the live scores from the lobby
		int index = FindLocalBeatmap(ChartFor(me));
		if (index < 0)
		{
			LocalInMatch = true;
			ReportFinished(quit: true);
			return;
		}

		LocalInMatch = true;
		_scoreTimer = 0;
		// Everyone plays their own loadout, limited to mods that keep scores fair.
		ModsOperator.Reset();
		foreach (var mod in me.Mods)
			if (IsModAllowed(mod) && ModsOperator.Mods.ContainsKey(mod)) ModsOperator.Mods[mod] = true;
		ApplyGlobalMods(State.CurrentMods, State.CurrentSpeed); // DT / HT from the queue item, same speed for everyone
		SettingsOperator.Marathon = false;
		SettingsOperator.SpectatorMode = false;
		if (SettingsOperator.SessionConfig.SongID != index) SelectSong(index);
		SettingsOperator.SessionConfig.GameMode = CleanGameMode(State.CurrentGameMode); // everyone plays the item's mode
		BuildMatchLeaderboard();
		SettingsOperator.loopaudio = false;
		Sample.PlaySample("res://SelectableSkins/Slia/Sounds/play.wav");
		GetNode<SceneTransition>("/root/Transition").Switch("res://Panels/Screens/SongLoadingScreen.tscn");
	}

	private void ApplyState(LanRoomState newState)
	{
		var old = State;
		State = newState;
		StateVersion++;
		TimeLeftNow = newState.TimeLeft;

		if (_joining)
		{
			_joining = false;
			ChatLog.Clear();
			AddChatLine(null, $"Joined {newState.Name}. Say hi!");
			GetNode<SceneTransition>("/root/Transition").Switch(LobbyScene);
		}

		var oldChart = old?.Players.FirstOrDefault(p => p.Id == LocalId) is { } oldMe ? oldMe.Difficulty ?? old.Beatmap : old?.Beatmap;
		if (oldChart?.Checksum != LocalChart?.Checksum)
			RecheckLocalMap(force: true);

		if (old != null && old.HostId != newState.HostId && newState.HostId == LocalId)
			Notify.Post("You're the host now!");

		UpdateMatchLeaderboard();
	}

	/// <summary>Works out whether we have the selected beatmap and tells the server.</summary>
	private void RecheckLocalMap(bool force = false)
	{
		var chart = LocalChart;
		if (chart == null) return;
		int index = FindLocalBeatmap(chart);
		bool has = index >= 0;
		// Preview music + background follow what's up next, but only in the lobby (not while browsing song select).
		if (has && State.Phase == LanPhase.Lobby && !SettingsOperator.inGameplay && SettingsOperator.SessionConfig.SongID != index
			&& GetTree().CurrentScene?.SceneFilePath == LobbyScene)
			SelectSong(index);
		var me = LocalPlayer;
		if (!force && me != null && me.HasMap == has) return;
		if (IsServer) ServerSetHasMap(1, has, chart.Checksum);
		else RpcId(1, MethodName.RpcSetHasMap, has, chart.Checksum);
	}

	/// <summary>Index into SettingsOperator.Beatmaps of the exact same chart, or -1.</summary>
	public int FindLocalBeatmap(LanBeatmapRef r)
	{
		if (r == null) return -1;
		if (LanSelfTest.ForceMissingMap) return -1;
		var maps = SettingsOperator.Beatmaps;
		for (int i = 0; i < maps.Count; i++)
		{
			var b = maps[i];
			bool candidate = (r.BeatmapID > 0 && b.BeatmapID == r.BeatmapID)
				|| (b.Title == r.Title && b.Artist == r.Artist && b.Version == r.Version);
			if (candidate && Checksum(b.Rawurl) == r.Checksum) return i;
		}
		return -1;
	}

	private string Checksum(string path)
	{
		if (string.IsNullOrEmpty(path)) return null;
		if (_checksumCache.TryGetValue(path, out var hash)) return hash;
		hash = ChecksumUtil.GetSha256(path);
		if (hash != null) _checksumCache[path] = hash;
		return hash;
	}

	// ---- Live in-game leaderboard (reuses the gameplay leaderboard panel) ----

	private readonly Dictionary<long, LeaderboardEntry> _matchEntries = new();

	private void BuildMatchLeaderboard()
	{
		_matchEntries.Clear();
		ApiOperator.LeaderboardList.Clear();
		foreach (var p in State.Players.Where(p => p.InMatch && p.Id != LocalId))
		{
			var e = new LeaderboardEntry { username = p.Name, Accuracy = 1f, mods = "", time = DateTimeOffset.UtcNow.ToUnixTimeSeconds() };
			_matchEntries[p.Id] = e;
			ApiOperator.LeaderboardList.Add(e);
		}
	}

	private void UpdateMatchLeaderboard()
	{
		if (!LocalInMatch || State == null) return;
		foreach (var p in State.Players)
		{
			if (!_matchEntries.TryGetValue(p.Id, out var e)) continue;
			e.score = p.Score;
			e.combo = p.Combo;
			e.Accuracy = p.Accuracy;
		}
	}

	// =====================================================================
	// Connection events
	// =====================================================================

	private void OnPeerConnected(long id)
	{
		if (IsServer) GD.Print($"[LAN] Peer {id} connected, waiting for hello");
	}

	private void OnPeerDisconnected(long id)
	{
		if (!IsServer || State == null) return;
		var p = FindPlayer(id);
		if (p == null) return;
		State.Players.Remove(p);
		_outgoing.RemoveAll(t => t.Peer == id);
		ServerChat(0, $"{p.Name} left the room.");
		int removed = State.Queue.RemoveAll(q => q.OwnerId == id);
		if (removed > 0) ServerQueueChanged(); // their picks leave with them
		// The host left: the controls go back to whoever runs the room.
		if (id == State.HostId && FindPlayer(1) is { } server)
			SetHost(server, $"{server.Name} is the host again.");
		ServerEndMatchIfDone();
		MarkDirty();
	}

	private void OnConnectedToServer()
	{
		RpcId(1, MethodName.RpcHello, LocalName(), Protocol, GameVersion());
	}

	private void OnConnectionFailed()
	{
		Notify.Post(Relay?.CloseReason ?? "Couldn't connect to that room.", Type: NotificationIcons.NotificationType.Warning);
		Multiplayer.MultiplayerPeer = new OfflineMultiplayerPeer();
		ResetSession();
	}

	private void OnServerDisconnected()
	{
		bool wasInRoom = State != null;
		var reason = Relay?.CloseReason;
		Multiplayer.MultiplayerPeer = new OfflineMultiplayerPeer();
		ResetSession();
		if (!wasInRoom) return;
		Notify.Post(reason ?? "The host closed the room.", Type: NotificationIcons.NotificationType.Warning);
		// If we're mid-song, let the player finish; the end screen sends them back to the lounge.
		if (!SettingsOperator.inGameplay && GetTree().CurrentScene?.SceneFilePath == LobbyScene)
			GetNode<SceneTransition>("/root/Transition").Switch(LoungeScene);
	}

	private void ResetSession()
	{
		State = null;
		IsServer = false;
		IsOnline = false;
		_listingSent = null;
		LocalInMatch = false;
		_joining = false;
		_dirty = false;
		_outgoing.Clear();
		_incoming = null;
		TransferProgress = -1;
		Feeds.Clear();
		_specCharts.Clear();
		_specOut.Clear();
		if (_matchEntries.Count > 0) ApiOperator.LeaderboardList.Clear();
		_matchEntries.Clear();
		StateVersion++;
	}

	// =====================================================================
	// RPCs  (client -> host: AnyPeer, validated on the host;  host -> clients: Authority)
	// =====================================================================

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcHello(string name, int protocol, string version)
	{
		if (!IsServer || State == null) return;
		long id = Multiplayer.GetRemoteSenderId();
		if (protocol != Protocol)
		{
			RpcId(id, MethodName.RpcKicked, $"Version mismatch: the host is running Qlute {GameVersion()}.");
			GetTree().CreateTimer(0.5).Timeout += () => Multiplayer.MultiplayerPeer?.DisconnectPeer((int)id);
			return;
		}
		if (FindPlayer(id) != null) return;

		// Online, the server tells us who really connected, so nobody can pose as someone else.
		if (IsOnline && Relay?.PeerUsers.TryGetValue((int)id, out var verified) == true) name = verified;
		var baseName = Clean(name, 32, "Guest");
		var unique = baseName;
		for (int n = 2; State.Players.Any(p => p.Name == unique); n++) unique = $"{baseName} ({n})";

		State.Players.Add(new LanPlayer { Id = id, Name = unique });
		ServerChat(0, $"{unique} joined the room.");
		MarkDirty();
		BroadcastState(); // send the newcomer the room right away
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcSetReady(bool ready)
	{
		if (IsServer && State != null) ServerSetReady(Multiplayer.GetRemoteSenderId(), ready);
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcSetHasMap(bool has, string checksum)
	{
		if (IsServer && State != null) ServerSetHasMap(Multiplayer.GetRemoteSenderId(), has, checksum);
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcChat(string text)
	{
		if (IsServer && State != null) ServerChat(Multiplayer.GetRemoteSenderId(), text);
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered)]
	private void RpcScore(int score, int combo, int maxCombo, float acc, int max, int great, int meh, int bad)
	{
		if (IsServer && State != null) ServerScore(Multiplayer.GetRemoteSenderId(), score, combo, maxCombo, acc, max, great, meh, bad);
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcFinished(int score, int maxCombo, float acc, int max, int great, int meh, int bad, bool quit)
	{
		if (IsServer && State != null) ServerFinished(Multiplayer.GetRemoteSenderId(), score, maxCombo, acc, max, great, meh, bad, quit);
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcRequestBeatmap()
	{
		if (IsServer && State != null) ServerRouteBeatmapRequest(Multiplayer.GetRemoteSenderId());
	}

	// ---- Host controls: any peer may call these; the server checks the sender is the host ----

	// ---- Queue + loadout: any player may call these ----

	private static LanBeatmapRef ParseMap(string json)
	{
		try { return JsonSerializer.Deserialize<LanBeatmapRef>(json); }
		catch { return null; }
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcAddToQueue(string json, string[] mods, float speed, int mode)
	{
		if (IsServer && State != null) ServerAddToQueue(Multiplayer.GetRemoteSenderId(), ParseMap(json), mods, speed, mode);
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcReplaceQueueItem(int itemId, string json, string[] mods, float speed, int mode)
	{
		if (IsServer && State != null) ServerReplaceQueueItem(Multiplayer.GetRemoteSenderId(), itemId, ParseMap(json), mods, speed, mode);
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcRemoveQueueItem(int itemId)
	{
		if (IsServer && State != null) ServerRemoveQueueItem(Multiplayer.GetRemoteSenderId(), itemId);
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcSetDifficulty(string json)
	{
		if (IsServer && State != null) ServerSetDifficulty(Multiplayer.GetRemoteSenderId(), json ?? "");
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcSetMods(string[] mods)
	{
		if (IsServer && State != null) ServerSetMods(Multiplayer.GetRemoteSenderId(), mods);
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcSetWatchTarget(long target)
	{
		if (IsServer && State != null) ServerSetWatchTarget(Multiplayer.GetRemoteSenderId(), target);
	}

	// Live play goes reliably on its own channel so a big chart never holds up chat / room state.
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable, TransferChannel = 2)]
	private void RpcSpecChart(byte[] chart)
	{
		if (IsServer && State != null) ServerStoreChart(Multiplayer.GetRemoteSenderId(), chart);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable, TransferChannel = 2)]
	private void RpcSpecChartFrom(long from, byte[] chart)
	{
		if (State != null) OnSpecChart(from, chart);
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable, TransferChannel = 2)]
	private void RpcSpecTick(byte[] packet)
	{
		if (IsServer && State != null) ServerRelaySpec(Multiplayer.GetRemoteSenderId(), packet);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable, TransferChannel = 2)]
	private void RpcSpecTickFrom(long from, byte[] packet)
	{
		if (State != null && LocalPlayer?.WatchTarget >= 0) OnSpecTick(from, packet);
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcSetActivity(int activity)
	{
		if (IsServer && State != null) ServerSetActivity(Multiplayer.GetRemoteSenderId(), activity);
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcAdjustTimer(int deltaSeconds)
	{
		if (IsServer && State != null) ServerAdjustTimer(Multiplayer.GetRemoteSenderId(), deltaSeconds);
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcStartNow()
	{
		if (IsServer && State != null) ServerStartNow(Multiplayer.GetRemoteSenderId());
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcRename(string name)
	{
		if (IsServer && State != null) ServerRename(Multiplayer.GetRemoteSenderId(), name);
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcTransferHost(long target)
	{
		if (IsServer && State != null) ServerTransferHost(Multiplayer.GetRemoteSenderId(), target);
	}

	/// <summary>Server -> a player who has the map: please send it to <paramref name="target"/>.</summary>
	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcSendBeatmapTo(long target)
	{
		if (State != null && !IsServer) SendBeatmapTo(target);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcState(string json)
	{
		try
		{
			var s = JsonSerializer.Deserialize<LanRoomState>(json);
			if (s != null) ApplyState(s);
		}
		catch (Exception e)
		{
			GD.PrintErr($"[LAN] Bad room state from host: {e.Message}");
		}
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcChatLine(string name, string text) => AddChatLine(string.IsNullOrEmpty(name) ? null : name, text);

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcStartMatch(int matchId) => OnStartMatch(matchId);

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcKicked(string reason)
	{
		Notify.Post(reason, Type: NotificationIcons.NotificationType.Warning);
	}

	// Beatmaps can come from the server or (relayed through it) from another player, so these are AnyPeer;
	// we only accept them while we're waiting for a transfer, and only from someone in the room.
	private bool AcceptTransferFrom(long sender) =>
		TransferProgress >= 0 && State != null && (sender == 1 || FindPlayer(sender) != null);

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcBeatmapUnavailable(string reason)
	{
		if (AcceptTransferFrom(Multiplayer.GetRemoteSenderId())) OnBeatmapUnavailable(reason);
	}

	// Separate channel so a big transfer doesn't hold up chat/state updates.
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable, TransferChannel = 1)]
	private void RpcBeatmapChunk(string fileName, int index, int total, byte[] data)
	{
		long sender = Multiplayer.GetRemoteSenderId();
		if (!AcceptTransferFrom(sender)) return;
		if (index == 0) { _incoming = new MemoryStream(); _incomingFrom = sender; }
		if (_incoming == null || sender != _incomingFrom) return;
		_incoming.Write(data, 0, data.Length);
		TransferProgress = (index + 1) / (float)total;
		if (index < total - 1) return;

		var bytes = _incoming.ToArray();
		_incoming = null;
		TransferProgress = -1;
		var safeName = Path.GetFileName(fileName);
		if (!safeName.EndsWith(".osz")) return;
		try
		{
			var target = LanSelfTest.TransferOutputDir ?? SettingsOperator.downloadsdir;
			var tmp = Path.Combine(SettingsOperator.tempdir, safeName + ".part");
			File.WriteAllBytes(tmp, bytes);
			File.Move(tmp, Path.Combine(target, safeName), overwrite: true);
			GD.Print($"[LAN] Received {safeName} ({bytes.Length / 1024} KB)");
			Notify.Post("Got the beatmap, importing...");
			// BeatmapListener imports *.osz from the downloads folder; RecheckLocalMap runs once it shows up.
		}
		catch (Exception e)
		{
			GD.PrintErr($"[LAN] Couldn't save beatmap: {e.Message}");
			Notify.Post("Couldn't save the beatmap from the host.", Type: NotificationIcons.NotificationType.Warning);
		}
	}

	// =====================================================================
	// Live spectating: the player sends their chart once, then song time + inputs + judgements
	// =====================================================================

	/// <summary>Gameplay calls this once the chart is loaded (LAN matches only).</summary>
	public void SpecSendChart(List<NotesEn> notes, int viewHeight)
	{
		if (notes == null) return;
		SpecSendChart(new LanSpecChart
		{
			Mode = 0,
			Height = viewHeight,
			Times = notes.Select(n => -n.timing).ToArray(), // NotesEn.timing is stored negative
			Lanes = notes.Select(n => (byte)Math.Clamp(n.NotePart, 0, 3)).ToArray(),
		});
	}

	/// <summary>Any game mode: send the chart spectators draw (LAN matches only).</summary>
	public void SpecSendChart(LanSpecChart chart)
	{
		if (!LocalInMatch || State == null || chart == null) return;
		_specOut.Clear();
		_specTimer = 0;
		var blob = chart.Encode();
		if (IsServer) ServerStoreChart(1, blob);
		else RpcId(1, MethodName.RpcSpecChart, blob);
	}

	/// <summary>Tap!: the cursor position (playfield units) at <paramref name="songMs"/>.</summary>
	public void SpecCursor(float songMs, Vector2 pos)
	{
		if (LocalInMatch) _specOut.Cursor(songMs, pos);
	}

	private float _specSongMs;

	/// <summary>Gameplay: its song clock this frame (same clock as the key / judge times).</summary>
	public void SpecTime(float songMs) => _specSongMs = songMs;

	/// <summary>Gameplay: a key went down / up at <paramref name="songMs"/>.</summary>
	public void SpecKey(int lane, bool down, float songMs)
	{
		if (LocalInMatch) _specOut.Key(songMs, lane, down);
	}

	/// <summary>Gameplay: note <paramref name="index"/> was judged (0 perfect, 1 great, 2 meh, 3 miss).</summary>
	public void SpecJudge(int index, int judge, float songMs)
	{
		if (LocalInMatch) _specOut.Judge(songMs, index, judge);
	}

	private void OnSpecChart(long from, byte[] chart)
	{
		var c = LanSpecChart.Decode(chart, MaxChartNotes);
		if (c == null) return;
		if (!Feeds.TryGetValue(from, out var f)) Feeds[from] = f = new LanSpectateFeed();
		f.SetChart(c);
	}

	private void OnSpecTick(long from, byte[] packet)
	{
		if (!Feeds.TryGetValue(from, out var f)) Feeds[from] = f = new LanSpectateFeed();
		f.Apply(packet);
	}

	/// <summary>True when someone in the room is watching the local player.</summary>
	private bool IsWatched() => State.Players.Any(v => v.Id != LocalId && (v.WatchTarget == 0 || v.WatchTarget == LocalId));

	private void PumpSpectateFeed(double delta)
	{
		if (!LocalInMatch || !SettingsOperator.inGameplay || LocalPlayer?.Finished != false) return;
		_specTimer -= delta;
		if (_specTimer > 0) return;
		_specTimer = SpectateTickSeconds;
		if (!IsWatched()) { _specOut.Clear(); return; } // nobody watching: don't send anything
		var packet = _specOut.Flush(_specSongMs, SettingsOperator.Gameplaycfg.Combo,
			SettingsOperator.Gameplaycfg.Score, LocalAccuracy());
		if (IsServer) ServerRelaySpec(1, packet);
		else RpcId(1, MethodName.RpcSpecTick, packet);
	}

	// =====================================================================
	// Online rooms (lounge list + our listing)
	// =====================================================================

	/// <summary>Rooms on the online server, refreshed while the lounge is open.</summary>
	public readonly List<OnlineRoomInfo> OnlineRooms = new();
	public int OnlineRoomsVersion { get; private set; }
	/// <summary>What the lounge shows under "Online" when there's no list (not logged in, offline...).</summary>
	public string OnlineStatus { get; private set; } = "";
	private WebSocketPeer _browser;
	private double _browseTimer, _browseRetry;
	private bool _browsing;
	private string _listingSent;
	private double _listingTimer;

	public void StartOnlineBrowse()
	{
		_browsing = true;
		_browseRetry = 0;
		OpenBrowser();
	}

	public void StopOnlineBrowse()
	{
		_browsing = false;
		_browser?.Close();
		_browser = null;
		OnlineRooms.Clear();
		OnlineRoomsVersion++;
	}

	public void RefreshOnlineRooms() => _browseTimer = 0;

	private void OpenBrowser()
	{
		_browser = null;
		if (!OnlineApi.Available) { SetOnlineStatus("Log in to see online rooms."); return; }
		_browser = OnlineApi.Connect("multiplayer", out var err);
		if (err != Error.Ok) { _browser = null; SetOnlineStatus($"Couldn't reach the server ({err})."); return; }
		SetOnlineStatus("Looking for online rooms...");
		_browseTimer = 0;
	}

	private void SetOnlineStatus(string text)
	{
		if (OnlineStatus == text) return;
		OnlineStatus = text;
		OnlineRoomsVersion++;
	}

	private void PollOnlineBrowser(double delta)
	{
		if (!_browsing) return;
		if (_browser == null)
		{
			// Retry every few seconds (e.g. after logging in while the lounge is open).
			_browseRetry -= delta;
			if (_browseRetry <= 0) { _browseRetry = 5; OpenBrowser(); }
			return;
		}
		_browser.Poll();
		var state = _browser.GetReadyState();
		if (state == WebSocketPeer.State.Closed)
		{
			bool authFailed = _browser.GetCloseCode() == OnlineApi.AuthFailedCode;
			SetOnlineStatus(authFailed ? OnlineApi.CloseReason(_browser, "The server refused your login.") : "Can't reach the online server right now.");
			_browser = null;
			_browseRetry = authFailed ? 60 : 5;
			if (OnlineRooms.Count > 0) { OnlineRooms.Clear(); OnlineRoomsVersion++; }
			return;
		}
		if (state != WebSocketPeer.State.Open) return;

		while (_browser.GetAvailablePacketCount() > 0)
		{
			var msg = OnlineApi.ParseJson(_browser.GetPacket());
			var op = OnlineApi.Str(msg, "op");
			if (op == "error" && OnlineApi.Bool(msg, "fatal")) { SetOnlineStatus(OnlineApi.Str(msg, "text") ?? "The server refused your login."); continue; }
			if (op != "rooms" || msg["rooms"] is not System.Text.Json.Nodes.JsonArray arr) continue;
			OnlineRooms.Clear();
			foreach (var r in arr.OfType<System.Text.Json.Nodes.JsonObject>())
			{
				OnlineRooms.Add(new OnlineRoomInfo
				{
					Id = OnlineApi.Long(r, "id"),
					Name = OnlineApi.Str(r, "name") ?? "Room",
					Host = OnlineApi.Str(r, "host") ?? "?",
					Players = (int)OnlineApi.Long(r, "players"),
					Phase = (int)OnlineApi.Long(r, "phase"),
					Map = OnlineApi.Str(r, "map") ?? "",
					Proto = (int)OnlineApi.Long(r, "proto"),
					Version = OnlineApi.Str(r, "ver") ?? "",
				});
			}
			SetOnlineStatus(OnlineRooms.Count == 0 ? "No online rooms right now. Make one!" : "");
			OnlineRoomsVersion++;
		}
		_browseTimer -= delta;
		if (_browseTimer <= 0)
		{
			_browseTimer = 3;
			OnlineApi.SendJson(_browser, new System.Text.Json.Nodes.JsonObject { ["op"] = "list" });
		}
	}

	/// <summary>Host: keep our room's listing current (sends on change, and every 10 s as a heartbeat).</summary>
	private void UpdateOnlineListing(double delta)
	{
		var relay = Relay;
		if (relay == null || relay.RoomId == 0) return;
		var map = State.Beatmap?.Display ?? "";
		var key = $"{State.Name}|{State.Players.Count}|{(int)State.Phase}|{map}";
		_listingTimer -= delta;
		if (key == _listingSent && _listingTimer > 0) return;
		_listingSent = key;
		_listingTimer = 10;
		relay.UpdateListing(State.Name, State.Players.Count, (int)State.Phase, map);
	}

	/// <summary>Our online room lost the server: it's gone for everyone, so tell the player and go back.</summary>
	private void LostOnlineHost(string reason)
	{
		Leave(silent: true);
		Notify.Post($"Your online room closed: {reason}", Type: NotificationIcons.NotificationType.Warning);
		if (!SettingsOperator.inGameplay && GetTree().CurrentScene?.SceneFilePath == LobbyScene)
			GetNode<SceneTransition>("/root/Transition").Switch(LoungeScene);
	}

	// =====================================================================
	// Discovery internals	// =====================================================================
	// Discovery internals
	// =====================================================================

	private void SendBeacon(double delta)
	{
		_beaconTimer -= delta;
		if (_beaconTimer > 0) return;
		_beaconTimer = 1.0;

		var beacon = new LanBeacon
		{
			ver = GameVersion(),
			name = State.Name,
			host = State.HostName,
			port = GamePort,
			players = State.Players.Count,
			phase = (int)State.Phase,
			map = State.Beatmap?.Display,
		};
		var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(beacon));
		try
		{
			_beaconTx ??= new UdpClient { EnableBroadcast = true };
			foreach (var addr in BroadcastAddresses())
			{
				try { _beaconTx.Send(bytes, bytes.Length, new IPEndPoint(addr, DiscoveryPort)); }
				catch (SocketException) { /* interface may have gone away */ }
			}
		}
		catch (Exception e)
		{
			GD.PrintErr($"[LAN] Beacon failed: {e.Message}");
		}
	}

	private List<IPAddress> BroadcastAddresses()
	{
		_broadcastAddrsAge += 1;
		if (_broadcastAddrs != null && _broadcastAddrsAge < 10) return _broadcastAddrs;
		_broadcastAddrsAge = 0;

		var list = new List<IPAddress> { IPAddress.Broadcast };
		try
		{
			foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
			{
				if (ni.OperationalStatus != OperationalStatus.Up || ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
				foreach (var ua in ni.GetIPProperties().UnicastAddresses)
				{
					if (ua.Address.AddressFamily != AddressFamily.InterNetwork || ua.IPv4Mask == null) continue;
					var ip = ua.Address.GetAddressBytes();
					var mask = ua.IPv4Mask.GetAddressBytes();
					var bc = new byte[4];
					for (int i = 0; i < 4; i++) bc[i] = (byte)(ip[i] | ~mask[i]);
					var addr = new IPAddress(bc);
					if (!list.Contains(addr)) list.Add(addr);
				}
			}
		}
		catch (Exception e)
		{
			GD.PrintErr($"[LAN] Couldn't list network interfaces: {e.Message}");
		}
		_broadcastAddrs = list;
		return list;
	}

	private void PollDiscovery()
	{
		if (_discoveryRx == null) return;
		ulong now = Time.GetTicksMsec();
		try
		{
			while (_discoveryRx.Available > 0)
			{
				IPEndPoint from = null;
				var data = _discoveryRx.Receive(ref from);
				LanBeacon beacon;
				try { beacon = JsonSerializer.Deserialize<LanBeacon>(data); }
				catch { continue; }
				if (beacon?.q != BeaconTag || beacon.port <= 0) continue;

				var key = $"{from.Address}:{beacon.port}";
				bool changed = !Rooms.TryGetValue(key, out var old)
					|| JsonSerializer.Serialize(old.Beacon) != JsonSerializer.Serialize(beacon);
				Rooms[key] = new LanRoomInfo { Address = from.Address.ToString(), Port = beacon.port, Beacon = beacon, LastSeenMs = now };
				if (changed) RoomsVersion++;
			}
		}
		catch (Exception e)
		{
			GD.PrintErr($"[LAN] Discovery error: {e.Message}");
		}

		// Rooms that stopped broadcasting are gone.
		var stale = Rooms.Where(r => now - r.Value.LastSeenMs > 4000).Select(r => r.Key).ToList();
		foreach (var k in stale) Rooms.Remove(k);
		if (stale.Count > 0) RoomsVersion++;
	}

	// =====================================================================
	// Helpers
	// =====================================================================

	/// <summary>Gameplaycfg.Accuracy is 100 before the first judgement and NaN for 0/0; send a clean 0..1 value.</summary>
	private static float LocalAccuracy()
	{
		var g = SettingsOperator.Gameplaycfg.Max + SettingsOperator.Gameplaycfg.Great + SettingsOperator.Gameplaycfg.Meh + SettingsOperator.Gameplaycfg.Bad;
		var a = SettingsOperator.Gameplaycfg.Accuracy;
		if (g == 0 || !float.IsFinite(a)) return 1f;
		return Math.Clamp(a, 0f, 1f);
	}

	/// <summary>Describes a library beatmap for the room (null if its file can't be read).</summary>
	private LanBeatmapRef BuildRef(int index)
	{
		if (index < 0 || index >= SettingsOperator.Beatmaps.Count) return null;
		var b = SettingsOperator.Beatmaps[index];
		var checksum = Checksum(b.Rawurl);
		if (checksum == null) { Notify.Post("Couldn't read that beatmap file.", Type: NotificationIcons.NotificationType.Warning); return null; }
		return new LanBeatmapRef
		{
			BeatmapID = b.BeatmapID,
			BeatmapSetID = b.BeatmapSetID,
			Title = b.Title,
			Artist = b.Artist,
			Version = b.Version,
			Mapper = b.Mapper,
			Checksum = checksum,
			Levelrating = b.Levelrating,
			LengthMs = b.Timetotal,
		};
	}

	/// <summary>The DT / HT the local player has on in song select, with its speed.</summary>
	private static (List<string>, float) LocalGlobalMods()
	{
		if (ModsOperator.Mods["dt"]) return (new List<string> { "dt" }, DtCustom.Speed);
		if (ModsOperator.Mods["ht"]) return (new List<string> { "ht" }, HtCustom.Speed);
		return (new List<string>(), 1f);
	}

	/// <summary>Only modes that have a gameplay scene (0 Mania, 1 Taiko, 3 Tap!); anything else plays Mania.</summary>
	public static int CleanGameMode(int mode) => mode is 0 or 1 or 3 ? mode : 0;

	public static string GameModeName(int mode) => CleanGameMode(mode) switch { 1 => "Taiko", 3 => "Tap!", _ => "Mania" };

	private static (List<string>, float) CleanGlobalMods(string[] mods, float speed)
	{
		var list = mods?.Where(GlobalMods.Contains).Distinct().ToList() ?? new List<string>();
		if (list.Contains("dt")) return (new List<string> { "dt" }, float.IsFinite(speed) ? Math.Clamp(speed, MinDtSpeed, MaxDtSpeed) : DtCustom.DTSpeed);
		if (list.Contains("ht")) return (new List<string> { "ht" }, float.IsFinite(speed) ? Math.Clamp(speed, MinHtSpeed, MaxHtSpeed) : HtCustom.HTSpeed);
		return (new List<string>(), 1f);
	}

	/// <summary>Turns on room-wide DT / HT locally at the given speed (keeps the other mods as they are).</summary>
	private static void ApplyGlobalMods(List<string> mods, float speed)
	{
		ModsOperator.Mods["dt"] = mods.Contains("dt");
		ModsOperator.Mods["ht"] = !ModsOperator.Mods["dt"] && mods.Contains("ht");
		if (ModsOperator.Mods["dt"]) DtCustom.Speed = speed;
		if (ModsOperator.Mods["ht"]) HtCustom.Speed = speed;
		ModsOperator.Refresh(); // sets the default DT / HT pitch...
		if (ModsOperator.Mods["dt"] || ModsOperator.Mods["ht"]) AudioPlayer.Instance.PitchScale = speed; // ...then the item's speed
		ModsOperator.GetModAlias();
	}

	private static List<string> LocalAllowedMods()
	{
		var mods = ModsOperator.Mods.Where(m => m.Value && IsModAllowed(m.Key)).Select(m => m.Key).ToList();
		if (mods.Contains("black-out")) mods.Remove("slice");
		return mods;
	}

	private void SelectSong(int index) => GetNode<SettingsOperator>("/root/SettingsOperator").SelectSongID(index);

	private LanPlayer FindPlayer(long id) => State?.Players.FirstOrDefault(p => p.Id == id);

	private void AddChatLine(string name, string text)
	{
		ChatLog.Add(name == null ? $"[color=#7fc8ff]* {Escape(text)}[/color]" : $"[color=#ffda75]{Escape(name)}[/color]: {Escape(text)}");
		if (ChatLog.Count > MaxChatLines) ChatLog.RemoveRange(0, ChatLog.Count - MaxChatLines);
		ChatVersion++;
		LanSelfTest.Log($"chat: {(name == null ? "*" : name)}: {text}");
	}

	private static string Escape(string s) => s.Replace("[", "[lb]");

	private static string Clean(string s, int maxLen, string fallback)
	{
		if (string.IsNullOrWhiteSpace(s)) return fallback;
		s = new string(s.Where(c => !char.IsControl(c)).ToArray()).Trim();
		if (s.Length == 0) return fallback;
		return s.Length > maxLen ? s[..maxLen] : s;
	}
}
