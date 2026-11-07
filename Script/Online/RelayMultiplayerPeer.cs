using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

/// <summary>
/// A <see cref="MultiplayerPeer"/> that tunnels Godot's high-level multiplayer through the server's relay
/// (ws/v1/multiplayer/), so online rooms run the exact same <see cref="LanNetwork"/> code as LAN rooms.
///
/// The host is peer 1 and runs the room; players only ever talk to the host (Godot's server relay handles
/// the rest). Everything goes over one websocket, so every transfer mode ends up reliable + ordered.
///
/// Host migration: when the host leaves, the server moves the room to a player ("promoted" / "host_moved").
/// This peer then hands its socket to a new peer object (<see cref="Handover"/>) that LanNetwork swaps in, so
/// Godot's multiplayer starts clean (its per-peer caches belong to the old host).
/// </summary>
public partial class RelayMultiplayerPeer : MultiplayerPeerExtension
{
	private const int HeaderSize = 6; // int32 peer, uint8 channel, uint8 mode
	private const double PingInterval = 15;

	private struct Packet { public int From; public int Channel; public TransferModeEnum Mode; public byte[] Data; }

	private WebSocketPeer _ws;
	private bool _isHost, _opened;
	private int _uniqueId;
	private int _joinRoom;
	private JsonObject _hostRequest;
	private ConnectionStatus _status = ConnectionStatus.Disconnected;
	private readonly Queue<Packet> _incoming = new();
	private readonly HashSet<int> _peers = new();
	private readonly Queue<(bool connected, int peer)> _pendingSignals = new();
	private int _target, _channel;
	private TransferModeEnum _mode = TransferModeEnum.Reliable;
	private bool _refuse;
	private ulong _lastPingMs;

	/// <summary>Online room id (set once hosting / joined).</summary>
	public long RoomId { get; private set; }
	/// <summary>Why the connection ended, for the player (null while it's fine).</summary>
	public string CloseReason { get; private set; }
	/// <summary>Bytes waiting to go out on the socket (big beatmap transfers hold back while this is high).</summary>
	/// <summary>Bytes of game packets sent so far (benchmarking).</summary>
	public long BytesSent { get; private set; }
	public long FramesSent { get; private set; }
	public long FramesReceived { get; private set; }
	public readonly Dictionary<string, long> SentByTarget = new();
	public int OutboundBuffered => _ws?.GetCurrentOutboundBufferedAmount() ?? 0;
	/// <summary>Host: the server-verified account name of each player, by peer id.</summary>
	public readonly Dictionary<int, string> PeerUsers = new();
	/// <summary>Set when the room moved to another host: swap this in as the MultiplayerPeer (this one is done).</summary>
	public RelayMultiplayerPeer Handover { get; private set; }
	/// <summary>With <see cref="Handover"/>: the peer id that became the new host (1). It's ours if we were promoted.</summary>
	public int NewHostOldId { get; private set; }

	/// <summary>Sockets that are closing; <see cref="PollLingering"/> keeps them going until they're done.</summary>
	private static readonly List<WebSocketPeer> Lingering = new();

	public static void PollLingering()
	{
		for (int i = Lingering.Count - 1; i >= 0; i--)
		{
			Lingering[i].Poll();
			if (Lingering[i].GetReadyState() == WebSocketPeer.State.Closed) Lingering.RemoveAt(i);
		}
	}

	// ---- Setup ----

	public Error Host(string roomName, int lanProtocol, string gameVersion)
	{
		_isHost = true;
		_uniqueId = 1;
		_hostRequest = new JsonObject { ["op"] = "host", ["name"] = roomName, ["proto"] = lanProtocol, ["ver"] = gameVersion, ["migrate"] = true };
		// Like an ENet server: usable right away (nobody can join until the server confirms the room anyway).
		return Open(ConnectionStatus.Connected);
	}

	public Error Join(long roomId)
	{
		_isHost = false;
		_joinRoom = (int)roomId;
		return Open(ConnectionStatus.Connecting);
	}

