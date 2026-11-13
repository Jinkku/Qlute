using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

/// <summary>
/// Turns a profile bio's osu!-style BBCode (the website's Chikara/bbcode.py) into Godot RichTextLabel BBCode.
/// Plain text is escaped ([lb]); only the known tags become markup, with their arguments checked, and
/// unknown / unbalanced tags stay as literal text, like on the website.
/// </summary>
public static class BioBBCode
{
	private static readonly Regex Token = new(@"(\[/?(?:[a-z]+|\*)(?:=[^\]\n]{0,300})?\])", RegexOptions.IgnoreCase);
	private static readonly Regex TagParts = new(@"^\[(/?)([a-z]+|\*)(?:=(.*))?\]$", RegexOptions.IgnoreCase);
	private static readonly Regex Colour = new(@"^(#[0-9a-f]{3}|#[0-9a-f]{6}|[a-z]{3,20})$", RegexOptions.IgnoreCase);
	private static readonly Regex Link = new(@"^https?://[^\s<>""'\[\]]+$", RegexOptions.IgnoreCase);
	private static readonly HashSet<string> Raw = new() { "img", "code", "c" };
	private static readonly HashSet<string> Block = new() { "centre", "center", "quote", "box", "heading", "notice", "code", "list", "*" };
	private static readonly Dictionary<string, int> Sizes = new() { ["50"] = 50, ["85"] = 85, ["100"] = 100, ["150"] = 150 };

	private record Tok(string Kind, string Name, string Arg, string Raw);

	private static string Esc(string s) => s.Replace("[", "[lb]");

	private static string Url(string v)
	{
		v = (v ?? "").Trim().Trim('"');
		return Link.IsMatch(v) ? v : null;
	}

	/// <param name="baseSize">The label's normal font size ([size=150] is 1.5x this).</param>
	private static (string open, string close)? Open(string name, string arg, int baseSize)
	{
		switch (name)
		{
			case "b": return ("[b]", "[/b]");
			case "i": return ("[i]", "[/i]");
			case "u": return ("[u]", "[/u]");
			case "s": case "strike": return ("[s]", "[/s]");
			case "color":
				return arg != null && Colour.IsMatch(arg.Trim()) ? ($"[color={arg.Trim()}]", "[/color]") : null;
			case "size":
				return arg != null && Sizes.TryGetValue(arg.Trim(), out var pct)
					? ($"[font_size={Math.Max(6, baseSize * pct / 100)}]", "[/font_size]") : null;
			case "centre": case "center": return ("[center]", "[/center]");
			case "url":
				var link = Url(arg);
				return link == null ? null : ($"[url={link}][color=#f3baff][u]", "[/u][/color][/url]");
			case "quote":
				var by = (arg ?? "").Trim().Trim('"');
				return ($"[indent][color=#ffffffb0]{(by.Length > 0 ? $"[b]{Esc(by)} wrote:[/b]\n" : "")}", "[/color][/indent]\n");
			case "spoiler": return ("[bgcolor=#ffffff20]", "[/bgcolor]");
			case "box":
				var title = (arg ?? "").Trim();
				return ($"[b]▸ {Esc(title.Length > 0 ? title : "spoiler")}[/b]\n[indent]", "[/indent]\n");
			case "heading": return ($"[font_size={baseSize * 13 / 10}][b]", "[/b][/font_size]\n");
			case "notice": return ("[bgcolor=#f3baff1f]", "[/bgcolor]\n");
			case "list": return ((arg ?? "").Trim() == "1" ? "[ol type=1]" : "[ul]", (arg ?? "").Trim() == "1" ? "[/ol]\n" : "[/ul]\n");
			case "*": return ("", "\n");
		}
		return null;
	}

