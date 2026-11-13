using Godot;

/// <summary>
/// Opens the online player list (top panel "UserList" button) and player profiles (clicking any player card).
/// Both live on one CanvasLayer above the current scene. Styled after the website's user page.
/// </summary>
public static class PlayerOverlays
{
	private static CanvasLayer _layer;
	private static PlayerListPanel _list;
	private static PlayerProfilePanel _profile;

	private static CanvasLayer Layer()
	{
		if (GodotObject.IsInstanceValid(_layer)) return _layer;
		var tree = (SceneTree)Engine.GetMainLoop();
		_layer = new CanvasLayer { Name = "PlayerOverlays", Layer = 90 };
		tree.Root.AddChild(_layer);
		return _layer;
	}

	public static bool ListOpen => GodotObject.IsInstanceValid(_list) && !_list.Closing;

	public static void ToggleList()
	{
		if (ListOpen) { _list.Close(); return; }
		_list = new PlayerListPanel();
		Layer().AddChild(_list);
	}

	public static void OpenProfile(string username)
	{
		if (string.IsNullOrWhiteSpace(username) || username is "Guest" or "None") return;
		if (GodotObject.IsInstanceValid(_profile)) _profile.QueueFree();
		_profile = new PlayerProfilePanel(username);
		Layer().AddChild(_profile);
	}


	private static BeatmapInfo _beatmap;

	/// <summary>A beatmap set's info page (Browse cards, profile favourites), above everything else here.</summary>
	public static void OpenBeatmap(BrowseCatalogLegend set, Texture2D cover = null)
	{
		if (set == null) return;
		if (GodotObject.IsInstanceValid(_beatmap)) _beatmap.QueueFree();
		_beatmap = new BeatmapInfo(set, cover);
		Layer().AddChild(_beatmap);
	}

	public static void OpenReport(string username, string context = "")
	{
		if (!SettingsOperator.SessionConfig.Loggedin) { Notify.Post("Log in to report players."); return; }
		Layer().AddChild(new ReportDialog(username, context));
	}

	/// <summary>A pop-up message above everything (e.g. the account was just restricted).</summary>
	public static void ShowMessage(string title, string text, Color accent)
	{
		var layer = Layer();
		layer.AddChild(new MessageDialog(title, text, accent));
	}

	private static AdminPanel _admin;

	/// <summary>Team only (ModerationApi.IsStaff). username: jump straight to that player.</summary>
	public static void OpenAdmin(string username = null)
	{
		if (!ModerationApi.IsStaff) return;
		if (GodotObject.IsInstanceValid(_admin))
		{
			if (!string.IsNullOrEmpty(username)) _admin.OpenUser(username);
			_admin.GetParent().MoveChild(_admin, -1);
			return;
		}
		_admin = new AdminPanel(username);
		Layer().AddChild(_admin);
	}

	public static void OpenAdminTab(string tab)
	{
		OpenAdmin();
		if (GodotObject.IsInstanceValid(_admin)) _admin.CallDeferred(AdminPanel.MethodName.OpenTab, tab);
	}

	public static void ToggleAdmin()
	{
		if (GodotObject.IsInstanceValid(_admin)) _admin.Close();
		else OpenAdmin();
	}

	public static readonly Color Page = new("#221E27");
	public static readonly Color Box = new(0, 0, 0, 0x85 / 255f);
	public static readonly Color TitleStrip = new(0, 0, 0, 0x40 / 255f);
	public static readonly Color StatusStrip = new("#f3baff2a");
	public static readonly Color Card = new("#3a3342"); 
	public static readonly Color CardHover = new("#463e50");
	public static readonly Color Pill = new(0, 0, 0, 0x28 / 255f);
	public static readonly Color ModPill = new(1, 1, 1, 0x28 / 255f);
	public static readonly Color MedalBox = new(0, 0, 0, 0x20 / 255f);
	public static readonly Color Dim = new("#ffffff99");
	public static readonly Color OnlineDot = new("#67b930");
	public static readonly Color OfflineDot = new(1, 1, 1, 0.25f);