	private Error Open(ConnectionStatus initial)
	{
		_ws = OnlineApi.Connect("multiplayer", out var err);
		if (err != Error.Ok) { CloseReason = $"Couldn't reach the server ({err})."; return err; }
		_status = initial;
		_lastPingMs = Time.GetTicksMsec();
		return Error.Ok;
	}

	/// <summary>A peer that takes over this connection after the room moved (see <see cref="Handover"/>).</summary>
	private static RelayMultiplayerPeer Adopt(WebSocketPeer ws, long roomId, bool host, int id, IEnumerable<(int peer, string user)> peers)
	{
		var p = new RelayMultiplayerPeer
		{
			_ws = ws, _isHost = host, _uniqueId = id, _opened = true, RoomId = roomId,
			_status = ConnectionStatus.Connected, _lastPingMs = Time.GetTicksMsec(),
		};
		foreach (var (peer, user) in peers)
		{
			p._peers.Add(peer);
			if (host) p.PeerUsers[peer] = user;
			p._pendingSignals.Enqueue((true, peer)); // Godot learns who's there on the first poll
		}
		return p;
	}

	/// <summary>Host: refresh the room's listing (also keeps it alive on the server).
	/// <paramref name="successor"/> is who should take the room over if we leave.</summary>
	public void UpdateListing(string name, int players, int phase, string map, long successor)
	{
		if (!_isHost || RoomId == 0 || _ws == null) return;
		OnlineApi.SendJson(_ws, new JsonObject { ["op"] = "update", ["name"] = name, ["players"] = players, ["phase"] = phase, ["map"] = map ?? "", ["next"] = successor });
		_lastPingMs = Time.GetTicksMsec();
	}

	// ---- Polling ----

	public override void _Poll()
	{
		while (_pendingSignals.Count > 0)
		{
			var (connected, peer) = _pendingSignals.Dequeue();
			EmitSignal(connected ? MultiplayerPeer.SignalName.PeerConnected : MultiplayerPeer.SignalName.PeerDisconnected, (long)peer);
		}
		FlushBatch(); // what Godot sent since the last poll (at most a frame old)
		if (_ws == null) return;

		_ws.Poll();
		var state = _ws.GetReadyState();
		if (state == WebSocketPeer.State.Open)
		{
			if (!_opened)
			{
				_opened = true;
				if (_isHost) OnlineApi.SendJson(_ws, _hostRequest);
				else OnlineApi.SendJson(_ws, new JsonObject { ["op"] = "join", ["room"] = _joinRoom });
			}
			while (_ws != null && _ws.GetAvailablePacketCount() > 0)
			{
				var data = _ws.GetPacket();
				if (_ws.WasStringPacket()) HandleControl(OnlineApi.ParseJson(data));
				else HandleBinary(data);
			}
			if (_ws != null && Time.GetTicksMsec() - _lastPingMs > PingInterval * 1000)
			{
				_lastPingMs = Time.GetTicksMsec();
				OnlineApi.SendJson(_ws, new JsonObject { ["op"] = "ping" });
			}
		}
		else if (state == WebSocketPeer.State.Closed)
		{
			CloseReason ??= OnlineApi.CloseReason(_ws, _opened ? "Lost the connection to the server." : "Couldn't reach the server.");
			Shutdown();
		}
	}