	private static List<Tok> Tokens(string text)
	{
		var list = new List<Tok>();
		foreach (var part in Token.Split(text))
		{
			if (part.Length == 0) continue;
			var m = TagParts.Match(part);
			list.Add(m.Success
				? new Tok(m.Groups[1].Value.Length > 0 ? "close" : "open", m.Groups[2].Value.ToLowerInvariant(),
					m.Groups[3].Success ? m.Groups[3].Value : null, part)
				: new Tok("text", null, null, part));
		}
		return list;
	}

	public static string ToGodot(string text, int baseSize = 14)
	{
		text = (text ?? "").Replace("\r\n", "\n").Replace('\r', '\n');
		var tokens = Tokens(text);
		var sb = new StringBuilder();
		var stack = new List<(string name, string close)>();
		bool eatNewline = false;
		for (int i = 0; i < tokens.Count;)
		{
			var t = tokens[i++];
			if (t.Kind == "text")
			{
				var raw = t.Raw;
				if (eatNewline && raw.StartsWith('\n')) raw = raw[1..];
				eatNewline = false;
				var next = i < tokens.Count ? tokens[i] : null;
				if (next != null && next.Kind != "text" && Block.Contains(next.Name) && raw.EndsWith('\n')) raw = raw[..^1];
				sb.Append(Esc(raw));
				continue;
			}
			eatNewline = false;

			if (t.Kind == "open" && (Raw.Contains(t.Name) || (t.Name == "url" && t.Arg == null)))
			{
				int j = -1;
				for (int k = i; k < tokens.Count; k++)
					if (tokens[k].Kind == "close" && tokens[k].Name == t.Name) { j = k; break; }
				if (j >= 0)
				{
					var content = string.Concat(tokens.Skip(i).Take(j - i).Select(x => x.Raw));
					string done = null;
					switch (t.Name)
					{
						case "img":
							var src = Url(content);
							if (src != null) done = $"[url={src}][color=#f3baff][u](image)[/u][/color][/url]";
							break;
						case "url":
							var link = Url(content);
							if (link != null) done = $"[url={link}][color=#f3baff][u]{Esc(link)}[/u][/color][/url]";
							break;
						case "code":
							done = $"[code]{Esc(content.Trim('\n'))}[/code]\n";
							eatNewline = true;
							break;
						default:
							done = $"[code]{Esc(content)}[/code]";
							break;
					}
					if (done != null)
					{
						sb.Append(done);
						i = j + 1;
						continue;
					}
				}
				sb.Append(Esc(t.Raw));
				continue;
			}

			if (t.Kind == "open")
			{
				if (t.Name == "*")
				{
					if (!stack.Any(s => s.name == "list")) { sb.Append(Esc(t.Raw)); continue; }
					while (stack.Count > 0 && stack[^1].name is not ("list" or "*")) Pop(sb, stack);
					if (stack.Count > 0 && stack[^1].name == "*") Pop(sb, stack);
				}
				var tag = Open(t.Name, t.Arg, baseSize);
				if (tag == null) { sb.Append(Esc(t.Raw)); continue; }
				sb.Append(tag.Value.open);
				stack.Add((t.Name, tag.Value.close));
				eatNewline = Block.Contains(t.Name);
				continue;
			}

			// close tag
			string[] alias = t.Name switch
			{
				"center" or "centre" => new[] { "center", "centre" },
				"strike" or "s" => new[] { "strike", "s" },
				_ => new[] { t.Name },
			};
			if (!stack.Any(s => alias.Contains(s.name))) { sb.Append(Esc(t.Raw)); continue; }
			while (stack.Count > 0)
			{
				var name = stack[^1].name;
				Pop(sb, stack);
				if (alias.Contains(name)) break;
			}
			eatNewline = Block.Contains(t.Name);
		}
		while (stack.Count > 0) Pop(sb, stack);
		// A list item's closing newline right before [/ul] would add an empty bullet.
		return Regex.Replace(sb.ToString(), @"\n(\[/(?:ul|ol)\])", "$1");
	}

	private static void Pop(StringBuilder sb, List<(string name, string close)> stack)
	{
		sb.Append(stack[^1].close);
		stack.RemoveAt(stack.Count - 1);
	}
}