	// ---- the website's 2026 refresh (profile.css / beatmap.css / catalog.css)
	public static readonly Color Accent = new("#f3baff");
	public static readonly Color AccentHover = new("#f7cfff");
	/// <summary>Dark text on accent buttons (the page colour).</summary>
	public static readonly Color Ink = new("#221E27");
	public static readonly Color Tile = new("#ffffff10");
	public static readonly Color Tile2 = new("#ffffff1a");
	public static readonly Color Quiet = new("#ffffff1c");
	public static readonly Color QuietHover = new("#ffffff30");
	public static readonly Color CardBg = new("#2d2733");
	public static readonly Color Perfect = new("#c08bff"), Great = new("#6ec1ff"), Meh = new("#ffd76a"), Miss = new("#ff7a7a");
	public static readonly Color Presence = new("#6ee7a0");

	public static Color StatusAccent(int status) => status switch
	{
		1 => new Color("#67b930"),
		2 or 4 => new Color("#c5c220"),
		_ => new Color("#d47037"),
	};

	/// <summary>hsl() like CSS (h in degrees, s / l 0-1).</summary>
	public static Color Hsl(float h, float s, float l, float a = 1)
	{
		h = Mathf.PosMod(h, 360f) / 360f;
		float q = l < 0.5f ? l * (1 + s) : l + s - l * s, p = 2 * l - q;
		float Ch(float t)
		{
			t = Mathf.PosMod(t, 1f);
			return t < 1 / 6f ? p + (q - p) * 6 * t : t < 0.5f ? q : t < 2 / 3f ? p + (q - p) * (2 / 3f - t) * 6 : p;
		}
		return new Color(Ch(h + 1 / 3f), Ch(h), Ch(h - 1 / 3f), a);
	}

	/// <summary>The website's level colour (beatmapcard.js lvStyle): blue to red over Lv 1-55, red to magenta to Lv 69; Lv 70+ is black.</summary>
	public static bool LevelIsBlack(double level) => level >= 70;
	public static Color LevelColor(double level, float lightness = 0.72f)
	{
		if (LevelIsBlack(level)) return Colors.White;
		var hue = level <= 55 ? 200 - level * 200 / 55 : 360 - (level - 55) * 4;
		return Hsl((float)System.Math.Round(hue), 0.75f, lightness);
	}

	/// <summary>StyleBoxFlat with an optional border (website "1px solid ...").</summary>
	public static StyleBoxFlat Style(Color bg, int radius, int marginX, int marginY, Color border, int borderWidth = 1)
	{
		var s = Style(bg, radius, marginX, marginY);
		s.BorderColor = border;
		s.SetBorderWidthAll(borderWidth);
		return s;
	}

	/// <summary>Rounded website pill button (.pf-btn / .bm-button): primary = lavender with dark bold text, else quiet.</summary>
	public static Button PillButton(string text, bool primary = false, string tooltip = null, int size = 14, Color? bg = null, Color? hover = null)
	{
		var b = new Button { Text = text, TooltipText = tooltip ?? "", FocusMode = Control.FocusModeEnum.None, MouseDefaultCursorShape = Control.CursorShape.PointingHand };
		b.AddThemeFontSizeOverride("font_size", size);
		PaintPill(b, primary, bg, hover);
		b.AddThemeConstantOverride("icon_max_width", 16);
		b.AddThemeConstantOverride("h_separation", 7);
		return b;
	}

