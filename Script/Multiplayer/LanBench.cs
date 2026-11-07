using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

/// <summary>
/// Dev-only multiplayer load test. One process hosts, others run many bot players each (real LanNetwork code,
/// own connection per bot, fake gameplay). Results are JSON lines appended to --bench-out=FILE.
///
///   host: -- --bench-host --bench-players=N [--bench-online] --bench-out=F [--bench-room-file=F]
///   bots: -- --bench-bots=N --bench-first=K [--bench-online --bench-room-file=F] --bench-out=F
/// Online also needs --online-test-user/--online-test-pass (and QLUTE_WS_BASE for a local server).
/// Flow: everyone joins -> all ready -> host starts -> bots stream scores for --bench-song seconds -> results.
/// </summary>
public partial class LanBench : Node
{
	private static readonly string[] Args = OS.GetCmdlineUserArgs();
	private static string Value(string prefix) => Args.FirstOrDefault(a => a.StartsWith(prefix))?[prefix.Length..];
	private static bool Has(string flag) => Args.Contains(flag);
	private static int Int(string prefix, int fallback) => int.TryParse(Value(prefix), out var v) ? v : fallback;

	public static void Attach(LanNetwork net)
	{
		if (!Has("--bench-host") && Value("--bench-bots=") == null) return;
		net.CallDeferred(Node.MethodName.AddChild, new LanBench { _net = net });
	}

