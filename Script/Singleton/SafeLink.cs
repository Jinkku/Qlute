using System;

/// <summary>Links that come from the server (notices, notifications) are only opened when they're plain https web links,
/// so a bad link can't launch a file, a local program or some other URL handler.</summary>
public static class SafeLink
{
	public static bool IsHttps(string url) =>
		!string.IsNullOrWhiteSpace(url)
		&& url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
		&& Uri.TryCreate(url, UriKind.Absolute, out var u)
		&& u.Scheme == Uri.UriSchemeHttps
		&& !string.IsNullOrEmpty(u.Host);
}