	private void HandleControl(JsonObject msg)
	{
		switch (OnlineApi.Str(msg, "op"))
		{
			case "hosted":
				RoomId = OnlineApi.Long(msg, "room");
				break;
			case "joined":
				RoomId = OnlineApi.Long(msg, "room");
				_uniqueId = (int)OnlineApi.Long(msg, "peer");
				_peers.Add(1);
				_status = ConnectionStatus.Connected;
				EmitSignal(MultiplayerPeer.SignalName.PeerConnected, 1L);
				break;
			case "peer_connected":
			{
				int peer = (int)OnlineApi.Long(msg, "peer");
				if (!_isHost || peer <= 1) break;
				if (_refuse) { OnlineApi.SendJson(_ws, new JsonObject { ["op"] = "kick", ["peer"] = peer }); break; }
				_peers.Add(peer);
				PeerUsers[peer] = OnlineApi.Str(msg, "user") ?? "Guest";
				EmitSignal(MultiplayerPeer.SignalName.PeerConnected, (long)peer);
				break;
			}
			case "peer_disconnected":
			{
				int peer = (int)OnlineApi.Long(msg, "peer");
				if (_peers.Remove(peer)) EmitSignal(MultiplayerPeer.SignalName.PeerDisconnected, (long)peer);
				PeerUsers.Remove(peer);
				break;
			}
			case "promoted":
			{
				if (_isHost) break;
				var peers = new List<(int, string)>();
				if (msg["peers"] is JsonArray arr)
					foreach (var e in arr.OfType<JsonObject>())
						if ((int)OnlineApi.Long(e, "peer") is > 1 and var pid && pid != _uniqueId) peers.Add((pid, OnlineApi.Str(e, "user") ?? "Guest"));
				NewHostOldId = _uniqueId;
				Handover = Adopt(_ws, RoomId, host: true, 1, peers);
				_ws = null; // the socket is the new peer's now; keep our status so Godot doesn't see a disconnect
				break;
			}
			case "host_moved":
				if (_isHost) break;
				NewHostOldId = (int)OnlineApi.Long(msg, "old_peer");
				Handover = Adopt(_ws, RoomId, host: false, _uniqueId, new[] { (1, "") });
				_ws = null;
				break;
			case "join_failed":
			case "closed":
				CloseReason = OnlineApi.Str(msg, "text") ?? OnlineApi.Str(msg, "reason") ?? "The room closed.";
				_ws.Close();
				Shutdown();
				break;
			case "error":
				if (OnlineApi.Bool(msg, "fatal") || _status != ConnectionStatus.Connected || (_isHost && RoomId == 0))
				{
					CloseReason = OnlineApi.Str(msg, "text") ?? "The server refused the connection.";
					_ws.Close();
					Shutdown();
				}
				else GD.PrintErr($"[ONLINE] relay: {OnlineApi.Str(msg, "text")}");
				break;
			// "pong", "rooms" and anything newer: nothing to do
		}
	}

	private void HandleBinary(byte[] data)
	{
		if (_status != ConnectionStatus.Connected || data.Length < HeaderSize) return;
		FramesReceived++;
		int from = BitConverter.ToInt32(data, 0);
		if (_isHost ? !_peers.Contains(from) : from != 1) return;
		var payload = new byte[data.Length - HeaderSize];
		Buffer.BlockCopy(data, HeaderSize, payload, 0, payload.Length);
		_incoming.Enqueue(new Packet { From = from, Channel = data[4], Mode = (TransferModeEnum)data[5], Data = payload });
	}

	/// <summary>Socket gone: drop everyone. SceneMultiplayer turns the status change into server_disconnected / connection_failed.</summary>
	private void Shutdown()
	{
		_ws = null;
		var wasHost = _isHost;
		foreach (var p in _peers)
			if (wasHost) EmitSignal(MultiplayerPeer.SignalName.PeerDisconnected, (long)p);
		_peers.Clear();
		_incoming.Clear();
		_status = ConnectionStatus.Disconnected;
	}

	// ---- MultiplayerPeer ----

	// ---- Host: merge broadcasts ----
	// Godot's multiplayer sends a broadcast as one copy per player (it loops over its peers itself). Online that's N
	// frames through the server for every room update, which swamps the relay past ~50 players. Consecutive copies of
	// the same packet are held for a moment and go out as one frame "to everyone" (or "everyone but X") instead.
	private byte[] _batch;
	private int _batchChannel;
	private TransferModeEnum _batchMode;
	private readonly HashSet<int> _batchTargets = new();

	public override Error _PutPacketScript(byte[] buffer)
	{
		if (_ws == null || _status != ConnectionStatus.Connected) return Error.Unconfigured;
		if (_isHost && _target > 0)
		{
			if (_batch != null && (_batchChannel != _channel || _batchMode != _mode || _batchTargets.Contains(_target)
				|| !_batch.AsSpan().SequenceEqual(buffer)))
				FlushBatch();
			if (_batch == null) { _batch = buffer; _batchChannel = _channel; _batchMode = _mode; }
			_batchTargets.Add(_target);
			return Error.Ok;
		}
		FlushBatch(); // keep the order
		return SendFrame(_isHost ? _target : 1, buffer, _channel, _mode);
	}

