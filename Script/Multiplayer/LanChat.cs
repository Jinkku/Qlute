using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

/// <summary>
/// LAN chat (autoload "LanChat"). Serverless: every running game is an equal peer.
///
/// - Presence: each game broadcasts a "hello" on UDP <see cref="PresencePort"/> every couple of seconds.
///   Peers that go quiet for <see cref="PeerTimeoutMs"/> are dropped.
/// - Public room (#lan): exists only while at least one other peer is around; it's invisible otherwise.
/// - Private rooms: made locally, people are invited one by one, and messages only go to members.
/// - Everything else is sent straight to the peer (unicast) and acknowledged, with a few retries.
///
/// The wire format is documented in Script/Multiplayer/LanChatProtocol.md. It is meant to stay compatible
/// for as long as this feature exists, so read the rules there before changing anything that goes on the wire.
/// </summary>
public partial class LanChat : Node
{
	// ---- Wire constants: FROZEN. Changing these breaks every older/newer client. ----
	public const string Tag = "qlute-chat";
	public const int PresencePort = 8913;
	/// <summary>Current protocol version. Only ever goes up, and only for additive changes (see LanChatProtocol.md).</summary>
	public const int Protocol = 1;
	public const string PublicRoomId = "lan";

	public const int MaxTextLength = 500;
	public const int MaxNameLength = 32;
	public const int MaxRoomTitleLength = 40;
	public const int MaxPacketBytes = 8 * 1024;
	private const double HelloInterval = 2.0;
	private const ulong PeerTimeoutMs = 7000;
	private const double RetryInterval = 0.6;
	private const int MaxRetries = 5;
	private const int MaxLines = 300;

	public static LanChat Instance { get; private set; }

	public class Peer
	{
		public string Id;
		public string Name;
		public string GameVersion;
		public int Protocol;
		public IPEndPoint EndPoint;
		public ulong LastSeenMs;
	}

	public class Line
	{
		public string FromId; // null = system line
		public string Name;
		public string Text;
		public long Time;
	}

	public class Room
	{
		public string Id;
		public string Title;
		public bool IsPublic;
		/// <summary>Member id -> last known name. Includes ourselves for private rooms.</summary>
		public readonly Dictionary<string, string> Members = new();
		/// <summary>Peers we've sent an invite to that haven't answered yet.</summary>
		public readonly HashSet<string> Invited = new();
		public readonly List<Line> Lines = new();
		public int Unread;
	}

	public class Invite
	{
		public string RoomId;
		public string Title;
		public string FromId;
		public string FromName;
		public Dictionary<string, string> Members = new();
	}

	/// <summary>Random id for this run of the game. Peers know us by it.</summary>
	public readonly string LocalId = Guid.NewGuid().ToString("N")[..16];
	public readonly Dictionary<string, Peer> Peers = new();
	public readonly Dictionary<string, Room> Rooms = new();
	public readonly List<Invite> Invites = new();
	/// <summary>Bumped whenever anything the chat UI shows changes.</summary>
	public int Version { get; private set; }
	public bool Running => _direct != null;
	/// <summary>The public room is only visible while somebody else is on the LAN.</summary>
	public bool PublicVisible => Peers.Count > 0;

	private UdpClient _presenceRx; // shared broadcast port (ReuseAddress so several games per PC work)
	private UdpClient _direct;     // our own port: sends everything, receives unicast
	private double _helloTimer;
	private string _lastName;
	private List<IPAddress> _broadcastAddrs;
	private double _broadcastAddrsAge = double.MaxValue;

	private class Pending { public string PeerId; public byte[] Data; public double Next; public int Tries; }
	private readonly Dictionary<string, Pending> _pending = new(); // key: peerId + "/" + mid
	private readonly HashSet<string> _seen = new();
	private readonly Queue<string> _seenOrder = new();

	private static readonly string[] Args = OS.GetCmdlineUserArgs();
	private static string ArgValue(string prefix) => Args.FirstOrDefault(a => a.StartsWith(prefix))?[prefix.Length..];
	private static bool TestMode => Args.Any(a => a.StartsWith("--lan-chat-test"));
	private static void TestLog(string s) { if (TestMode) GD.Print($"[LANCHAT] {s}"); }