	private LanNetwork _net;
	private bool Online => Has("--bench-online");
	private string OutFile => Value("--bench-out=");
	private double _t;

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
		if (Value("--bench-bots=") != null) StartBots();
	}

	public override void _Process(double delta)
	{
		_t += delta;
		if (Has("--bench-host")) HostTick(delta);
		else BotsTick(delta);
	}

	private void Write(object o)
	{
		var line = JsonSerializer.Serialize(o);
		GD.Print("[BENCH] " + line);
		if (OutFile != null) File.AppendAllText(OutFile, line + "\n");
	}

	private static Dictionary<string, double> Stats(List<double> v)
	{
		if (v.Count == 0) return new() { ["n"] = 0 };
		var s = v.OrderBy(x => x).ToList();
		double P(double q) => s[Math.Min(s.Count - 1, (int)(q * s.Count))];
		return new() { ["n"] = s.Count, ["avg"] = Math.Round(s.Average(), 2), ["p50"] = Math.Round(P(0.5), 2), ["p95"] = Math.Round(P(0.95), 2), ["p99"] = Math.Round(P(0.99), 2), ["max"] = Math.Round(s[^1], 2) };
	}

	// =====================================================================
	// Host
	// =====================================================================

	private enum HostPhase { Starting, Joining, Readying, Playing, Done }
	private HostPhase _phase = HostPhase.Starting;
	private double _phaseAt;
	private readonly Dictionary<HostPhase, List<double>> _frames = new();
	private readonly List<double> _sentPerSec = new();
	private readonly List<double> _stateBytes = new();
	private double _secTimer;
	private long _lastSent;
	private double _joinSeconds;
	private int _maxJoined;

	private void SetPhase(HostPhase p) { _phase = p; _phaseAt = _t; GD.Print($"[BENCH] host phase {p} at {_t:0.0}s"); }

	private void HostTick(double delta)
	{
		int expected = Int("--bench-players=", 8);
		if (_phase != HostPhase.Starting && _phase != HostPhase.Done)
		{
			if (!_frames.TryGetValue(_phase, out var list)) _frames[_phase] = list = new();
			list.Add(delta * 1000);
			_secTimer += delta;
			if (_secTimer >= 1)
			{
				long sent = SentBytes();
				_sentPerSec.Add((sent - _lastSent) / _secTimer / 1024.0);
				_lastSent = sent;
				_secTimer = 0;
				if (_net.State != null) _stateBytes.Add(JsonSerializer.Serialize(_net.State).Length / 1024.0);
			}
		}
		var s = _net.State;
		int players = s == null ? 0 : s.Players.Count - 1; // not counting the host
		_maxJoined = Math.Max(_maxJoined, players);
		switch (_phase)
		{
			case HostPhase.Starting when _t > 4:
				bool ok = Online ? _net.HostOnline("Bench room") : _net.Host("Bench room");
				if (!ok) { Finish("couldn't host"); return; }
				_net.HostAdjustTimer(10000); // longest countdown: the benchmark starts the match itself
				_net.AddToQueue(Math.Max(0, SettingsOperator.SessionConfig.SongID));
				SetPhase(HostPhase.Joining);
				break;
			case HostPhase.Joining:
				if (Online && Value("--bench-room-file=") is { } rf && (_net.Multiplayer.MultiplayerPeer as RelayMultiplayerPeer)?.RoomId is > 0 and var id && !File.Exists(rf))
					File.WriteAllText(rf, id.ToString());
				if (players >= expected) { _joinSeconds = _t - _phaseAt; SetPhase(HostPhase.Readying); }
				else if (_t - _phaseAt > 120) Finish($"only {players}/{expected} joined");
				break;
			case HostPhase.Readying:
				if (s.Players.Count(p => p.Ready) >= expected) { _net.HostStartNow(); }
				if (s.Phase == LanPhase.Playing) SetPhase(HostPhase.Playing);
				else if (_t - _phaseAt > 60) Finish($"only {s.Players.Count(p => p.Ready)}/{expected} ready; not ready: " +
					string.Join(", ", s.Players.Where(p => !p.Ready && p.Id != 1).Take(10).Select(p => $"{p.Name}(map={p.HasMap})")));
				break;
			case HostPhase.Playing:
				if (s.Phase == LanPhase.Lobby) Finish(s.LastResults.Count >= expected ? null : $"results had {s.LastResults.Count}/{expected}");
				else if (_t - _phaseAt > 120) Finish("match never ended");
				break;
		}
	}

	private long SentBytes()
	{
		var peer = _net.Multiplayer.MultiplayerPeer;
		if (peer is RelayMultiplayerPeer relay) return relay.BytesSent;
		if (peer is ENetMultiplayerPeer enet && enet.Host != null) return _lastSent + (long)enet.Host.PopStatistic(ENetConnection.HostStatistic.SentData);
		return _lastSent;
	}

	private void Finish(string error)
	{
		if (_phase == HostPhase.Done) return;
		Write(new
		{
			role = "host", mode = Online ? "online" : "lan", players = Int("--bench-players=", 8), joined = _maxJoined,
			ok = error == null, error, join_s = Math.Round(_joinSeconds, 1),
			lobby_frame_ms = Stats(_frames.GetValueOrDefault(HostPhase.Readying) ?? new()),
			match_frame_ms = Stats(_frames.GetValueOrDefault(HostPhase.Playing) ?? new()),
			upload_kib_s = Stats(_sentPerSec), state_kib = Stats(_stateBytes),
			frames_sent = (_net.Multiplayer.MultiplayerPeer as RelayMultiplayerPeer)?.FramesSent ?? 0,
			frames_by_target = (_net.Multiplayer.MultiplayerPeer as RelayMultiplayerPeer)?.SentByTarget,
		});
		SetPhase(HostPhase.Done);
		if (Value("--bench-done-file=") is { } df) File.WriteAllText(df, "done");
		GetTree().CreateTimer(3).Timeout += () => { _net.Leave(silent: true); GetTree().Quit(); };
	}

	// =====================================================================
	// Bots
	// =====================================================================

	private readonly List<LanNetwork> _bots = new();
	private int _joinNext;
	private double _joinTimer;
	private long _roomId;
	private readonly List<double> _probe = new();
	private readonly Dictionary<LanNetwork, (string token, ulong sentAt, int seenChat)> _probing = new();
	private double _probeTimer;
	private readonly List<double> _botFrames = new();
	private readonly Dictionary<LanNetwork, double> _readyAt = new();
	private bool _sawMatch, _reported;
	private double _quietSince = -1;

	private void StartBots()
	{
		int n = Int("--bench-bots=", 8), first = Int("--bench-first=", 1);
		for (int i = 0; i < n; i++)
		{
			// Each bot gets its own SceneMultiplayer, so it's its own player with its own connection.
			var root = new Node { Name = $"Bot{first + i}" };
			GetTree().Root.AddChild(root);
			GetTree().SetMultiplayer(new SceneMultiplayer(), root.GetPath());
			var bot = new LanNetwork { Name = "LanNetwork", IsBot = true, BotName = $"bot{first + i}", BotSongSeconds = Int("--bench-song=", 20) };
			root.AddChild(bot);
			_bots.Add(bot);
		}
	}

	private void BotsTick(double delta)
	{
		if (_bots.Count == 0) return;
		_botFrames.Add(delta * 1000);
		if (Online && _roomId == 0)
		{
			if (Value("--bench-room-file=") is { } rf && File.Exists(rf) && long.TryParse(File.ReadAllText(rf).Trim(), out var id)) _roomId = id;
			else return;
		}
		// Join in waves (like people arriving), 10 every 0.1 s.
		_joinTimer -= delta;
		if (_joinNext < _bots.Count && _joinTimer <= 0 && _t > 15) // after the game has finished booting
		{
			_joinTimer = 0.1;
			for (int k = 0; k < 10 && _joinNext < _bots.Count; k++, _joinNext++)
			{
				var bot = _bots[_joinNext];
				if (Online) bot.JoinOnline(_roomId);
				else bot.Join(Value("--bench-addr=") ?? "127.0.0.1", Int("--bench-port=", LanNetwork.DefaultGamePort));
			}
		}
		foreach (var bot in _bots)
		{
			var me = bot.LocalPlayer;
			// Ready up (again after a reconnect, like a player would), at most every 3 s.
			if (me != null && me.HasMap && !me.Ready && bot.State.Phase == LanPhase.Lobby
				&& (!_readyAt.TryGetValue(bot, out var at) || _t - at > 3)) { _readyAt[bot] = _t; bot.SetReady(true); }
			if (bot.State?.Phase == LanPhase.Playing) _sawMatch = true;
		}

		// Latency probes: a few bots send a chat line every 2 s and time how long until it comes back to them.
		foreach (var (bot, p) in _probing.ToList())
		{
			if (bot.ChatLog.Any(l => l.Contains(p.token)))
			{
				_probe.Add((Time.GetTicksMsec() - p.sentAt));
				_probing.Remove(bot);
			}
			else if (Time.GetTicksMsec() - p.sentAt > 10000) { _probe.Add(10000); _probing.Remove(bot); }
		}
		_probeTimer -= delta;
		if (_probeTimer <= 0)
		{
			_probeTimer = 2;
			foreach (var bot in _bots.Where(b => b.LocalPlayer != null).Take(4).Where(b => !_probing.ContainsKey(b)))
			{
				var token = $"probe-{bot.BotName}-{Time.GetTicksMsec()}";
				_probing[bot] = (token, Time.GetTicksMsec(), bot.ChatLog.Count);
				bot.SendChat(token);
			}
		}

		// Done once the host left (or after a long time).
		int connected = _bots.Count(b => b.State != null);
		if (_sawMatch && connected == 0 && _quietSince < 0) _quietSince = _t;
		bool hostDone = Value("--bench-done-file=") is { } df && File.Exists(df);
		if (!_reported && (hostDone || (_quietSince >= 0 && _t - _quietSince > 1) || _t > 400))
		{
			_reported = true;
			Write(new
			{
				role = "bots", mode = Online ? "online" : "lan", first = Int("--bench-first=", 1), bots = _bots.Count,
				saw_match = _sawMatch, chat_roundtrip_ms = Stats(_probe), bot_frame_ms = Stats(_botFrames),
				frames_received = Stats(_bots.Select(b => (double)((b.Multiplayer.MultiplayerPeer as RelayMultiplayerPeer)?.FramesReceived ?? 0)).ToList()),
			});
			GetTree().Quit();
		}
	}
}