	/// <summary>Sends what's held back: one frame if it's for every player (or all but one), else one per player.</summary>
	private void FlushBatch()
	{
		if (_batch == null) return;
		var data = _batch;
		_batch = null;
		var targets = _batchTargets.ToList();
		_batchTargets.Clear();
		if (_ws == null) return;
		if (targets.Count > 1 && _batchTargets.Count == 0 && targets.Count == _peers.Count && _peers.All(targets.Contains))
			SendFrame(0, data, _batchChannel, _batchMode);
		else if (targets.Count > 1 && targets.Count == _peers.Count - 1 && targets.All(_peers.Contains))
			SendFrame(-_peers.First(p => !targets.Contains(p)), data, _batchChannel, _batchMode);
		else
			foreach (var t in targets) SendFrame(t, data, _batchChannel, _batchMode);
	}

	private Error SendFrame(int target, byte[] buffer, int channel, TransferModeEnum mode)
	{
		var frame = new byte[HeaderSize + buffer.Length];
		BitConverter.TryWriteBytes(frame.AsSpan(0, 4), target);
		frame[4] = (byte)channel;
		frame[5] = (byte)mode;
		Buffer.BlockCopy(buffer, 0, frame, HeaderSize, buffer.Length);
		BytesSent += frame.Length;
		FramesSent++;
		var key = !_isHost ? "to-host" : target == 0 ? "all" : target < 0 ? "all-but" : "one";
		SentByTarget[key] = SentByTarget.GetValueOrDefault(key) + 1;
		return _ws.Send(frame, WebSocketPeer.WriteMode.Binary);
	}

	public override byte[] _GetPacketScript() => _incoming.Count > 0 ? _incoming.Dequeue().Data : Array.Empty<byte>();
	public override int _GetAvailablePacketCount() => _incoming.Count;
	public override int _GetPacketPeer() => _incoming.Count > 0 ? _incoming.Peek().From : 0;
	public override int _GetPacketChannel() => _incoming.Count > 0 ? _incoming.Peek().Channel : 0;
	public override TransferModeEnum _GetPacketMode() => _incoming.Count > 0 ? _incoming.Peek().Mode : TransferModeEnum.Reliable;
	public override int _GetMaxPacketSize() => 500 * 1024;

	public override void _SetTransferChannel(int channel) => _channel = channel;
	public override int _GetTransferChannel() => _channel;
	public override void _SetTransferMode(TransferModeEnum mode) => _mode = mode;
	public override TransferModeEnum _GetTransferMode() => _mode;
	public override void _SetTargetPeer(int peer) => _target = peer;

	public override bool _IsServer() => _isHost;
	public override bool _IsServerRelaySupported() => true;
	public override int _GetUniqueId() => _uniqueId;
	public override ConnectionStatus _GetConnectionStatus() => _status;
	public override void _SetRefuseNewConnections(bool enable) => _refuse = enable;
	public override bool _IsRefusingNewConnections() => _refuse;

	public override void _DisconnectPeer(int peer, bool force)
	{
		if (!_isHost || !_peers.Remove(peer)) return;
		PeerUsers.Remove(peer);
		OnlineApi.SendJson(_ws, new JsonObject { ["op"] = "kick", ["peer"] = peer });
		if (!force) _pendingSignals.Enqueue((false, peer)); // like ENet: the signal comes on a later poll
	}

	public override void _Close()
	{
		FlushBatch();
		if (_ws != null)
		{
			OnlineApi.SendJson(_ws, new JsonObject { ["op"] = "leave" });
			_ws.Close(1000, "leaving");
			Lingering.Add(_ws); // keep polling so the leave + close frames actually go out
		}
		_ws = null;
		_peers.Clear();
		_incoming.Clear();
		_status = ConnectionStatus.Disconnected;
	}
}
