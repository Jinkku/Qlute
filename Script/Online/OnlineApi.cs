using Godot;
using System;
using System.Text.Json.Nodes;

/// <summary>
/// Shared bits for the server's websockets (online chat + online multiplayer).
/// Protocol: Chikara/online/PROTOCOL.md. Paths are versioned (/ws/v1/...) and only ever grow.
/// </summary>
public static class OnlineApi
{
	public const int Protocol = 1;
	/// <summary>A bad login closes with this code; don't keep retrying.</summary>
	public const int AuthFailedCode = 4001;

	/// <summary>Online needs a logged-in account (the server checks the same credentials as score submission).</summary>
	public static bool Available =>
		SettingsOperator.SessionConfig.Loggedin && !string.IsNullOrEmpty(ApiOperator.PasswordHash) && !string.IsNullOrEmpty(ApiOperator.Username);

	/// <summary>ws(s)://server/ws/v1/{path}/ from the "api" setting. QLUTE_WS_BASE overrides it (local testing).</summary>
	public static string Url(string path)
	{
		var b = System.Environment.GetEnvironmentVariable("QLUTE_WS_BASE");
		if (string.IsNullOrEmpty(b))
		{
			b = SettingsOperator.GetSetting("api")?.ToString() ?? "https://qlute.jinkku.moe/";
			if (b.StartsWith("https://")) b = "wss://" + b["https://".Length..];
			else if (b.StartsWith("http://")) b = "ws://" + b["http://".Length..];
		}
		return $"{b.TrimEnd('/')}/ws/v1/{path}/";
	}

	public static WebSocketPeer Connect(string path, out Error err)
	{
		var ws = new WebSocketPeer
		{
			HandshakeHeaders = new[]
			{
				$"Username: {ApiOperator.Username}",
				$"Password: {ApiOperator.PasswordHash}",
				$"User-Agent: {ApiOperator.AgentString}",
			},
			InboundBufferSize = 4 * 1024 * 1024,
			OutboundBufferSize = 4 * 1024 * 1024,
			MaxQueuedPackets = 8192,
		};
		var url = Url(path);
		err = ws.ConnectToUrl(url, url.StartsWith("wss://") ? TlsOptions.Client() : null);
		return ws;
	}

	public static void SendJson(WebSocketPeer ws, JsonObject obj)
	{
		if (ws?.GetReadyState() == WebSocketPeer.State.Open) ws.SendText(obj.ToJsonString());
	}

	public static JsonObject ParseJson(byte[] data)
	{
		try { return JsonNode.Parse(data) as JsonObject; }
		catch { return null; }
	}

	public static string Str(JsonObject o, string key)
	{
		try { return o?[key] is JsonValue v && v.TryGetValue<string>(out var s) ? s : null; }
		catch { return null; }
	}

	public static long Long(JsonObject o, string key, long fallback = 0)
	{
		try
		{
			if (o?[key] is not JsonValue v) return fallback;
			if (v.TryGetValue<long>(out var l)) return l;
			if (v.TryGetValue<double>(out var d)) return (long)d;
		}
		catch { }
		return fallback;
	}

	public static bool Bool(JsonObject o, string key)
	{
		try { return o?[key] is JsonValue v && v.TryGetValue<bool>(out var b) && b; }
		catch { return false; }
	}

	/// <summary>Readable reason for a closed socket.</summary>
	public static string CloseReason(WebSocketPeer ws, string fallback)
	{
		var r = ws?.GetCloseReason();
		return string.IsNullOrWhiteSpace(r) ? fallback : r;
	}
}
