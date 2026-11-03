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
/// - Flow: host picks a beatmap -> "time till play" countdown runs -> drops to 5s once everyone is ready ->
///   ready players load in -> live scores stream to the host -> results when everyone finished -> back to lobby.
/// - "Server" vs "host": the player who created the room is the server (peer 1) for its whole life. The *host*
///   is whoever holds the room controls (<see cref="LanRoomState.HostId"/>) and can be handed to anyone;
///   their requests go through the server like everything else.
/// </summary>
public partial class LanNetwork : Node
{
	public const string BeaconTag = "qlute-lan";
	public const int Protocol = 2;
	public const int MaxRoomNameLength = 40;
	public const int DiscoveryPort = 8911;
	public const int DefaultGamePort = 8912;
	public const int MaxPeers = 4095; // ENet's hard limit; rooms are otherwise unlimited.
	public const double AllReadyCountdown = 5;
	public const int MinLobbyTimer = 15, MaxLobbyTimer = 600;
	public const string LobbyScene = "res://Panels/Screens/MultiplayerLobby.tscn";
	public const string LoungeScene = "res://Panels/Screens/MultiplayerList.tscn";
	private const int ChunkSize = 60 * 1024;
	private const long MaxShareBytes = 200L * 1024 * 1024;
	private const int MaxChatLines = 200;

	public static LanNetwork Instance { get; private set; }
	/// <summary>True while the local player is playing a LAN match.</summary>
	public static bool InMatch => Instance != null && Instance.LocalInMatch;

	// ---- Session state (read by the UI; mutate only through the methods below) ----
	public LanRoomState State { get; private set; }
	/// <summary>True on the machine running the room (peer 1). Leaving closes the room for everyone.</summary>
	public bool IsServer { get; private set; }
	/// <summary>True when the local player holds the host controls.</summary>
	public bool IsHost => State != null && LocalId != 0 && State.HostId == LocalId;
	public bool LocalInMatch { get; private set; }
	public int StateVersion { get; private set; }
	public int GamePort { get; private set; }
	public long LocalId => Multiplayer.MultiplayerPeer is ENetMultiplayerPeer ? Multiplayer.GetUniqueId() : 0;
	public LanPlayer LocalPlayer => State?.Players.FirstOrDefault(p => p.Id == LocalId);
	public string ReturnScene => State != null ? LobbyScene : LoungeScene;
	/// <summary>Countdown smoothed locally between host updates.</summary>
	public double TimeLeftNow { get; private set; }

	public readonly List<string> ChatLog = new();
	public int ChatVersion { get; private set; }

