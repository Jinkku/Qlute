using Godot;

/// <summary>
/// Pauses a solo play when the game window loses focus (alt-tab, a popup from another app), so the play isn't lost
/// to misses while you're away. Never during spectating / replays or LAN matches (those can't pause).
/// Setting "pauseonfocusloss" (on by default).
/// </summary>
public static class FocusPause
{
	public const string SettingKey = "pauseonfocusloss";
	private static bool _pending;

	/// <summary>From SettingsOperator when the window loses focus.</summary>
	public static void Lost()
	{
		if (SettingsOperator.inGameplay) _pending = true;
	}

	/// <summary>Called when a play starts: focus lost before it doesn't count.</summary>
	public static void Reset() => _pending = false;

	/// <summary>True once after focus was lost, if this play may pause for it.</summary>
	public static bool Take(bool dead)
	{
		if (!_pending) return false;
		_pending = false;
		if (SettingsOperator.SpectatorMode || LanNetwork.InMatch || dead) return false; // dying: the fail screen opens the menu itself
		return Check.CheckBoolValue(SettingsOperator.GetSetting(SettingKey)?.ToString() ?? "true");
	}
}
