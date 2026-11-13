using Godot;
using System.Collections.Generic;
using System.Linq;


public partial class PlayerProfilePanel : Control
{
	private readonly string _username;
	private PanelContainer _panel;
	private VBoxContainer _page;
	private Node _scene;
	
	private readonly List<(GridContainer grid, float min, float gap, int max)> _autoGrids = new();
	private GridContainer _scoreCols;

	private const int SidePad = 15, Gap = 12;

	public PlayerProfilePanel() : this("") { }
	public PlayerProfilePanel(string username) => _username = username.Trim();

	public override void _Ready()
	{
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		MouseFilter = MouseFilterEnum.Stop;

		var dim = new ColorRect { Color = new Color(0, 0, 0, 0.6f), MouseFilter = MouseFilterEnum.Stop };
		dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		dim.GuiInput += e => { if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true }) Close(); };
		AddChild(dim);

		var center = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
		center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(center);

		var view = GetViewportRect().Size;
		_panel = new PanelContainer { CustomMinimumSize = new Vector2(Mathf.Min(1160, view.X - 40), Mathf.Min(820, view.Y - 60)) };
		_panel.AddThemeStyleboxOverride("panel", PlayerOverlays.Style(PlayerOverlays.Page, 0, 0, 0));
		center.AddChild(_panel);

		var outer = new VBoxContainer();
		outer.AddThemeConstantOverride("separation", 0);
		_panel.AddChild(outer);

		var bar = PlayerOverlays.TitleBar(outer, "user page");
		var web = PlayerOverlays.FlatButton("view on website");
		web.Pressed += () => OS.ShellOpen(PlayerApi.WebProfileUrl(_username));
		bar.AddChild(web);
		var close = PlayerOverlays.FlatButton("✕", "Close (Esc)");
		close.Pressed += Close;
		bar.AddChild(close);

		var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
		outer.AddChild(scroll);
		_page = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		_page.AddThemeConstantOverride("separation", Gap);
		scroll.AddChild(_page);
		Section(_page).AddChild(PlayerOverlays.Text($"Loading {_username}'s profile...", 14, PlayerOverlays.Dim));

		// Same as the chat overlay: keep the scene underneath from reacting while this is open.
		_scene = GetTree().CurrentScene;
		_scene?.SetProcessInput(false);

		Modulate = new Color(1, 1, 1, 0);
		CreateTween().SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out).TweenProperty(this, "modulate:a", 1f, 0.2f);
		Load();
	}

	public override void _ExitTree()
	{
		if (IsInstanceValid(_scene)) _scene.SetProcessInput(true);
		PreviewPlayer.Stop();
	}

	public override void _Input(InputEvent e)
	{
		if (e.IsActionPressed("ui_cancel")) { Close(); GetViewport().SetInputAsHandled(); }
	}

	private Button _friend;
	private int _shownFriends = -1;

	public override void _Process(double delta)
	{
		if (IsInstanceValid(_friend) && _shownFriends != PlayerApi.FriendsVersion) ShowFriendButton();
	}

	public override void _PhysicsProcess(double delta)
	{
		// From the panel, not the grids: a grid's width follows its columns, which would feed back.
		var width = _panel.Size.X - SidePad * 2 - 12;
		foreach (var (grid, min, gap, max) in _autoGrids)
		{
			if (!IsInstanceValid(grid)) continue;
			var columns = PlayerOverlays.AutoColumns(width, min, gap);
			if (max > 0) columns = System.Math.Min(columns, System.Math.Max(1, grid.GetChildCount()));
			if (grid.Columns != columns) grid.Columns = columns;
		}
		if (IsInstanceValid(_scoreCols))
		{
			var columns = _panel.Size.X >= 900 ? 2 : 1; // profile.css: one column under 900px
			if (_scoreCols.Columns != columns) _scoreCols.Columns = columns;
		}
	}

	private void ShowFriendButton()
	{
		_shownFriends = PlayerApi.FriendsVersion;
		bool added = PlayerApi.Friends.TryGetValue(_username, out var mutual);
		_friend.Text = added ? (mutual ? "Mutual friends ✓" : "Friend ✓") : "Add friend";
		_friend.TooltipText = added ? "Remove from your friends" : $"Add {_username} to your friends list";
		PlayerOverlays.PaintPill(_friend, primary: !added); // .pf-btn, ".on" once added
	}

	private async void ToggleFriend()
	{
		_friend.Disabled = true;
		var error = await PlayerApi.SetFriend(_username, !PlayerApi.IsFriend(_username));
		if (!IsInstanceValid(this)) return;
		_friend.Disabled = false;
		if (error != null) Notify.Post(error, Type: NotificationIcons.NotificationType.Warning);
		ShowFriendButton();
	}

	private bool _closing;
	private void Close()
	{
		if (_closing) return;
		_closing = true;
		var t = CreateTween().SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
		t.TweenProperty(this, "modulate:a", 0f, 0.15f);
		t.TweenCallback(Callable.From(QueueFree));
	}

	private async void Load()
	{
		var (p, error) = await PlayerApi.FetchProfile(_username);
		if (!IsInstanceValid(this)) return;
		foreach (var c in _page.GetChildren()) c.QueueFree();
		if (p == null)
		{
			Section(_page).AddChild(PlayerOverlays.Text(error ?? "Couldn't load this profile.", 14, PlayerOverlays.Dim, wrap: true));
			return;
		}
		if (p.Restricted)
		{
			var box = Section(_page, 40);
			var msg = new VBoxContainer();
			box.AddChild(msg);
			var face = PlayerOverlays.Text("o-o", 32, bold: true);
			face.HorizontalAlignment = HorizontalAlignment.Center;
			msg.AddChild(face);
			var t = PlayerOverlays.Text("This profile has been privated QnQ", 16, bold: true);
			t.HorizontalAlignment = HorizontalAlignment.Center;
			msg.AddChild(t);
			return;
		}
		Hero(p);
		Stats(p);
		Bio(p);
		Medals(p.Medals);
		ScoreColumns(p);
		Beatmaps(p);
		Favorites(p.Username);
	}

	// =====================================================================
	// Building blocks
	// =====================================================================

	/// <summary>A website ".infobox .pf-section": dark box, 15px sides, 20px top / bottom.</summary>
	private static PanelContainer Section(Control parent, int marginY = 20)
	{
		var box = PlayerOverlays.Panel(PlayerOverlays.Box, 0, SidePad, marginY);
		parent.AddChild(box);
		return box;
	}

	private static VBoxContainer SectionBody(PanelContainer box, int separation = 12)
	{
		var v = new VBoxContainer();
		v.AddThemeConstantOverride("separation", separation);
		box.AddChild(v);
		return v;
	}

	private static Label SectionTitle(string text) => PlayerOverlays.Text(text, 17, bold: true);

	/// <summary>".pf-sec-head": title on the left, dim note on the right.</summary>
	private static Label SectionHead(VBoxContainer parent, string title, string note = "", int titleSize = 17)
	{
		var head = new HBoxContainer();
		head.AddThemeConstantOverride("separation", 10);
		var t = PlayerOverlays.Text(title, titleSize, bold: true);
		t.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		head.AddChild(t);
		var n = PlayerOverlays.Text(note, 13, PlayerOverlays.Dim);
		n.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		head.AddChild(n);
		parent.AddChild(head);
		return n;
	}

	/// <summary>fit: CSS auto-fit (columns never outnumber the items, so they stretch) instead of auto-fill.</summary>
	private GridContainer AutoGrid(Control parent, float min, int gap = 10, bool fit = false)
	{
		var grid = new GridContainer { Columns = 1, SizeFlagsHorizontal = SizeFlags.ExpandFill };
		grid.AddThemeConstantOverride("h_separation", gap);
		grid.AddThemeConstantOverride("v_separation", gap);
		parent.AddChild(grid);
		_autoGrids.Add((grid, min, gap, fit ? 1 : 0));
		return grid;
	}

	private static Control Circle(float size, Color fill, Color? border = null, int borderWidth = 0)
	{
		var c = new Panel { CustomMinimumSize = new Vector2(size, size), MouseFilter = MouseFilterEnum.Ignore, SizeFlagsVertical = SizeFlags.ShrinkCenter };
		var s = PlayerOverlays.Style(fill, Mathf.CeilToInt(size / 2), 0, 0);
		if (border is { } b) { s.BorderColor = b; s.SetBorderWidthAll(borderWidth); }
		c.AddThemeStyleboxOverride("panel", s);
		return c;
	}

	private static string SettingsUrl() => $"{PlayerApi.Base.TrimEnd('/')}/settings";

	// =====================================================================
	// Hero (.pf-hero): banner or gradient, avatar, name + badge, presence, rank block, actions
	// =====================================================================

	private void Hero(PlayerApi.Profile p)
	{
		var hero = new PanelContainer { ClipContents = true };
		hero.AddThemeStyleboxOverride("panel", PlayerOverlays.Style(PlayerOverlays.Box, 0, 0, 0));
		_page.AddChild(hero);

		// No banner: radial lavender glow from the top left over a plum diagonal.
		hero.AddChild(PlayerOverlays.GradientRect(new[] { new Color("#3a2a45"), new Color("#00000085"), new Color("#00000085") },
			new[] { 0f, 0.7f, 1f }, Vector2.Zero, Vector2.One));
		hero.AddChild(PlayerOverlays.GradientRect(new[] { new Color("#f3baff33"), new Color("#f3baff00") },
			new[] { 0f, 1f }, Vector2.Zero, new Vector2(0.66f, 0.77f), radial: true));
		if (!string.IsNullOrEmpty(p.Banner))
		{
			var banner = new TextureRect
			{
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
				MouseFilter = MouseFilterEnum.Ignore,
			};
			hero.AddChild(banner);
			var shadeSide = PlayerOverlays.GradientRect(new[] { new Color("#1b1620eb"), new Color("#1b1620b3"), new Color("#1b162066") },
				new[] { 0f, 0.45f, 1f }, Vector2.Zero, new Vector2(1, 0));
			var shadeBottom = PlayerOverlays.GradientRect(new[] { new Color("#1b1620cc"), new Color("#1b162000") },
				new[] { 0f, 0.6f }, new Vector2(0, 1), Vector2.Zero);
			shadeSide.Visible = shadeBottom.Visible = false;
			hero.AddChild(shadeSide);
			hero.AddChild(shadeBottom);
			PlayerApi.LoadPicture(p.Banner, tex =>
			{
				if (!IsInstanceValid(banner)) return;
				banner.Texture = tex;
				shadeSide.Visible = shadeBottom.Visible = true;
			});
		}

		var margin = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore };
		margin.AddThemeConstantOverride("margin_left", SidePad + 8);
		margin.AddThemeConstantOverride("margin_right", SidePad + 8);
		margin.AddThemeConstantOverride("margin_top", 28);
		margin.AddThemeConstantOverride("margin_bottom", 28);
		hero.AddChild(margin);
		var v = new VBoxContainer();
		v.AddThemeConstantOverride("separation", 14);
		margin.AddChild(v);

		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 22);
		v.AddChild(row);

		// avatar: 120px, 22px corners, faint border, shadow, green dot when online
		var avatar = new Control { CustomMinimumSize = new Vector2(120, 120), SizeFlagsVertical = SizeFlags.ShrinkCenter, MouseFilter = MouseFilterEnum.Ignore };
		var shadow = new Panel { MouseFilter = MouseFilterEnum.Ignore };
		var shadowStyle = PlayerOverlays.Style(new Color("#00000070"), 22, 0, 0);
		shadowStyle.ShadowColor = new Color("#00000070");
		shadowStyle.ShadowSize = 18;
		shadowStyle.ShadowOffset = new Vector2(0, 10);
		shadow.AddThemeStyleboxOverride("panel", shadowStyle);
		shadow.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		avatar.AddChild(shadow);
		var pic = PlayerOverlays.FramedPicture(p.Pfp, 120, 22, new Color("#ffffff30"), 3);
		pic.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		avatar.AddChild(pic);
		if (p.Online)
		{
			var dot = Circle(20, PlayerOverlays.Presence, new Color("#2a2230"), 4);
			dot.TooltipText = "online";
			dot.MouseFilter = MouseFilterEnum.Pass;
			dot.Position = new Vector2(104, 104);
			avatar.AddChild(dot);
		}
		row.AddChild(avatar);

		var ident = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkCenter };
		ident.AddThemeConstantOverride("separation", 10);
		row.AddChild(ident);
		var nameRow = new HBoxContainer();
		nameRow.AddThemeConstantOverride("separation", 12);
		ident.AddChild(nameRow);
		var name = PlayerOverlays.Text(p.Username, 34, bold: true);
		name.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, string.IsNullOrEmpty(p.Banner) ? 0 : 0.7f));
		name.AddThemeConstantOverride("shadow_offset_y", 2);
		name.AddThemeConstantOverride("shadow_outline_size", 6);
		nameRow.AddChild(name);
		if (p.TeamRole is "mod" or "admin")
		{
			// .team-badge
			var badge = PlayerOverlays.PillLabel(p.TeamRole == "admin" ? "Team Admin" : "Team Moderator",
				new Color(p.TeamRole == "admin" ? "#6620d6" : "#983aaf"), 14);
			badge.AddThemeStyleboxOverride("panel", PlayerOverlays.Style(new Color(p.TeamRole == "admin" ? "#6620d6" : "#983aaf"), 5, 12, 5));
			badge.SizeFlagsVertical = SizeFlags.ShrinkCenter;
			nameRow.AddChild(badge);
		}

		var meta = new HFlowContainer();
		meta.AddThemeConstantOverride("h_separation", 16);
		meta.AddThemeConstantOverride("v_separation", 8);
		ident.AddChild(meta);
		var presence = new HBoxContainer();
		presence.AddThemeConstantOverride("separation", 6);
		if (p.Online) presence.AddChild(Circle(8, PlayerOverlays.Presence, new Color("#6ee7a033"), 0));
		presence.AddChild(PlayerOverlays.Text(PlayerOverlays.StatusLine(p).Replace("Last Seen", "Last seen"), 14,
			p.Online ? new Color("#9ff0c0") : PlayerOverlays.Dim));
		meta.AddChild(presence);
		if (p.Joined > 0)
		{
			var joined = PlayerOverlays.Text($"Joined {System.DateTimeOffset.FromUnixTimeSeconds(p.Joined).ToLocalTime():d MMMM yyyy}", 14, PlayerOverlays.Dim);
			joined.TooltipText = PlayerApi.Ago(p.Joined);
			joined.MouseFilter = MouseFilterEnum.Pass;
			meta.AddChild(joined);
		}

		// .pf-rank-block
		var rank = PlayerOverlays.Panel(new Color("#00000040"), 14, 18, 12);
		rank.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		row.AddChild(rank);
		var rv = new VBoxContainer();
		rv.AddThemeConstantOverride("separation", 2);
		rank.AddChild(rv);
		Label Right(Label l) { l.HorizontalAlignment = HorizontalAlignment.Right; return l; }
		rv.AddChild(Right(PlayerOverlays.Caption("Global rank")));
		rv.AddChild(Right(PlayerOverlays.Text(p.Rank > 0 ? $"#{p.Rank:N0}" : "#?", 32, bold: true)));
		rv.AddChild(Right(PlayerOverlays.Text($"{p.Points:N0} pp", 15, PlayerOverlays.Accent, bold: true)));

		// .pf-actions
		var actions = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
		actions.AddThemeConstantOverride("separation", 8);
		var self = p.Username == ApiOperator.Username;
		if (SettingsOperator.SessionConfig.Loggedin && !self)
		{
			_friend = PlayerOverlays.PillButton("Add friend", primary: true);
			_friend.Pressed += ToggleFriend;
			actions.AddChild(_friend);
			if (PlayerApi.FriendsVersion == 0) PlayerApi.RefreshFriends(); // online chat (which loads it) may be off
			ShowFriendButton();
			var message = PlayerOverlays.PillButton("Message", tooltip: $"Send {p.Username} a direct message");
			message.Pressed += () => { Close(); OnlineChat.Instance?.OpenDm(p.Username); };
			actions.AddChild(message);
			var report = PlayerOverlays.PillButton("Report", tooltip: "Report this player to the Qlute team");
			report.Pressed += () => PlayerOverlays.OpenReport(p.Username);
			actions.AddChild(report);
		}
		if (ModerationApi.IsStaff)
		{
			var moderate = PlayerOverlays.PillButton("Moderate", tooltip: "Open this player in the admin panel");
			moderate.Pressed += () => PlayerOverlays.OpenAdmin(p.Username);
			actions.AddChild(moderate);
		}
		if (self && SettingsOperator.SessionConfig.Loggedin)
		{
			var edit = PlayerOverlays.PillButton("Edit profile", tooltip: "Change your picture, banner and about me on the website");
			edit.Pressed += () => OS.ShellOpen(SettingsUrl());
			actions.AddChild(edit);
		}
		if (actions.GetChildCount() > 0) v.AddChild(actions);
	}

	// =====================================================================
	// Stat tiles + hit distribution
	// =====================================================================

	private void Stats(PlayerApi.Profile p)
	{
		var v = SectionBody(Section(_page));
		var tiles = AutoGrid(v, 150, fit: true);

		// level tile with its progress bar
		var level = PlayerOverlays.Panel(PlayerOverlays.Tile, 12, 16, 14);
		level.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		var lv = new VBoxContainer();
		lv.AddThemeConstantOverride("separation", 4);
		level.AddChild(lv);
		lv.AddChild(PlayerOverlays.Caption("Level"));
		lv.AddChild(PlayerOverlays.Text(p.Level.ToString("N0"), 21, bold: true));
		var pct = Mathf.Clamp(p.LevelPct, 0, 100);
		var track = PlayerOverlays.Track(6, new Color("#ffffff1f"), out var fillRow);
		track.TooltipText = $"{pct}% to level {p.Level + 1}";
		track.MouseFilter = MouseFilterEnum.Pass;
		if (pct > 0)
		{
			var fill = PlayerOverlays.GradientRect(new[] { PlayerOverlays.Perfect, PlayerOverlays.Accent }, new[] { 0f, 1f }, Vector2.Zero, new Vector2(1, 0));
			fill.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			fill.SizeFlagsStretchRatio = pct;
			fillRow.AddChild(fill);
		}
		if (pct < 100) fillRow.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsStretchRatio = 100 - pct, MouseFilter = MouseFilterEnum.Ignore });
		lv.AddChild(track);
		lv.AddChild(PlayerOverlays.Text($"{pct}% to next", 12, PlayerOverlays.Dim));
		tiles.AddChild(level);

		var list = new List<(string, string)>
		{
			("Accuracy", $"{p.Accuracy:0.##}%"),
			("Ranked score", p.Score.ToString("N0")),
			("Play time", string.IsNullOrEmpty(p.PlaytimeText) ? "0s" : p.PlaytimeText),
			("Max combo", $"{p.MaxCombo:N0}x"),
		};
		foreach (var (k, val) in list)
		{
			var tile = PlayerOverlays.StatTile(k, val, 21);
			tile.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			tiles.AddChild(tile);
		}

		// .pf-hits
		var hits = new (string label, long n, Color color)[]
		{
			("Perfect", p.JMax, PlayerOverlays.Perfect), ("Great", p.JGreat, PlayerOverlays.Great),
			("Meh", p.JMeh, PlayerOverlays.Meh), ("Miss", p.JBad, PlayerOverlays.Miss),
		};
		var total = hits.Sum(h => h.n);
		var hv = new VBoxContainer();
		hv.AddThemeConstantOverride("separation", 10);
		v.AddChild(hv);
		SectionHead(hv, "Hit distribution", $"{total:N0} notes hit", 14);
		var hitbar = PlayerOverlays.Track(12, new Color("#ffffff14"), out var hitRow, 2);
		hitbar.TooltipText = string.Join(", ", hits.Select(h => $"{h.label} {h.n:N0}"));
		hitbar.MouseFilter = MouseFilterEnum.Pass;
		foreach (var (label, n, color) in hits)
		{
			if (n <= 0) continue;
			hitRow.AddChild(new ColorRect
			{
				Color = color, SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsStretchRatio = n,
				CustomMinimumSize = new Vector2(3, 0), MouseFilter = MouseFilterEnum.Ignore,
			});
		}
		hv.AddChild(hitbar);
		var legend = new HFlowContainer();
		legend.AddThemeConstantOverride("h_separation", 18);
		legend.AddThemeConstantOverride("v_separation", 6);
		foreach (var (label, n, color) in hits)
		{
			var item = new HBoxContainer();
			item.AddThemeConstantOverride("separation", 6);
			var swatch = PlayerOverlays.Panel(color, 3, 0, 0);
			swatch.CustomMinimumSize = new Vector2(10, 10);
			swatch.SizeFlagsVertical = SizeFlags.ShrinkCenter;
			item.AddChild(swatch);
			item.AddChild(PlayerOverlays.Text(label, 13, PlayerOverlays.Dim));
			item.AddChild(PlayerOverlays.Text(n.ToString("N0"), 13, bold: true));
			item.AddChild(PlayerOverlays.Text(total > 0 ? $"{n * 100.0 / total:0.0}%" : "0%", 11, PlayerOverlays.Dim));
			legend.AddChild(item);
		}
		hv.AddChild(legend);
	}

	// =====================================================================
	// About me
	// =====================================================================

	private void Bio(PlayerApi.Profile p)
	{
		var self = p.Username == ApiOperator.Username && SettingsOperator.SessionConfig.Loggedin;
		if (string.IsNullOrWhiteSpace(p.Bio) && !self) return;
		var v = SectionBody(Section(_page), 8);
		var head = new Control { CustomMinimumSize = new Vector2(0, 34) };
		var title = SectionTitle("About me");
		title.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		title.HorizontalAlignment = HorizontalAlignment.Center;
		title.VerticalAlignment = VerticalAlignment.Center;
		head.AddChild(title);
		if (self)
		{
			var edit = PlayerOverlays.FlatButton("Edit", "Edit your about me on the website");
			edit.SetAnchorsAndOffsetsPreset(LayoutPreset.CenterRight);
			edit.GrowHorizontal = GrowDirection.Begin;
			edit.Pressed += () => OS.ShellOpen(SettingsUrl());
			head.AddChild(edit);
		}
		v.AddChild(head);
		if (string.IsNullOrWhiteSpace(p.Bio))
		{
			var empty = PlayerOverlays.Text("You haven't written anything about yourself yet.", 14, new Color("#ffffff80"));
			empty.HorizontalAlignment = HorizontalAlignment.Center;
			v.AddChild(empty);
			return;
		}
		// osu!-style BBCode (bold, colours, links...), centred like the website.
		var text = new RichTextLabel
		{
			BbcodeEnabled = true,
			FitContent = true,
			ScrollActive = false,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			SelectionEnabled = true,
			MouseFilter = MouseFilterEnum.Pass,
			Text = "[center]" + BioBBCode.ToGodot(p.Bio.Trim(), 14) + "[/center]",
		};
		text.AddThemeFontSizeOverride("normal_font_size", 14);
		text.AddThemeFontSizeOverride("bold_font_size", 14);
		text.AddThemeFontOverride("bold_font", PlayerOverlays.Bold);
		text.AddThemeColorOverride("default_color", new Color("#ffffffdd"));
		text.AddThemeConstantOverride("line_separation", 4);
		text.MetaClicked += meta =>
		{
			var url = meta.AsString();
			if (url.StartsWith("https://") || url.StartsWith("http://")) OS.ShellOpen(url);
		};
		v.AddChild(text);
	}

	// =====================================================================
	// Medals (.pf-medal): earned first, lavender tiles; locked ones dimmed. Tooltip = how to earn it.
	// =====================================================================

	private void Medals(List<PlayerApi.Medal> medals)
	{
		if (medals.Count == 0) return;
		var v = SectionBody(Section(_page));
		SectionHead(v, "Medals", $"{medals.Count(m => m.Achieved)} / {medals.Count} earned");
		var grid = AutoGrid(v, 210);
		foreach (var m in medals.OrderBy(m => !m.Achieved)) grid.AddChild(MedalTile(m));
	}

	private static Control MedalTile(PlayerApi.Medal m)
	{
		StyleBoxFlat Look(bool hover) => m.Achieved
			? PlayerOverlays.Style(new Color("#f3baff1c"), 12, 14, 12, new Color(hover ? "#f3baff99" : "#f3baff40"))
			: PlayerOverlays.Style(PlayerOverlays.Tile, 12, 14, 12, new Color(hover ? "#f3baff99" : "#00000000"));
		var tile = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Pass, MouseDefaultCursorShape = CursorShape.Help };
		tile.AddThemeStyleboxOverride("panel", Look(false));
		var how = string.IsNullOrEmpty(m.Hint) ? m.Desc : m.Hint;
		tile.TooltipText = (m.Achieved ? "Got it! " : "") + how;

		var h = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
		h.AddThemeConstantOverride("separation", 12);
		tile.AddChild(h);
		var icon = new PanelContainer { CustomMinimumSize = new Vector2(36, 36), SizeFlagsVertical = SizeFlags.ShrinkCenter, MouseFilter = MouseFilterEnum.Ignore };
		icon.AddThemeStyleboxOverride("panel", PlayerOverlays.Style(m.Achieved ? PlayerOverlays.Accent : new Color("#ffffff20"), 18, 0, 0));
		var glyph = PlayerOverlays.Text(m.Achieved ? "★" : "?", 18, m.Achieved ? PlayerOverlays.Ink : new Color("#ffffffaa"));
		glyph.HorizontalAlignment = HorizontalAlignment.Center;
		glyph.VerticalAlignment = VerticalAlignment.Center;
		icon.AddChild(glyph);
		h.AddChild(icon);

		var text = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore };
		text.AddThemeConstantOverride("separation", 2);
		text.AddChild(PlayerOverlays.Text(m.Title, 14, bold: true, trim: true));
		var desc = PlayerOverlays.Text(m.Desc, 12, PlayerOverlays.Dim, trim: true);
		text.AddChild(desc);
		h.AddChild(text);

		float rest = m.Achieved ? 1f : 0.45f;
		icon.Modulate = text.Modulate = new Color(1, 1, 1, rest);
		tile.MouseEntered += () => { tile.AddThemeStyleboxOverride("panel", Look(true)); if (!m.Achieved) icon.Modulate = text.Modulate = new Color(1, 1, 1, 0.75f); };
		tile.MouseExited += () => { tile.AddThemeStyleboxOverride("panel", Look(false)); icon.Modulate = text.Modulate = new Color(1, 1, 1, rest); };
		return tile;
	}

	// =====================================================================
	// Best scores | Last played (.pf-score-cols)
	// =====================================================================

	private void ScoreColumns(PlayerApi.Profile p)
	{
		// Full width, so the two columns line up with the sections above and below.
		_scoreCols = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
		_scoreCols.AddThemeConstantOverride("h_separation", Gap);
		_scoreCols.AddThemeConstantOverride("v_separation", Gap);
		_page.AddChild(_scoreCols);
		Scores(_scoreCols, "Best scores", p.Best, weighted: true, empty: "No Ranked Plays -n-");
		Scores(_scoreCols, "Last played", p.Recent, weighted: false, empty: "No Recent Plays -n-");
	}

	private static void Scores(Control parent, string title, List<PlayerApi.Play> plays, bool weighted, string empty)
	{
		var box = Section(parent);
		box.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		box.SizeFlagsVertical = SizeFlags.Fill;
		var v = SectionBody(box, 6);
		v.AddChild(SectionTitle(title));
		if (plays.Count == 0)
		{
			var l = PlayerOverlays.Text(empty, 14, PlayerOverlays.Dim);
			l.HorizontalAlignment = HorizontalAlignment.Center;
			v.AddChild(l);
			return;
		}
		double share = 1;
		foreach (var s in plays)
		{
			v.AddChild(ScoreCard(s, weighted ? share : -1));
			share *= 0.95; // same weighting as the server (PP_WEIGHT)
		}
	}

	/// <summary>".pf-scores .score-card": grade, "Title by Artist" over "Difficulty · when", mods, pp in lavender.</summary>
	private static Control ScoreCard(PlayerApi.Play s, double share)
	{
		var card = new PanelContainer { MouseFilter = MouseFilterEnum.Pass };
		var normal = PlayerOverlays.Style(PlayerOverlays.Tile, 10, 12, 10);
		var hover = PlayerOverlays.Style(PlayerOverlays.Tile2, 10, 12, 10);
		card.AddThemeStyleboxOverride("panel", normal);
		card.MouseEntered += () => card.AddThemeStyleboxOverride("panel", hover);
		card.MouseExited += () => card.AddThemeStyleboxOverride("panel", normal);
		var h = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
		h.AddThemeConstantOverride("separation", 10);
		card.AddChild(h);

		var grade = PlayerApi.Grade(s);
		var badge = PlayerOverlays.PillLabel(grade, PlayerOverlays.GradeColor(grade), 14, 44);
		badge.AddThemeStyleboxOverride("panel", PlayerOverlays.Style(PlayerOverlays.GradeColor(grade), 8, 10, 4));
		badge.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		h.AddChild(badge);

		var info = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = MouseFilterEnum.Ignore };
		info.AddThemeConstantOverride("separation", 2);
		var line1 = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
		line1.AddThemeConstantOverride("separation", 0);
		line1.AddChild(PlayerOverlays.Text(s.Beatmap, 14, bold: true));
		var by = PlayerOverlays.Text(string.IsNullOrEmpty(s.Artist) ? "" : $" by {s.Artist}", 14, trim: true);
		by.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		line1.AddChild(by);
		info.AddChild(line1);
		var line2 = $"{s.Difficulty} - {PlayerApi.Ago(s.Created)}";
		if (System.Math.Abs(s.Speed - 1) > 0.001) line2 += $" ({s.Speed:0.##}x)";
		if (share >= 0) line2 += $" (weighted {(int)System.Math.Round(share * 100)}%)";
		info.AddChild(PlayerOverlays.Text(line2, 12, PlayerOverlays.Dim, trim: true));
		h.AddChild(info);

		var right = new HBoxContainer { SizeFlagsVertical = SizeFlags.ShrinkCenter, MouseFilter = MouseFilterEnum.Ignore };
		right.AddThemeConstantOverride("separation", 4);
		foreach (var mod in Mods(s.Mods))
		{
			var m = PlayerOverlays.PillLabel(mod, PlayerOverlays.ModPill, 12);
			m.AddThemeStyleboxOverride("panel", PlayerOverlays.Style(PlayerOverlays.ModPill, 5, 7, 3));
			right.AddChild(m);
		}
		if (share >= 0) right.AddChild(Pp($"{(int)(s.Points * share)}pp*", "Weighted"));
		right.AddChild(Pp($"{(int)s.Points}pp", null));
		h.AddChild(right);
		return card;
	}

	private static Label Pp(string text, string tooltip)
	{
		var l = PlayerOverlays.Text(text, 13, PlayerOverlays.Accent, bold: true);
		l.CustomMinimumSize = new Vector2(46, 0);
		l.HorizontalAlignment = HorizontalAlignment.Right;
		if (tooltip != null) { l.TooltipText = tooltip; l.MouseFilter = MouseFilterEnum.Pass; }
		return l;
	}

	private static readonly string[] ModAliases = { "AT", "DT", "HT", "SL", "BT", "RND", "NF" }; // views.py modsaliasab
	private static IEnumerable<string> Mods(string mods) => ModAliases.Where(a => !string.IsNullOrEmpty(mods) && mods.Contains(a));

	// =====================================================================
	// Beatmaps they uploaded or claimed: Ranked / Special / Unranked tabs (apiv2/search?uploader=)
	// =====================================================================

	private const int CardMin = 340, CardGap = 10, PerPage = 50;

	private async void Beatmaps(PlayerApi.Profile p)
	{
		var tabs = new (string status, string label)[] { ("1", "Ranked"), ("4", "Special"), ("0", "Unranked") };
		// Keep the section's place in the page while counts load (Favourites is added after it).
		var box = Section(_page);
		box.Visible = false;
		var cached = new Dictionary<string, List<BrowseCatalogLegend>>();
		if (!PlayerApi.HasCounts(p))
		{
			// Older server: count from the first page of each tab (50 per page, so "50" can mean 50+).
			foreach (var (tab, _) in tabs)
			{
				var (sets, _) = await BeatmapSocialApi.Uploads(p.Username, tab);
				if (!IsInstanceValid(this)) return;
				cached[tab] = sets ?? new();
				p.BeatmapCounts[tab] = cached[tab].Count;
			}
		}
		long Count(string s) => p.BeatmapCounts.TryGetValue(s, out var n) ? n : 0;
		if (tabs.All(t => Count(t.status) == 0)) { box.QueueFree(); return; }
		box.Visible = true;

		var v = SectionBody(box, 10);
		v.AddChild(SectionTitle("Beatmaps"));
		v.AddChild(PlayerOverlays.SegmentBar(out var row));
		var grid = AutoGrid(v, CardMin, CardGap);
		var hint = PlayerOverlays.Text("", 14, new Color("#ffffffaa"));
		hint.HorizontalAlignment = HorizontalAlignment.Center;
		hint.Visible = false;
		v.AddChild(hint);
		var more = PlayerOverlays.PillButton("Show more");
		more.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
		more.Visible = false;
		v.AddChild(more);

		var buttons = new List<Button>();
		string status = null;
		int page = 0, request = 0;

		async void LoadPage()
		{
			int mine = request;
			more.Visible = false;
			var (sets, error) = page == 0 && cached.Remove(status, out var first) ? (first, null) : await BeatmapSocialApi.Uploads(p.Username, status, page);
			if (!IsInstanceValid(this) || mine != request) return;
			if (sets == null) { hint.Visible = true; hint.Text = error ?? "Couldn't load the beatmaps right now."; return; }
			AddCards(grid, sets, null);
			page++;
			hint.Visible = grid.GetChildCount() == 0;
			hint.Text = "No beatmaps here yet -n-";
			more.Visible = sets.Count >= PerPage;
		}

		void Show(int i)
		{
			request++;
			PreviewPlayer.Stop();
			for (int k = 0; k < buttons.Count; k++) PlayerOverlays.PaintSegment(buttons[k], k == i);
			status = tabs[i].status;
			page = 0;
			foreach (var c in grid.GetChildren()) c.QueueFree();
			LoadPage();
		}

		for (int i = 0; i < tabs.Length; i++)
		{
			var n = Count(tabs[i].status);
			var b = PlayerOverlays.SegmentButton($"{tabs[i].label}  {n:N0}");
			b.Disabled = n == 0;
			int index = i;
			b.Pressed += () => Show(index);
			row.AddChild(b);
			buttons.Add(b);
		}
		more.Pressed += LoadPage;
		Show(System.Array.FindIndex(tabs, t => Count(t.status) > 0));
	}

	/// <summary>The same cards as the Browse screen, stretched to the grid's columns.</summary>
	private static void AddCards(GridContainer grid, IEnumerable<BrowseCatalogLegend> sets, int? status)
	{
		var scene = GD.Load<PackedScene>("res://Panels/BrowseElements/Card.tscn");
		foreach (var set in sets.Where(s => s.beatmaps is { Count: > 0 }))
		{
			set.beatmaps = set.beatmaps.OrderBy(d => d.level).ToList();
			var card = scene.Instantiate<CardFunctions>();
			card.Modulate = new Color(1, 1, 1, 0);
			card.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			card.CustomMinimumSize = new Vector2(0, card.CustomMinimumSize.Y);
			grid.AddChild(card);
			card.Setup(set, -1, status ?? set.beatmaps[0].ranked);
		}
	}

	// =====================================================================
	// Favourite beatmaps (apiv2/favorites)
	// =====================================================================

	private void Favorites(string username)
	{
		var v = SectionBody(Section(_page), 10);
		var head = new HBoxContainer();
		head.AddThemeConstantOverride("separation", 6);
		head.AddChild(SectionTitle("Favourite Beatmaps"));
		var count = PlayerOverlays.Text("", 15, new Color("#ffffff80"));
		count.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		head.AddChild(count);
		v.AddChild(head);

		var grid = AutoGrid(v, CardMin, CardGap);
		var hint = PlayerOverlays.Text("Loading favourites...", 14, new Color("#ffffffaa"), wrap: true);
		hint.HorizontalAlignment = HorizontalAlignment.Center;
		v.AddChild(hint);
		var more = PlayerOverlays.PillButton("Show more");
		more.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
		more.Visible = false;
		v.AddChild(more);

		int page = 0, shown = 0;
		async void LoadPage()
		{
			more.Visible = false;
			var (total, sets, error) = await BeatmapSocialApi.Favorites(username, page);
			if (!IsInstanceValid(this)) return;
			if (sets == null) { hint.Text = error ?? "Couldn't load the favourites right now."; return; }
			AddCards(grid, sets, null);
			shown += sets.Count;
			page++;
			count.Text = total > 0 ? $"· {total:N0}" : "";
			hint.Visible = shown == 0;
			if (shown == 0)
				hint.Text = username == ApiOperator.Username
					? "No favourites yet. Press Favourite on a beatmap's info page to keep it here."
					: "No favourite beatmaps yet -n-";
			more.Visible = shown < total && sets.Count > 0;
		}
		more.Pressed += LoadPage;
		LoadPage();
	}
}