	public override void _Ready()
	{
		Instance = this;
		ProcessMode = ProcessModeEnum.Always;
		Rooms[PublicRoomId] = new Room { Id = PublicRoomId, Title = "#lan", IsPublic = true };
		if (Enabled || TestMode) Start();
		if (TestMode) AddChild(new LanChatSelfTest());
	}

	public override void _ExitTree() => Stop();

	// ---- Settings: privacy + offline name ----
	public const string EnabledSetting = "lanchat";
	public const string NicknameSetting = "lannickname";

	/// <summary>Whether we show up in (and can use) the LAN chat. When off, no socket is open at all:
	/// nobody on the network can see us and we don't see them.</summary>
	public static bool Enabled
	{
		get
		{
			var v = SettingsOperator.GetSetting(EnabledSetting)?.ToString();
			return v == null || Check.CheckBoolValue(v);
		}
	}

	public void SetEnabled(bool on)
	{
		SettingsOperator.SetSetting(EnabledSetting, on);
		if (on && !Running) { Start(); _helloTimer = 0; }
		else if (!on && Running) Stop();
		Version++;
	}

	/// <summary>The name used when not logged in (empty = the saved account name, or "Guest").</summary>
	public static string Nickname => Clean(SettingsOperator.GetSetting(NicknameSetting)?.ToString(), MaxNameLength, "");

	/// <summary>Logged-in players always use their account name, so you can't pose as someone's account.</summary>
	public static bool CanUseNickname => !SettingsOperator.SessionConfig.Loggedin;

	public void SetNickname(string name)
	{
		SettingsOperator.SetSetting(NicknameSetting, Clean(name, MaxNameLength, ""));
		AnnounceName();
	}

	/// <summary>Tell everyone our (possibly new) name right away.</summary>
	public void AnnounceName()
	{
		var me = LocalName();
		foreach (var room in Rooms.Values)
			if (room.Members.ContainsKey(LocalId)) room.Members[LocalId] = me;
		if (Running) _helloTimer = 0;
		Version++;
	}

	private void Stop()
	{
		if (Running)
		{
			foreach (var room in Rooms.Values.Where(r => !r.IsPublic).ToList()) LeaveRoom(room.Id, notify: true);
			var bye = Encode(Packet("bye"));
			foreach (var p in Peers.Values) SendRaw(bye, p.EndPoint);
		}
		_presenceRx?.Close();
		_direct?.Close();
		_presenceRx = null;
		_direct = null;
		Peers.Clear();
		Invites.Clear();
		_pending.Clear();
		Rooms[PublicRoomId].Lines.Clear();
		Rooms[PublicRoomId].Unread = 0;
		Version++;
	}

	private void Start()
	{
		try
		{
			_direct = new UdpClient(new IPEndPoint(IPAddress.Any, 0)) { EnableBroadcast = true };
			_presenceRx = new UdpClient { ExclusiveAddressUse = false };
			_presenceRx.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
			_presenceRx.Client.Bind(new IPEndPoint(IPAddress.Any, PresencePort));
			TestLog($"started id={LocalId} port={((IPEndPoint)_direct.Client.LocalEndPoint).Port}");
		}
		catch (Exception e)
		{
			GD.PrintErr($"[LANCHAT] Disabled, couldn't open UDP {PresencePort}: {e.Message}");
			_presenceRx?.Close();
			_direct?.Close();
			_presenceRx = null;
			_direct = null;
		}
	}

	public static string LocalName()
	{
		if (CanUseNickname && Nickname.Length > 0) return Nickname;
		return Clean(ApiOperator.Username, MaxNameLength, "Guest");
	}

	// =====================================================================
	// Public API (used by ChatOverlay)
	// =====================================================================

	public IEnumerable<Room> VisibleRooms()
	{
		if (PublicVisible) yield return Rooms[PublicRoomId];
		foreach (var r in Rooms.Values.Where(r => !r.IsPublic)) yield return r;
	}