	public static void PaintPill(Button b, bool primary, Color? bg = null, Color? hover = null, Color? border = null)
	{
		var normal = bg ?? (primary ? Accent : Quiet);
		var over = hover ?? (primary ? AccentHover : QuietHover);
		StyleBox S(Color c) => border is { } line ? new PillBox(c, line, 16, 8) : Style(c, 99, 16, 8);
		b.AddThemeStyleboxOverride("normal", S(normal));
		b.AddThemeStyleboxOverride("hover", S(over));
		b.AddThemeStyleboxOverride("pressed", S(over));
		b.AddThemeStyleboxOverride("hover_pressed", S(over));
		b.AddThemeStyleboxOverride("disabled", S(new Color(normal, normal.A * 0.6f)));
		b.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
		var text = primary ? Ink : Colors.White;
		foreach (var key in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_hover_pressed_color", "font_focus_color" })
			b.AddThemeColorOverride(key, text);
		b.AddThemeColorOverride("font_disabled_color", new Color(text, 0.6f));
		if (primary) b.AddThemeFontOverride("font", Bold);
		else b.RemoveThemeFontOverride("font");
	}

	/// <summary>Uppercase dim caption over a big white value (.pf-tile / .bm-stat).</summary>
	public static PanelContainer StatTile(string caption, string value, int valueSize = 21, Color? valueColor = null, string sub = null)
	{
		var p = Panel(Tile, 12, 16, 14);
		var v = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		v.AddThemeConstantOverride("separation", 4);
		p.AddChild(v);
		v.AddChild(Caption(caption));
		var val = Text(value, valueSize, valueColor, bold: true, trim: true);
		val.TooltipText = value;
		val.MouseFilter = Control.MouseFilterEnum.Pass;
		v.AddChild(val);
		if (!string.IsNullOrEmpty(sub)) v.AddChild(Text(sub, 12, Dim));
		return p;
	}

	/// <summary>Small uppercase, letter-spaced dim label (.pf-tile-label, .bm-stat p, .bm-meta dt).</summary>
	public static Label Caption(string text, Color? color = null) => Text(text.ToUpperInvariant(), 11, color ?? Dim);

	/// <summary>
	/// A rounded bar track (level bar, hit distribution, preview progress): fill the returned row with
	/// stretch-ratio parts. The parts are clipped by an opaque inner mask: ClipChildren on the translucent
	/// track itself would also fade the parts to the track's alpha.
	/// </summary>
	public static PanelContainer Track(int height, Color track, out HBoxContainer row, int gap = 0)
	{
		var bar = new PanelContainer { CustomMinimumSize = new Vector2(0, height) };
		bar.AddThemeStyleboxOverride("panel", Style(track, height / 2, 0, 0));
		var mask = new PanelContainer { ClipChildren = CanvasItem.ClipChildrenMode.Only, MouseFilter = Control.MouseFilterEnum.Ignore };
		mask.AddThemeStyleboxOverride("panel", Style(Colors.White, height / 2, 0, 0));
		bar.AddChild(mask);
		row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		row.AddThemeConstantOverride("separation", gap);
		mask.AddChild(row);
		return bar;
	}

	// ---- small icons drawn from SVG (the emoji font would otherwise turn ▶ / ⏱ into colour emoji)
	private static readonly System.Collections.Generic.Dictionary<string, ImageTexture> Icons = new();

	public static ImageTexture SvgIcon(string body, string color = "#fff")
	{
		var key = body + color;
		if (Icons.TryGetValue(key, out var tex)) return tex;
		var img = new Image();
		img.LoadSvgFromString($"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\" width=\"24\" height=\"24\" fill=\"{color}\" stroke=\"{color}\">{body}</svg>", 2f);
		return Icons[key] = ImageTexture.CreateFromImage(img);
	}

	public const string PlayGlyph = "<path stroke=\"none\" d=\"M7 4.5v15l12.5-7.5z\"/>";
	public const string PauseGlyph = "<path stroke=\"none\" d=\"M6 4h4.5v16H6zM13.5 4H18v16h-4.5z\"/>";
	public const string ClockGlyph = "<circle cx=\"12\" cy=\"13\" r=\"8\" fill=\"none\" stroke-width=\"2.4\"/><path fill=\"none\" stroke-width=\"2.4\" stroke-linecap=\"round\" d=\"M12 9v4.5l3 2M9.5 2.5h5\"/>";

	/// <summary>A small icon as a control (for chips / rows).</summary>
	public static TextureRect IconRect(string glyph, Color color, float size = 13)
	{
		return new TextureRect
		{
			Texture = SvgIcon(glyph), Modulate = color, CustomMinimumSize = new Vector2(size, size),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			SizeFlagsVertical = Control.SizeFlags.ShrinkCenter, MouseFilter = Control.MouseFilterEnum.Ignore,
		};
	}

	/// <summary>Segmented tabs (.pb-tabs / .cs-options): returns the bar; Add buttons with <see cref="SegmentButton"/>.</summary>
	public static PanelContainer SegmentBar(out HBoxContainer row)
	{
		var bar = Panel(new Color("#00000033"), 10, 3, 3);
		bar.SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin;
		row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 2);
		bar.AddChild(row);
		return bar;
	}

	public static Button SegmentButton(string text)
	{
		var b = new Button { Text = text, FocusMode = Control.FocusModeEnum.None, MouseDefaultCursorShape = Control.CursorShape.PointingHand };
		b.AddThemeFontSizeOverride("font_size", 14);
		PaintSegment(b, false);
		return b;
	}

