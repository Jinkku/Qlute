using System.Collections.Generic;

// Data shared between LAN peers. Serialized as JSON, so keep these as plain properties.

public enum LanPhase { Lobby = 0, Playing = 1 }

public class LanPlayer
{
	public long Id { get; set; }
	public string Name { get; set; } = "Guest";
	public bool Ready { get; set; }
	public bool HasMap { get; set; }
	public bool InMatch { get; set; }
	public bool Finished { get; set; }
	public bool Quit { get; set; }
	public int Score { get; set; }
	public int Combo { get; set; }
	public int MaxCombo { get; set; }
	public float Accuracy { get; set; } = 1f;
	public int Max { get; set; }
	public int Great { get; set; }
	public int Meh { get; set; }
	public int Bad { get; set; }
}

public class LanBeatmapRef
{
	public int BeatmapID { get; set; } = -1;
	public int BeatmapSetID { get; set; } = -1;
	public string Title { get; set; }
	public string Artist { get; set; }
	public string Version { get; set; }
	public string Mapper { get; set; }
	/// <summary>SHA-256 of the difficulty file, so everyone plays the exact same chart.</summary>
	public string Checksum { get; set; }
	public float Levelrating { get; set; }
	public float LengthMs { get; set; }

	public string Display => $"{Artist} - {Title} [{Version}]";
}

public class LanResult
{
	public int Rank { get; set; }
	public string Name { get; set; }
	public int Score { get; set; }
	public float Accuracy { get; set; }
	public int MaxCombo { get; set; }
	public bool Quit { get; set; }
}

public class LanRoomState
{
	public string Name { get; set; }
	public string HostName { get; set; }
	public LanPhase Phase { get; set; } = LanPhase.Lobby;
	public List<LanPlayer> Players { get; set; } = new();
	public LanBeatmapRef Beatmap { get; set; }
	/// <summary>Seconds until the match auto-starts ("time till play").</summary>
	public double TimeLeft { get; set; }
	public bool CountdownActive { get; set; }
	/// <summary>Length of the lobby countdown in seconds (host adjustable).</summary>
	public int LobbyTimer { get; set; } = 60;
	public int MatchId { get; set; }
	public List<LanResult> LastResults { get; set; } = new();
}

/// <summary>UDP beacon a host broadcasts so lounges on the LAN can list the room.</summary>
public class LanBeacon
{
	public string q { get; set; } = LanNetwork.BeaconTag;
	public int proto { get; set; } = LanNetwork.Protocol;
	public string ver { get; set; }
	public string name { get; set; }
	public string host { get; set; }
	public int port { get; set; }
	public int players { get; set; }
	public int phase { get; set; }
	public string map { get; set; }
}

/// <summary>A room seen on the LAN by the lounge.</summary>
public class LanRoomInfo
{
	public string Address { get; set; }
	public int Port { get; set; }
	public LanBeacon Beacon { get; set; }
	public ulong LastSeenMs { get; set; }
}