	public void Send(string roomId, string text)
	{
		if (!Running || !Rooms.TryGetValue(roomId, out var room)) return;
		text = Clean(text, MaxTextLength, null);
		if (text == null) return;
		var mid = NewId();
		var pkt = Packet("msg");
		pkt["room"] = roomId;
		pkt["mid"] = mid;
		pkt["text"] = text;
		pkt["name"] = LocalName();
		pkt["ts"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
		var targets = room.IsPublic ? Peers.Keys.ToList() : room.Members.Keys.Where(id => id != LocalId).ToList();
		foreach (var id in targets) SendReliable(id, mid, pkt);
		AddLine(room, LocalId, LocalName(), text, markUnread: false);
	}

	/// <summary>Make a private room with only us in it. Returns its id.</summary>
	public string CreatePrivateRoom(string title)
	{
		var room = new Room
		{
			Id = NewId(),
			Title = Clean(title, MaxRoomTitleLength, $"{LocalName()}'s room"),
		};
		room.Members[LocalId] = LocalName();
		Rooms[room.Id] = room;
		AddSystem(room, "Private room created. Invite people from the LAN list on the right.");
		return room.Id;
	}

	public void InvitePeer(string roomId, string peerId)
	{
		if (!Rooms.TryGetValue(roomId, out var room) || room.IsPublic || !Peers.TryGetValue(peerId, out var peer)) return;
		if (room.Members.ContainsKey(peerId)) return;
		var pkt = Packet("invite");
		var mid = NewId();
		pkt["mid"] = mid;
		pkt["room"] = room.Id;
		pkt["title"] = room.Title;
		pkt["name"] = LocalName();
		pkt["members"] = MembersJson(room);
		SendReliable(peerId, mid, pkt);
		room.Invited.Add(peerId);
		AddSystem(room, $"Invited {peer.Name}.");
	}

	public void AcceptInvite(Invite inv)
	{
		Invites.Remove(inv);
		if (!Rooms.TryGetValue(inv.RoomId, out var room))
		{
			room = new Room { Id = inv.RoomId, Title = inv.Title };
			Rooms[room.Id] = room;
		}
		foreach (var kv in inv.Members) room.Members[kv.Key] = kv.Value;
		room.Members[inv.FromId] = inv.FromName;
		room.Members[LocalId] = LocalName();
		foreach (var id in room.Members.Keys.Where(id => id != LocalId).ToList())
		{
			var pkt = Packet("join");
			var mid = NewId();
			pkt["mid"] = mid;
			pkt["room"] = room.Id;
			SendReliable(id, mid, pkt);
		}
		AddSystem(room, $"You joined {room.Title}.");
	}

	public void DeclineInvite(Invite inv)
	{
		Invites.Remove(inv);
		var pkt = Packet("decline");
		pkt["room"] = inv.RoomId;
		if (Peers.TryGetValue(inv.FromId, out var p)) SendRaw(Encode(pkt), p.EndPoint);
		Version++;
	}

	public void LeaveRoom(string roomId, bool notify = true)
	{
		if (!Rooms.TryGetValue(roomId, out var room) || room.IsPublic) return;
		if (notify)
		{
			foreach (var id in room.Members.Keys.Where(id => id != LocalId))
			{
				var pkt = Packet("leave");
				var mid = NewId();
				pkt["mid"] = mid;
				pkt["room"] = roomId;
				SendReliable(id, mid, pkt);
			}
		}
		Rooms.Remove(roomId);
		Version++;
	}

	public void MarkRead(string roomId)
	{
		if (Rooms.TryGetValue(roomId, out var r) && r.Unread != 0) { r.Unread = 0; Version++; }
	}

	public bool IsOnline(string peerId) => peerId == LocalId || Peers.ContainsKey(peerId);

	// =====================================================================
	// Frame update
	// =====================================================================

	public override void _Process(double delta)
	{
		if (!Running) return;

		var name = LocalName();
		if (name != _lastName) { _lastName = name; AnnounceName(); }

		_helloTimer -= delta;
		if (_helloTimer <= 0)
		{
			_helloTimer = HelloInterval;
			SendHello();
		}

		Receive(_presenceRx);
		Receive(_direct);

		ulong now = Time.GetTicksMsec();
		var stale = Peers.Values.Where(p => now - p.LastSeenMs > PeerTimeoutMs).Select(p => p.Id).ToList();
		foreach (var id in stale) DropPeer(id, "timed out");

		foreach (var (key, p) in _pending.ToList())
		{
			p.Next -= delta;
			if (p.Next > 0) continue;
			if (p.Tries >= MaxRetries || !Peers.TryGetValue(p.PeerId, out var peer)) { _pending.Remove(key); continue; }
			p.Tries++;
			p.Next = RetryInterval;
			SendRaw(p.Data, peer.EndPoint);
		}
	}

	private void SendHello()
	{
		var pkt = Packet("hello");
		pkt["name"] = LocalName();
		pkt["game"] = LanNetwork.GameVersion();
		pkt["port"] = ((IPEndPoint)_direct.Client.LocalEndPoint).Port;
		var data = Encode(pkt);
		foreach (var addr in BroadcastAddresses()) SendRaw(data, new IPEndPoint(addr, PresencePort));
		SendRaw(data, new IPEndPoint(IPAddress.Loopback, PresencePort)); // other games on this PC
	}

	private void Receive(UdpClient sock)
	{
		if (sock == null) return;
		try
		{
			while (sock.Available > 0)
			{
				IPEndPoint from = null;
				var data = sock.Receive(ref from);
				if (data.Length > MaxPacketBytes) continue;
				JsonObject pkt;
				try { pkt = JsonNode.Parse(data) as JsonObject; }
				catch { continue; }
				if (pkt == null || Str(pkt, "q") != Tag) continue;
				var id = Str(pkt, "id");
				if (string.IsNullOrEmpty(id) || id == LocalId || id.Length > 64) continue;
				Handle(pkt, id, from);
			}
		}
		catch (Exception e)
		{
			if (Running) GD.PrintErr($"[LANCHAT] Receive error: {e.Message}");
		}
	}

	// =====================================================================
	// Packet handling. Unknown types and fields are ignored on purpose (forward compatibility).
	// =====================================================================

	private void Handle(JsonObject pkt, string id, IPEndPoint from)
	{
		var type = Str(pkt, "t");
		if (type == "hello") { OnHello(pkt, id, from); return; }

		if (!Peers.TryGetValue(id, out var peer)) return; // only talk to peers we've heard a hello from
		peer.LastSeenMs = Time.GetTicksMsec();

		// A private message can beat the sender's "join" here. Don't ack it, so the sender retries after the join lands.
		if (type == "msg" && Rooms.TryGetValue(Str(pkt, "room") ?? "", out var target) && !target.IsPublic
			&& !target.Members.ContainsKey(id)) return;

		// Reliable packets carry "mid": always ack (even duplicates, the first ack may have been lost).
		var mid = Str(pkt, "mid");
		if (!string.IsNullOrEmpty(mid) && type != "ack")
		{
			var ack = Packet("ack");
			ack["mid"] = mid;
			SendRaw(Encode(ack), peer.EndPoint);
			if (!FirstTime(id + "/" + mid)) return;
		}

		switch (type)
		{
			case "ack": _pending.Remove(id + "/" + mid); break;
			case "bye": DropPeer(id, "left"); break;
			case "msg": OnMessage(pkt, peer); break;
			case "invite": OnInvite(pkt, peer); break;
			case "join": OnJoin(pkt, peer); break;
			case "leave": OnLeave(pkt, peer); break;
			case "decline":
				if (Rooms.TryGetValue(Str(pkt, "room") ?? "", out var r) && !r.IsPublic && r.Invited.Remove(peer.Id))
					AddSystem(r, $"{peer.Name} declined the invite.");
				break;
			default: break; // a newer client's packet: ignore
		}
	}

	private void OnHello(JsonObject pkt, string id, IPEndPoint from)
	{
		int port = Int(pkt, "port", from.Port);
		if (port <= 0 || port > 65535) return;
		var name = Clean(Str(pkt, "name"), MaxNameLength, "Guest");
		var ep = new IPEndPoint(from.Address, port);
		bool isNew = !Peers.TryGetValue(id, out var peer);
		if (isNew)
		{
			peer = new Peer { Id = id };
			Peers[id] = peer;
		}
		// Prefer a non-loopback address so the endpoint stays the same however the hello arrived.
		if (isNew || !IPAddress.IsLoopback(from.Address)) peer.EndPoint = ep;
		bool changed = isNew || peer.Name != name;
		peer.Name = name;
		peer.GameVersion = Clean(Str(pkt, "game"), 32, "?");
		peer.Protocol = Int(pkt, "v", 1);
		peer.LastSeenMs = Time.GetTicksMsec();
		foreach (var room in Rooms.Values)
			if (room.Members.ContainsKey(id)) room.Members[id] = name;
		if (isNew)
		{
			TestLog($"peer {name} ({id}) at {ep}");
			if (Peers.Count == 1) AddSystem(Rooms[PublicRoomId], "Someone else is playing on this network. Say hi!");
			AddSystem(Rooms[PublicRoomId], $"{name} is online.", markUnread: false);
			SendHello(); // answer right away so they see us without waiting a full interval
		}
		if (changed) Version++;
	}

	private void OnMessage(JsonObject pkt, Peer peer)
	{
		var roomId = Str(pkt, "room") ?? "";
		var text = Clean(Str(pkt, "text"), MaxTextLength, null);
		if (text == null || !Rooms.TryGetValue(roomId, out var room)) return;
		if (!room.IsPublic && !room.Members.ContainsKey(peer.Id)) return; // not a member: drop
		var name = Clean(Str(pkt, "name"), MaxNameLength, peer.Name); // optional: newest name even before the next hello
		AddLine(room, peer.Id, name, text, markUnread: true);
	}

	private void OnInvite(JsonObject pkt, Peer peer)
	{
		var roomId = Str(pkt, "room");
		if (string.IsNullOrEmpty(roomId) || roomId == PublicRoomId || roomId.Length > 64) return;
		if (Rooms.ContainsKey(roomId)) return; // already in it
		Invites.RemoveAll(i => i.RoomId == roomId);
		var inv = new Invite
		{
			RoomId = roomId,
			Title = Clean(Str(pkt, "title"), MaxRoomTitleLength, $"{peer.Name}'s room"),
			FromId = peer.Id,
			FromName = Clean(Str(pkt, "name"), MaxNameLength, peer.Name),
		};
		if (pkt["members"] is JsonArray arr)
		{
			foreach (var m in arr.OfType<JsonObject>().Take(256))
			{
				var mid = Str(m, "id");
				if (!string.IsNullOrEmpty(mid) && mid.Length <= 64 && mid != LocalId) inv.Members[mid] = Clean(Str(m, "name"), MaxNameLength, "Guest");
			}
		}
		Invites.Add(inv);
		Version++;
		TestLog($"invite to {inv.Title} from {inv.FromName}");
		Notify.Post($"{inv.FromName} invited you to the LAN chat room \"{inv.Title}\". Open chat to answer.");
	}

	private void OnJoin(JsonObject pkt, Peer peer)
	{
		if (!Rooms.TryGetValue(Str(pkt, "room") ?? "", out var room) || room.IsPublic) return;
		room.Invited.Remove(peer.Id);
		if (room.Members.ContainsKey(peer.Id)) return;
		room.Members[peer.Id] = peer.Name;
		AddSystem(room, $"{peer.Name} joined.");
	}

	private void OnLeave(JsonObject pkt, Peer peer)
	{
		if (!Rooms.TryGetValue(Str(pkt, "room") ?? "", out var room) || room.IsPublic) return;
		if (room.Members.Remove(peer.Id)) AddSystem(room, $"{peer.Name} left.");
	}

	private void DropPeer(string id, string why)
	{
		if (!Peers.Remove(id, out var peer)) return;
		foreach (var k in _pending.Keys.Where(k => k.StartsWith(id + "/")).ToList()) _pending.Remove(k);
		Invites.RemoveAll(i => i.FromId == id);
		TestLog($"peer {peer.Name} {why}");
		AddSystem(Rooms[PublicRoomId], $"{peer.Name} went offline.", markUnread: false);
		Version++;
	}

	// =====================================================================
	// Helpers
	// =====================================================================

	private void AddLine(Room room, string fromId, string name, string text, bool markUnread)
	{
		room.Lines.Add(new Line { FromId = fromId, Name = name, Text = text, Time = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() });
		if (room.Lines.Count > MaxLines) room.Lines.RemoveRange(0, room.Lines.Count - MaxLines);
		if (markUnread) room.Unread++;
		Version++;
		TestLog($"[{room.Title}] {(fromId == null ? "*" : name)}: {text}");
	}

	private void AddSystem(Room room, string text, bool markUnread = false) => AddLine(room, null, null, text, markUnread);

	private JsonObject Packet(string type) => new()
	{
		["q"] = Tag,
		["v"] = Protocol,
		["t"] = type,
		["id"] = LocalId,
	};

	private JsonArray MembersJson(Room room)
	{
		var arr = new JsonArray();
		foreach (var (id, name) in room.Members) arr.Add(new JsonObject { ["id"] = id, ["name"] = name });
		return arr;
	}

	private static byte[] Encode(JsonObject pkt) => Encoding.UTF8.GetBytes(pkt.ToJsonString());

	private void SendReliable(string peerId, string mid, JsonObject pkt)
	{
		if (!Peers.TryGetValue(peerId, out var peer)) return;
		var data = Encode(pkt);
		_pending[peerId + "/" + mid] = new Pending { PeerId = peerId, Data = data, Next = RetryInterval, Tries = 0 };
		SendRaw(data, peer.EndPoint);
	}

	private void SendRaw(byte[] data, IPEndPoint ep)
	{
		if (_direct == null || ep == null || data.Length > MaxPacketBytes) return;
		try { _direct.Send(data, data.Length, ep); }
		catch (SocketException) { /* interface went away / unreachable: retries or timeouts handle it */ }
		catch (ObjectDisposedException) { }
	}

	private bool FirstTime(string key)
	{
		if (!_seen.Add(key)) return false;
		_seenOrder.Enqueue(key);
		while (_seenOrder.Count > 2000) _seen.Remove(_seenOrder.Dequeue());
		return true;
	}

	private static string NewId() => Guid.NewGuid().ToString("N")[..16];

	/// <summary>Reads a string field leniently: wrong types read as null instead of failing the whole packet.</summary>
	private static string Str(JsonObject o, string key)
	{
		try { return o[key] is JsonValue v && v.TryGetValue<string>(out var s) ? s : null; }
		catch { return null; }
	}

	private static int Int(JsonObject o, string key, int fallback)
	{
		try
		{
			if (o[key] is not JsonValue v) return fallback;
			if (v.TryGetValue<int>(out var i)) return i;
			if (v.TryGetValue<double>(out var d) && d >= int.MinValue && d <= int.MaxValue) return (int)d;
		}
		catch { }
		return fallback;
	}

	public static string Clean(string s, int maxLen, string fallback)
	{
		if (string.IsNullOrWhiteSpace(s)) return fallback;
		s = new string(s.Where(c => !char.IsControl(c)).ToArray()).Trim();
		if (s.Length > maxLen) s = s[..maxLen];
		return s.Length == 0 ? fallback : s;
	}

	private List<IPAddress> BroadcastAddresses()
	{
		_broadcastAddrsAge += 1;
		if (_broadcastAddrs != null && _broadcastAddrsAge < 15) return _broadcastAddrs;
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
		catch (Exception e) { GD.PrintErr($"[LANCHAT] Couldn't list network interfaces: {e.Message}"); }
		_broadcastAddrs = list;
		return list;
	}
}

/// <summary>
/// Dev-only headless test. Flags (after "--"):
///   --lan-chat-test                 enable logging ([LANCHAT] lines)
///   --lan-chat-test-say=TEXT        say TEXT in #lan once a peer shows up
///   --lan-chat-test-invite          make a private room, invite the first peer, then talk in it
///   --lan-chat-test-accept          accept invites automatically and reply in the room
///   --lan-chat-test-quit=SECONDS    quit after SECONDS
///   --lan-chat-test-name=NAME       change the offline name to NAME once a peer shows up
///   --lan-chat-test-off=SECONDS     turn LAN chat off SECONDS after a peer shows up (privacy toggle)
///   --lan-chat-test-shot=PATH       open the chat overlay and save a screenshot to PATH (needs a window)
/// </summary>
public partial class LanChatSelfTest : Node
{
	private static readonly string[] Args = OS.GetCmdlineUserArgs();
	private static string Value(string p) => Args.FirstOrDefault(a => a.StartsWith(p))?[p.Length..];
	private bool _said, _invited, _privSaid, _renamed, _off;
	private string _room;
	private double _t, _peerAt = -1;

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
		if (double.TryParse(Value("--lan-chat-test-quit="), out var q))
			GetTree().CreateTimer(q).Timeout += () => { GD.Print("[LANCHAT] quitting"); GetTree().Quit(); };
	}