	public static void PaintSegment(Button b, bool on)
	{
		var normal = Style(on ? Quiet : Colors.Transparent, 8, 14, 6);
		if (on) { normal.BorderColor = Accent; normal.BorderWidthBottom = 2; }
		var hover = on ? normal : Style(Tile, 8, 14, 6);
		b.AddThemeStyleboxOverride("normal", normal);
		b.AddThemeStyleboxOverride("hover", hover);
		b.AddThemeStyleboxOverride("pressed", normal);
		b.AddThemeStyleboxOverride("hover_pressed", normal);
		b.AddThemeStyleboxOverride("disabled", Style(Colors.Transparent, 8, 14, 6));
		b.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
		var c = on ? Colors.White : new Color("#ffffff8c");
		b.AddThemeColorOverride("font_color", c);
		b.AddThemeColorOverride("font_hover_color", Colors.White);
		b.AddThemeColorOverride("font_pressed_color", Colors.White);
		b.AddThemeColorOverride("font_disabled_color", new Color(1, 1, 1, 0.22f));
		if (on) b.AddThemeFontOverride("font", Bold); else b.RemoveThemeFontOverride("font");
	}

	/// <summary>Picture with rounded corners and a border drawn on top (.pf-avatar img, .bm-pfp).</summary>
	public static Control FramedPicture(string url, float size, int radius, Color border, int borderWidth)
	{
		var box = new Control { CustomMinimumSize = new Vector2(size, size), MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
		var pic = Picture(url, size, radius);
		pic.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		box.AddChild(pic);
		if (borderWidth > 0)
		{
			var ring = new Panel { MouseFilter = Control.MouseFilterEnum.Ignore };
			var s = Style(Colors.Transparent, radius, 0, 0, border, borderWidth);
			s.DrawCenter = false;
			ring.AddThemeStyleboxOverride("panel", s);
			ring.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
			box.AddChild(ring);
		}
		return box;
	}

	/// <summary>A gradient fill (CSS linear-gradient / radial-gradient) for a TextureRect that covers its parent.</summary>
	public static TextureRect GradientRect(Color[] colors, float[] offsets, Vector2 from, Vector2 to, bool radial = false)
	{
		var g = new Gradient { Colors = colors, Offsets = offsets };
		return new TextureRect
		{
			Texture = new GradientTexture2D
			{
				Gradient = g, Width = 128, Height = 128, FillFrom = from, FillTo = to,
				Fill = radial ? GradientTexture2D.FillEnum.Radial : GradientTexture2D.FillEnum.Linear,
			},
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.Scale,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
	}

	/// <summary>Columns for a CSS "repeat(auto-fill, minmax(min, 1fr))" grid of the given width.</summary>
	public static int AutoColumns(float width, float min, float gap) => System.Math.Max(1, (int)((width + gap) / (min + gap)));

	public static Color GradeColor(string grade) => grade switch
	{
		"SS" => new Color("#983aaf"),
		"S" => new Color("#3b78d3"),
		"A" => new Color("#67b930"),
		"B" => new Color("#c5c220"),
		"C" => new Color("#d47037"),
		"D" => new Color("#d43737"),
		_ => new Color("#555055"),
	};

	public static StyleBoxFlat Style(Color bg, int radius = 0, int marginX = 15, int marginY = 10)
	{
		var s = new StyleBoxFlat { BgColor = bg };
		s.SetCornerRadiusAll(radius);
		// Godot's default 8 segments per corner makes circles and pills look polygonal; the website's are smooth.
		s.CornerDetail = Mathf.Clamp(radius, 8, 24);
		s.AntiAliasing = true;
		s.ContentMarginLeft = s.ContentMarginRight = marginX;
		s.ContentMarginTop = s.ContentMarginBottom = marginY;
		return s;
	}

	public static PanelContainer Panel(Color bg, int radius = 0, int marginX = 15, int marginY = 10)
	{
		var p = new PanelContainer();
		p.AddThemeStyleboxOverride("panel", Style(bg, radius, marginX, marginY));
		return p;
	}

	private const string BoldFontPath = "res://Fonts/font-bold.otf"; // Comfortaa Bold, OFL (Fonts/Comfortaa-OFL.txt)
	private static FontVariation _bold;
	public static Font Bold
	{
		get
		{
			if (_bold != null) return _bold;
			// A real bold font: faking it with VariationEmbolden smears the MSDF glyphs and the text looks blurry.
			var bold = ResourceLoader.Exists(BoldFontPath) ? GD.Load<FontFile>(BoldFontPath) : null;
			var regular = ThemeDB.GetProjectTheme()?.DefaultFont ?? ThemeDB.FallbackFont;
			_bold = bold != null
				? new FontVariation { BaseFont = bold, Fallbacks = new Godot.Collections.Array<Font> { regular } } // regular brings the emoji fallback
				: new FontVariation { BaseFont = regular, VariationEmbolden = 0.9f };
			return _bold;
		}
	}

	/// <summary>Label. trim: shrink with "..." to fit (only for labels that get their width from an expanding parent).</summary>
	public static Label Text(string text, int size = 14, Color? color = null, bool wrap = false, bool bold = false, bool trim = false)
	{
		var l = new Label { Text = text, TextOverrunBehavior = trim ? TextServer.OverrunBehavior.TrimEllipsis : TextServer.OverrunBehavior.NoTrimming, MouseFilter = Control.MouseFilterEnum.Ignore };
		if (trim) l.ClipText = true;
		if (wrap) l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		l.AddThemeFontSizeOverride("font_size", size);
		if (color != null) l.AddThemeColorOverride("font_color", color.Value);
		if (bold) l.AddThemeFontOverride("font", Bold);
		return l;
	}

	/// <summary>Small rounded pill (.pp / .mod / .rank).</summary>
	public static PanelContainer PillLabel(string text, Color bg, int size = 13, float minWidth = 0)
	{
		var p = Panel(bg, 5, 10, 4);
		p.CustomMinimumSize = new Vector2(minWidth, 0);
		p.MouseFilter = Control.MouseFilterEnum.Ignore;
		var l = Text(text, size, bold: true);
		l.HorizontalAlignment = HorizontalAlignment.Center;
		l.VerticalAlignment = VerticalAlignment.Center;
		l.TextOverrunBehavior = TextServer.OverrunBehavior.NoTrimming;
		p.AddChild(l);
		return p;
	}

	/// <summary>Picture with the website's 5px corners; guest picture until the real one arrives.</summary>
	public static PanelContainer Picture(string url, float size, int radius = 5)
	{
		var frame = new PanelContainer { ClipChildren = CanvasItem.ClipChildrenMode.Only, CustomMinimumSize = new Vector2(size, size), MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
		frame.AddThemeStyleboxOverride("panel", Style(Colors.White, radius, 0, 0));
		var pic = new TextureRect
		{
			Texture = GD.Load<Texture2D>("res://Resources/System/guest.png"),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		frame.AddChild(pic);
		PlayerApi.LoadPicture(url, tex => { if (GodotObject.IsInstanceValid(pic)) pic.Texture = tex; });
		return frame;
	}

	/// <summary>Website wording: what they're doing, or "Last Seen 12 Hours Ago".</summary>
	public static string StatusLine(PlayerApi.Player p) =>
		p.Online ? (string.IsNullOrWhiteSpace(p.Status) ? "Online" : p.Status)
			: p.LastSeen > 0 ? $"Last Seen {PlayerApi.Ago(p.LastSeen)}" : "New player <3";

	/// <summary>The ".infoboxtitle" strip: small caption on the left, optional controls on the right.</summary>
	public static HBoxContainer TitleBar(Control parent, string caption)
	{
		var strip = Panel(TitleStrip, 0, 15, 8);
		strip.CustomMinimumSize = new Vector2(0, 40);
		parent.AddChild(strip);
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 8);
		strip.AddChild(row);
		var l = Text(caption, 13);
		l.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		l.VerticalAlignment = VerticalAlignment.Center;
		row.AddChild(l);
		return row;
	}

	/// <summary>Flat text button that fits the strips.</summary>
	public static Button FlatButton(string text, string tooltip = null)
	{
		var b = new Button { Text = text, TooltipText = tooltip ?? "", Flat = true, FocusMode = Control.FocusModeEnum.None, MouseDefaultCursorShape = Control.CursorShape.PointingHand };
		b.AddThemeFontSizeOverride("font_size", 13);
		b.AddThemeStyleboxOverride("normal", Style(Pill, 5, 10, 4));
		b.AddThemeStyleboxOverride("hover", Style(ModPill, 5, 10, 4));
		b.AddThemeStyleboxOverride("pressed", Style(ModPill, 5, 10, 4));
		b.AddThemeStyleboxOverride("disabled", Style(Pill, 5, 10, 4));
		return b;
	}
}