	/// <summary>-1 when idle, otherwise 0..1 progress of a beatmap being received from the host.</summary>
	public float TransferProgress { get; private set; } = -1;

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
		_beaconTx?.Close();
		Leave(silent: true);
	}

	// =====================================================================
	// Public API used by the lounge / lobby / gameplay
	// =====================================================================

	public static string LocalName()
	{
		var name = ApiOperator.Username;
		return string.IsNullOrWhiteSpace(name) ? "Guest" : name;
	}

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
		IsServer = true;
		State = new LanRoomState
		{
			Name = Clean(roomName, MaxRoomNameLength, $"{LocalName()}'s room"),
			HostId = 1,
			HostName = LocalName(),
			Players = { new LanPlayer { Id = 1, Name = LocalName() } },
		};
		ChatLog.Clear();
		AddChatLine(null, $"Room created on port {port}. Pick a beatmap, then everyone readies up!");
		if (SettingsOperator.SessionConfig.SongID >= 0 && SettingsOperator.SessionConfig.SongID < SettingsOperator.Beatmaps.Count)
			HostSelectBeatmap(SettingsOperator.SessionConfig.SongID, announce: false);
		_beaconTimer = 0;
		MarkDirty();
		GD.Print($"[LAN] Hosting \"{State.Name}\" on port {port}");
		return true;
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

	/// <summary>Leave the current room (closes it for everyone if we're the host).</summary>
	public void Leave(bool silent = false)
	{
		if (Multiplayer.MultiplayerPeer is ENetMultiplayerPeer peer)
		{
			peer.Close();
			if (!silent) Notify.Post(IsServer ? "Closed the LAN room." : "Left the LAN room.");
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

	/// <summary>Host only: pick the beatmap everyone will play (index into the local library).</summary>
	public void HostSelectBeatmap(int index, bool announce = true)
	{
		if (!IsHost || State.Phase != LanPhase.Lobby) return;
		if (index < 0 || index >= SettingsOperator.Beatmaps.Count) return;
		var b = SettingsOperator.Beatmaps[index];
		var checksum = Checksum(b.Rawurl);
		if (checksum == null) { Notify.Post("Couldn't read that beatmap file.", Type: NotificationIcons.NotificationType.Warning); return; }
		if (State.Beatmap?.Checksum == checksum) return;

		var map = new LanBeatmapRef
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
		if (SettingsOperator.SessionConfig.SongID != index) SelectSong(index);
		if (IsServer) ServerSetBeatmap(1, map, announce);
		else RpcId(1, MethodName.RpcSelectBeatmap, JsonSerializer.Serialize(map));
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

		if (State == null) return;

		if (State.CountdownActive && State.Phase == LanPhase.Lobby)
			TimeLeftNow = Math.Max(0, TimeLeftNow - delta);

		if (IsServer)
		{
			ServerTick(delta);
			SendBeacon(delta);
		}
		PumpOutgoingTransfers(); // anyone holding the map may be asked to share it

		// Beatmap library changed (e.g. a map arrived from the host) -> re-check whether we can play.
		if (_lastBeatmapCount != SettingsOperator.Beatmaps.Count)
		{
			_lastBeatmapCount = SettingsOperator.Beatmaps.Count;
			RecheckLocalMap();
		}

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
		foreach (var p in s.Players)
		{
			p.InMatch = players.Contains(p);
			p.Ready = false;
			p.Finished = false;
			p.Quit = false;
			p.Score = p.Combo = p.MaxCombo = p.Max = p.Great = p.Meh = p.Bad = 0;
			p.Accuracy = 1f;
		}
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
		}).ToList();
		// Players who left mid-match aren't in s.Players anymore, so results only list who stayed.

		s.Phase = LanPhase.Lobby;
		s.CountdownActive = false;
		foreach (var p in s.Players) { p.InMatch = false; p.Ready = false; }

		ServerChat(0, "Match finished! Results:");
		foreach (var r in s.LastResults)
			ServerChat(0, r.Quit
				? $"#{r.Rank} {r.Name} - quit"
				: $"#{r.Rank} {r.Name} - {r.Score:N0} ({r.Accuracy:P2}, {r.MaxCombo}x)");
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
		if (checksum != State.Beatmap?.Checksum) return; // stale answer for a previous beatmap
		p.HasMap = has;
		if (!has) p.Ready = false;
		MarkDirty();
	}

	// ---- Host controls (validated against State.HostId, whoever sent them) ----

	private bool SentByHost(long sender) => State != null && sender == State.HostId && FindPlayer(sender) != null;

	private void ServerSetBeatmap(long sender, LanBeatmapRef map, bool announce)
	{
		if (!SentByHost(sender) || State.Phase != LanPhase.Lobby || string.IsNullOrEmpty(map?.Checksum)) return;
		if (State.Beatmap?.Checksum == map.Checksum) return;

		map.Title = Clean(map.Title, 200, "Unknown");
		map.Artist = Clean(map.Artist, 200, "Unknown");
		map.Version = Clean(map.Version, 100, "?");
		map.Mapper = Clean(map.Mapper, 100, "?");
		State.Beatmap = map;
		foreach (var p in State.Players)
		{
			p.Ready = false;
			p.HasMap = p.Id == sender; // everyone else re-reports once they've checked their library
		}
		State.CountdownActive = false; // restarts the "time till play" timer
		if (announce) ServerChat(0, $"Beatmap changed to {map.Display}");
		if (sender != 1) RecheckLocalMap(force: true); // the server player needs the map too
		MarkDirty();
	}

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
				if (info.Length > MaxShareBytes) return (byte[])null;
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
		int index = FindLocalBeatmap(State.Beatmap);
		if (index < 0)
		{
			LocalInMatch = true;
			ReportFinished(quit: true);
			return;
		}

		LocalInMatch = true;
		_scoreTimer = 0;
		ModsOperator.Reset();             // everyone plays nomod so scores are comparable
		ModsOperator.ModsEnabled.Clear();
		SettingsOperator.Marathon = false;
		SettingsOperator.SpectatorMode = false;
		if (SettingsOperator.SessionConfig.SongID != index) SelectSong(index);
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

		if (old?.Beatmap?.Checksum != newState.Beatmap?.Checksum)
			RecheckLocalMap(force: true);

		if (old != null && old.HostId != newState.HostId && newState.HostId == LocalId)
			Notify.Post("You're the host now!");

		UpdateMatchLeaderboard();
	}

	/// <summary>Works out whether we have the selected beatmap and tells the server.</summary>
	private void RecheckLocalMap(bool force = false)
	{
		if (State?.Beatmap == null) return;
		int index = FindLocalBeatmap(State.Beatmap);
		bool has = index >= 0;
		if (has && State.Phase == LanPhase.Lobby && !SettingsOperator.inGameplay && SettingsOperator.SessionConfig.SongID != index)
			SelectSong(index); // preview music + background follow the host's pick
		var me = LocalPlayer;
		if (!force && me != null && me.HasMap == has) return;
		if (IsServer) ServerSetHasMap(1, has, State.Beatmap.Checksum);
		else RpcId(1, MethodName.RpcSetHasMap, has, State.Beatmap.Checksum);
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
		Notify.Post("Couldn't connect to that room.", Type: NotificationIcons.NotificationType.Warning);
		Multiplayer.MultiplayerPeer = new OfflineMultiplayerPeer();
		ResetSession();
	}

	private void OnServerDisconnected()
	{
		bool wasInRoom = State != null;
		Multiplayer.MultiplayerPeer = new OfflineMultiplayerPeer();
		ResetSession();
		if (!wasInRoom) return;
		Notify.Post("The host closed the room.", Type: NotificationIcons.NotificationType.Warning);
		// If we're mid-song, let the player finish; the end screen sends them back to the lounge.
		if (!SettingsOperator.inGameplay && GetTree().CurrentScene?.SceneFilePath == LobbyScene)
			GetNode<SceneTransition>("/root/Transition").Switch(LoungeScene);
	}

	private void ResetSession()
	{
		State = null;
		IsServer = false;
		LocalInMatch = false;
		_joining = false;
		_dirty = false;
		_outgoing.Clear();
		_incoming = null;
		TransferProgress = -1;
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

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcSelectBeatmap(string json)
	{
		if (!IsServer || State == null) return;
		LanBeatmapRef map;
		try { map = JsonSerializer.Deserialize<LanBeatmapRef>(json); }
		catch { return; }
		ServerSetBeatmap(Multiplayer.GetRemoteSenderId(), map, announce: true);
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
