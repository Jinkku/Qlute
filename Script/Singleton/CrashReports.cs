using Godot;
using System;
using System.Text.RegularExpressions;

/// <summary>
/// Crash reporting (the Sentry Godot addon). It no longer starts by itself (sentry/options/auto_init=false):
/// it only starts when the player has agreed to it (setting "crashreports"), and every event has
/// user folder names scrubbed out before it's sent. The log file isn't attached.
/// </summary>
public static class CrashReports
{
	public const string Setting = "crashreports";
	public static bool Enabled => Check.CheckBoolValue(SettingsOperator.GetSetting(Setting)?.ToString() ?? "false");
	public static bool Running { get; private set; }

	private static GodotObject Sdk => Engine.HasSingleton("SentrySDK") ? Engine.GetSingleton("SentrySDK") : null;

	/// <summary>Starts Sentry if the player opted in. Safe to call more than once.</summary>
	public static void Start()
	{
		if (Running || !Enabled) return;
		var sdk = Sdk;
		if (sdk == null) return;
		try
		{
			sdk.Call("init", Callable.From<GodotObject>(Configure));
			Running = true;
			GD.Print("[Qlute] Crash reports on.");
		}
		catch (Exception e)
		{
			GD.PrintErr($"[Qlute] Couldn't start crash reports: {e.Message}");
		}
	}

	/// <summary>Stops sending anything (when the player turns it off).</summary>
	public static void Stop()
	{
		if (!Running) return;
		try { Sdk?.Call("close"); } catch (Exception e) { GD.PrintErr($"[Qlute] Couldn't stop crash reports: {e.Message}"); }
		Running = false;
	}

	public static void SetEnabled(bool on)
	{
		SettingsOperator.SetSetting(Setting, on);
		if (on) Start(); else Stop();
	}

	private static void Configure(GodotObject options)
	{
		options.Set("attach_log", false);              // the log has file paths, beatmap names, etc.
		options.Set("send_default_pii", false);
		// Log lines can't be scrubbed once they're breadcrumbs, so don't turn every print into one.
		options.Set("logger_messages_as_breadcrumbs", false);
		options.Set("before_send", Callable.From<GodotObject, GodotObject>(Scrub));
	}

	// C:/Users/<name>, C:\Users\<name>, /home/<name>, /Users/<name> (macOS). Windows names can have spaces, so the
	// name runs to the next slash; a path at the end of a message may take the words after it too, which is fine.
	private static readonly Regex UserPath = new(
		@"(?<pre>(?:[A-Za-z]:)?[\\/]+(?:Users|home)[\\/]+)(?<name>[^\\/""'<>:|*?\r\n]+)",
		RegexOptions.IgnoreCase | RegexOptions.Compiled);

	public static string ScrubText(string text)
	{
		if (string.IsNullOrEmpty(text)) return text;
		text = UserPath.Replace(text, m => m.Groups["pre"].Value + "<user>");
		var account = ApiOperator.Username;
		if (!string.IsNullOrEmpty(account) && account != "Guest" && account.Length >= 3)
			text = text.Replace(account, "<account>", StringComparison.OrdinalIgnoreCase);
		return text;
	}

	/// <summary>before_send: has to return the same event object (or null to drop it).</summary>
	private static GodotObject Scrub(GodotObject ev)
	{
		try
		{
			var msg = ev.Get("message").AsString();
			if (!string.IsNullOrEmpty(msg)) ev.Set("message", ScrubText(msg));
			if (ev.HasMethod("get_exception_count"))
			{
				int n = ev.Call("get_exception_count").AsInt32();
				for (int i = 0; i < n; i++)
				{
					var v = ev.Call("get_exception_value", i).AsString();
					ev.Call("set_exception_value", i, ScrubText(v));
				}
			}
		}
		catch (Exception e)
		{
			GD.PrintErr($"[Qlute] Crash report scrub failed, dropping it: {e.Message}");
			return null;
		}
		return ev;
	}
}
