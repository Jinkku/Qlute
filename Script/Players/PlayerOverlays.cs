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


	public static void OpenReport(string username, string context = "")
	{
		if (!SettingsOperator.SessionConfig.Loggedin) { Notify.Post("Log in to report players."); return; }
		Layer().AddChild(new ReportDialog(username, context));
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
	public static readonly Color Dim = new(1, 1, 1, 0.55f);
	public static readonly Color OnlineDot = new("#67b930");
	public static readonly Color OfflineDot = new(1, 1, 1, 0.25f);

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