	public override void _Process(double delta)
	{
		var c = LanChat.Instance;
		_t += delta;

		// --online-chat-test-accept: accept the first online chat invite, then talk in it and in #general.
		var oc = OnlineChat.Instance;
		if (Args.Contains("--online-chat-test-accept") && oc != null)
		{
			if (_onlineRoom == null && oc.Invites.Count > 0) { _onlineRoom = oc.Invites[0].RoomId; oc.AcceptInvite(oc.Invites[0]); GD.Print($"[LANCHAT] accepted online invite {_onlineRoom}"); }
			if (_onlineRoom != null && _onlineSaidAt < 0 && oc.Rooms.ContainsKey(_onlineRoom))
			{
				_onlineSaidAt = _t;
				oc.Send(_onlineRoom, "hi bob, joined from the game");
				oc.Send("#general", "hello #general from the game");
				GD.Print($"[LANCHAT] online rooms: {string.Join(", ", oc.Rooms.Values.Select(r => r.Title))}; online users: {string.Join(", ", oc.OnlineUsers)}");
			}
		}
		if (Value("--lan-chat-test-shot=") is { } onlineShot && _overlay == null && _onlineSaidAt >= 0 && _t - _onlineSaidAt > 3) OpenOverlayAndShoot(onlineShot);

		if (c.Peers.Count == 0) return;
		if (_peerAt < 0) { _peerAt = _t; GD.Print($"[LANCHAT] public room visible={c.PublicVisible}"); }
		if (_t - _peerAt < 1) return;

		var nick = Value("--lan-chat-test-name=");
		if (!_renamed && nick != null) { _renamed = true; c.SetNickname(nick); GD.Print($"[LANCHAT] renamed to {LanChat.LocalName()}"); }
		if (!_off && double.TryParse(Value("--lan-chat-test-off="), out var off) && _t - _peerAt > off)
		{
			_off = true;
			c.SetEnabled(false);
			GD.Print($"[LANCHAT] turned off, running={c.Running} peers={c.Peers.Count}");
			return;
		}

		var say = Value("--lan-chat-test-say=");
		if (!_said && say != null) { _said = true; c.Send(LanChat.PublicRoomId, say); }

		if (!_invited && Args.Contains("--lan-chat-test-invite"))
		{
			_invited = true;
			_room = c.CreatePrivateRoom("Test room");
			c.InvitePeer(_room, c.Peers.Keys.First());
		}
		if (_room != null && !_privSaid && c.Rooms.TryGetValue(_room, out var r) && r.Members.Count > 1)
		{
			_privSaid = true;
			c.Send(_room, "secret hello from the inviter");
		}

		if (Args.Contains("--lan-chat-test-accept") && c.Invites.Count > 0)
		{
			var inv = c.Invites[0];
			c.AcceptInvite(inv);
			c.Send(inv.RoomId, "thanks for the invite");
		}

		var shot = Value("--lan-chat-test-shot=");
		if (shot != null && _overlay == null && _t - _peerAt > 6) OpenOverlayAndShoot(shot);
	}

	private string _onlineRoom;
	private double _onlineSaidAt = -1;

	private void OpenOverlayAndShoot(string shot)
	{
		{
			_overlay = GD.Load<PackedScene>("res://Panels/Overlays/ChatOverlay.tscn").Instantiate<Control>();
			var layer = new CanvasLayer { Layer = 100 };
			AddChild(layer);
			layer.AddChild(_overlay);
			_overlay.Size = new Vector2(GetViewport().GetVisibleRect().Size.X, 300);
			_overlay.Position = new Vector2(0, GetViewport().GetVisibleRect().Size.Y - 300);
			GetTree().CreateTimer(1.5).Timeout += () =>
			{
				GetViewport().GetTexture().GetImage().SavePng(shot);
				GD.Print($"[LANCHAT] screenshot {shot}");
			};
		}
	}

	private Control _overlay;
}
