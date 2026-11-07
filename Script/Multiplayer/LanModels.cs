using System.Collections.Generic;

// Data shared between LAN peers. Serialized as JSON, so keep these as plain properties.

public enum LanPhase { Lobby = 0, Playing = 1 }

/// <summary>What a player is doing while they're not in a match.</summary>
public enum LanActivity { Lobby = 0, Selecting = 1, Results = 2, Spectating = 3 }

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
	public LanActivity Activity { get; set; }
	/// <summary>Who this player is spectating: -1 nobody, 0 everyone in the match (grid), otherwise a player id.</summary>
	public long WatchTarget { get; set; } = -1;
	/// <summary>Difficulty this player plays instead of the queue item's (same beatmap set). Null = the item's own.</summary>
	public LanBeatmapRef Difficulty { get; set; }
	/// <summary>Mod keys (ModsOperator.Mods) this player plays with. Only <see cref="LanNetwork.AllowedMods"/> are kept.</summary>
	public List<string> Mods { get; set; } = new();
}

/// <summary>A beatmap someone put in the round-robin queue.</summary>
public class LanQueueItem
{
	public int Id { get; set; }
	public long OwnerId { get; set; }
	public string OwnerName { get; set; }
	public LanBeatmapRef Beatmap { get; set; }
	/// <summary>Room-wide mods everyone plays this item with (only <see cref="LanNetwork.GlobalMods"/>: DT / HT).</summary>
	public List<string> Mods { get; set; } = new();
	/// <summary>Song speed for DT / HT (1 when neither is on).</summary>
	public float Speed { get; set; } = 1f;
	/// <summary>Game mode everyone plays this item in (0 Mania, 1 Taiko, 3 Tap!), picked by its owner.</summary>
	public int GameMode { get; set; }
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
	public string Version { get; set; }
	public string Mods { get; set; }
}

public class LanRoomState
{
	public string Name { get; set; }
	/// <summary>Player id holding the host controls (pick map, timer, rename, hand off host).
	/// Can move between players; peer 1 always runs the room itself.</summary>
	public long HostId { get; set; } = 1;
	public string HostName { get; set; }
	public LanPhase Phase { get; set; } = LanPhase.Lobby;
	public List<LanPlayer> Players { get; set; } = new();
	/// <summary>The map being played (Playing) or up next (Lobby: mirrors Queue[0]). Players may swap difficulty.</summary>
	public LanBeatmapRef Beatmap { get; set; }
	/// <summary>Upcoming picks, already sorted round-robin by the server. Queue[0] is up next.</summary>
	public List<LanQueueItem> Queue { get; set; } = new();
	/// <summary>Queue item id that <see cref="Beatmap"/> came from (0 = none).</summary>
	public int CurrentItemId { get; set; }
	public long CurrentOwnerId { get; set; }
	public string CurrentOwnerName { get; set; }
	/// <summary>Room-wide mods + speed of the item up next / being played (copied from its queue item).</summary>
	public List<string> CurrentMods { get; set; } = new();
	public float CurrentSpeed { get; set; } = 1f;
	/// <summary>Game mode of the item up next / being played (copied from its queue item).</summary>
	public int CurrentGameMode { get; set; }
	/// <summary>Owner of the last item that was played; the rotation continues with the next player.</summary>
	public long LastPickOwnerId { get; set; }
	public int NextQueueId { get; set; } = 1;
	/// <summary>Seconds until the match auto-starts ("time till play").</summary>
	public double TimeLeft { get; set; }
	public bool CountdownActive { get; set; }
	/// <summary>Length of the lobby countdown in seconds (host adjustable).</summary>
	public int LobbyTimer { get; set; } = 60;
	public int MatchId { get; set; }
	public List<LanResult> LastResults { get; set; } = new();
	/// <summary>Random id for the room's whole life, even when it moves to another game (LAN players find it again by this).</summary>
	public string RoomToken { get; set; }
}

/// <summary>Players that changed since the last update (whole players), the full player order (which also says who
/// left), and the countdown. Sent instead of the whole room when nothing else about the room changed.</summary>
public class LanPlayersPatch
{
	public List<LanPlayer> Changed { get; set; } = new();
	public List<long> Order { get; set; } = new();
	public double TimeLeft { get; set; }
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
	/// <summary><see cref="LanRoomState.RoomToken"/>, so players can find the room again after it moves.</summary>
	public string room { get; set; }
}

/// <summary>A room seen on the LAN by the lounge.</summary>
public class LanRoomInfo
{
	public string Address { get; set; }
	public int Port { get; set; }
	public LanBeacon Beacon { get; set; }
	public ulong LastSeenMs { get; set; }
}

/// <summary>A room on the online server, as the lounge lists it.</summary>
public class OnlineRoomInfo
{
	public long Id { get; set; }
	public string Name { get; set; }
	public string Host { get; set; }
	public int Players { get; set; }
	public int Phase { get; set; }
	public string Map { get; set; }
	public int Proto { get; set; }
	public string Version { get; set; }
}

/// <summary>The Create Room dialog's choices. Kept here so they survive a trip to song select for the starting beatmap.</summary>
public static class RoomDraft
{
	public static string Name;
	public static bool Online;
	/// <summary>Library index of the starting beatmap (-1 = none).</summary>
	public static int BeatmapIndex = -1;
	/// <summary>Room-wide DT / HT for the starting beatmap (picked in song select).</summary>
	public static List<string> Mods = new();
	public static float Speed = 1f;
	/// <summary>Set when song select returns to the lounge, so the dialog opens again.</summary>
	public static bool Reopen;

	public static void Reset()
	{
		Name = null;
		Online = false;
		BeatmapIndex = -1;
		Mods = new List<string>();
		Speed = 1f;
		Reopen = false;
	}
}
