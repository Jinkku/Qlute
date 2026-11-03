using Godot;
using System;
using System.Linq;

/// <summary>
/// Dev-only LAN smoke test. Run two instances (e.g. with --headless):
///   ... -- --lan-test-host
///   ... -- --lan-test-join [--lan-test-nomap --lan-test-out=/some/dir]
/// Add --lan-test-transfer to both: the host hands host to the joiner, who renames the room and bumps the timer.
/// Add --lan-test-quit=SECONDS to exit automatically. Output lines are prefixed with [LANTEST].
/// </summary>
public partial class LanSelfTest : Node
{
	private static readonly string[] Args = OS.GetCmdlineUserArgs();
	private static bool Has(string flag) => Args.Contains(flag);
	private static string Value(string prefix) => Args.FirstOrDefault(a => a.StartsWith(prefix))?[prefix.Length..];

	public static bool Enabled => Has("--lan-test-host") || Has("--lan-test-join");
	/// <summary>Pretend we don't own the host's beatmap (exercises the LAN beatmap transfer).</summary>
	public static bool ForceMissingMap => Has("--lan-test-nomap");
	/// <summary>Where received beatmaps are written instead of the real downloads folder.</summary>
	public static string TransferOutputDir => Value("--lan-test-out=");

	public static void Log(string msg)
	{
		if (Enabled) GD.Print($"[LANTEST] {msg}");
	}

	public static void Attach(LanNetwork net)
	{
		if (!Enabled) return;
		net.CallDeferred(Node.MethodName.AddChild, new LanSelfTest { _net = net });
	}

	private LanNetwork _net;
	private double _t, _logTimer;
	private bool _started, _chatted, _readied, _requested;
	private int _lastPhase = -1;
	private double _matchTime, _foundAt = -1, _lobbySeenAt = -1;
	private bool _loungeShot, _lobbyShot;

	private void Screenshot(string path)
	{
		GetViewport().GetTexture().GetImage().SavePng(path);
		Log($"screenshot {path}");
	}
	private bool _forfeited;
	private bool _transferred, _renamed;

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
		if (double.TryParse(Value("--lan-test-quit="), out var q))
			GetTree().CreateTimer(q).Timeout += () => { Log("quitting"); _net.Leave(silent: true); GetTree().Quit(); };
	}

	public override void _Process(double delta)
	{
		_t += delta;
		bool host = Has("--lan-test-host");

		if (!_started && _t > 6)
		{
			_started = true;
			if (host)
			{
				Log($"hosting: {_net.Host("LAN Test Room")}");
				_net.HostAdjustTimer(-10000); // shortest countdown
			}
			else
			{
				Log("searching for rooms");
				if (Value("--lan-test-shot=") != null) GetNode<SceneTransition>("/root/Transition").Switch(LanNetwork.LoungeScene);
				_net.StartDiscovery();
			}
		}

		// --lan-test-shot=PREFIX: save screenshots of the lounge and the lobby (windowed runs only).
		var shot = Value("--lan-test-shot=");
		if (shot != null && !host && _net.Rooms.Count > 0 && _foundAt < 0) _foundAt = _t;
		if (shot != null && !host && _foundAt >= 0 && !_loungeShot && _t > _foundAt + 2.5) { _loungeShot = true; Screenshot(shot + "_lounge.png"); }
		if (shot != null && _net.State != null && GetTree().CurrentScene?.Name == "MultiplayerLobby" && _lobbySeenAt < 0) _lobbySeenAt = _t;
		if (shot != null && _lobbySeenAt >= 0 && !_lobbyShot && _t > _lobbySeenAt + 3) { _lobbyShot = true; Screenshot(shot + "_lobby.png"); }
		if (shot != null && !host && !_loungeShot) return; // wait for the lounge screenshot before joining

		if (!host && _started && _net.State == null && _net.Rooms.Count > 0 && !_requested)
		{
			var room = _net.Rooms.Values.First();
			Log($"found room '{room.Beacon.name}' at {room.Address}:{room.Port} with {room.Beacon.players} player(s), map: {room.Beacon.map}");
			_requested = true;
			_net.Join(room.Address, room.Port);
		}

		var me = _net.LocalPlayer;
		if (me != null && !_chatted)
		{
			_chatted = true;
			_net.SendChat(host ? "hello from the host" : "hello from the client");
			if (ForceMissingMap) { Log("requesting beatmap from host"); _net.RequestBeatmapFromHost(); }
		}
		if (me != null && me.HasMap && !_readied && !ForceMissingMap && _net.State.Phase == LanPhase.Lobby && (host ? _net.State.Players.Count > 1 : true))
		{
			_readied = true;
			_net.SetReady(true);
			Log("readied up");
		}

		// --lan-test-transfer: exercise host hand-off + rename from a non-server host.
		if (Has("--lan-test-transfer") && _net.State != null)
		{
			if (host && !_transferred && _net.IsHost && _net.State.Players.Count > 1)
			{
				_transferred = true;
				var other = _net.State.Players.First(p => p.Id != _net.LocalId);
				Log($"transferring host to {other.Name} (id {other.Id})");
				_net.HostTransfer(other.Id);
			}
			if (!host && !_renamed && _net.IsHost)
			{
				_renamed = true;
				Log("I am the host now: renaming room and raising the timer");
				_net.HostRename("Renamed by client");
				_net.HostAdjustTimer(15);
			}
		}

		// --lan-test-forfeit=SECONDS: quit the match after that long in gameplay (exercises results/back-to-lobby).
		if (LanNetwork.InMatch && SettingsOperator.inGameplay && double.TryParse(Value("--lan-test-forfeit="), out var ff))
		{
			_matchTime += delta;
			if (_matchTime > ff && !_forfeited)
			{
				_forfeited = true;
				Log("forfeiting");
				_net.ReportFinished(quit: true);
				GetNode<SceneTransition>("/root/Transition").Switch(_net.ReturnScene);
			}
		}

		var s = _net.State;
		if (s != null && (int)s.Phase != _lastPhase)
		{
			_lastPhase = (int)s.Phase;
			Log($"phase -> {s.Phase}");
			if (s.Phase == LanPhase.Lobby) _readied = false;
		}

		_logTimer -= delta;
		if (_logTimer <= 0 && s != null)
		{
			_logTimer = 2;
			var players = string.Join(" | ", s.Players.Select(p => $"{p.Name}(id {p.Id}) ready={p.Ready} map={p.HasMap} inMatch={p.InMatch} score={p.Score} acc={p.Accuracy:P1}"));
			Log($"t={_t:0} scene={GetTree().CurrentScene?.Name} room='{s.Name}' hostId={s.HostId} isHost={_net.IsHost} lobbyTimer={s.LobbyTimer} phase={s.Phase} timer={(s.CountdownActive ? _net.TimeLeftNow.ToString("0.0") : "-")} map={s.Beatmap?.Display} transfer={_net.TransferProgress:0.00} :: {players}");
		}
	}
}
