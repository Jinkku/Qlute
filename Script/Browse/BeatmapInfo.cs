using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// A beatmap set's page in the game, laid out like the website's /beatmapset/SET/DIFF (beatmap.html, beatmap.css
/// "2026-11-13 refresh"): cover hero with chips and pill buttons, difficulty tabs + stat tiles, details + tags,
/// leaderboard and comments. Opened from the beatmap cards (PlayerOverlays.OpenBeatmap).
/// </summary>
public partial class BeatmapInfo : Control
{
	private readonly BrowseCatalogLegend _set;
	private Texture2D _cover;
	private List<CatalogBeatmapInfoLegend> _diffs = new();
	private CatalogBeatmapInfoLegend _current;
	private Dictionary<int, BeatmapSocialApi.DiffDetails> _details = new();
	private Node _scene;

	private PanelContainer _panel;
	private VBoxContainer _page;
	private HFlowContainer _tabs;
	private GridContainer _stats, _meta;
	private Label _diffName, _boardTitle, _commentCount, _commentLeft, _commentStatus;
	private VBoxContainer _board, _comments;
	private Button _download, _play, _preview, _favorite, _copy, _more, _send;
	private TextEdit _commentText;
	private PanelContainer _previewBar;
	private Control _previewFill, _previewRest;
	private string _previewGlyph;
	private int _favCount;
	private bool _favorited;
	private int _boardRequest, _commentPage, _commentTotal, _commentShown;

	private static Color Accent => PlayerOverlays.Accent;
	private static Color Tile => PlayerOverlays.Tile;
	private static readonly Color HeroButton = new("#00000066");
	private static readonly Color HeroButtonHover = new("#ffffff33");
	private const int MaxComment = 1000, SidePad = 15;

	public BeatmapInfo() : this(null, null) { }
	public BeatmapInfo(BrowseCatalogLegend set, Texture2D cover)
	{
		_set = set;
		_cover = cover;
	}

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

		var bar = PlayerOverlays.TitleBar(outer, "beatmap info");
		if (_set != null)
		{
			var web = PlayerOverlays.FlatButton("view on website");
			web.Pressed += () => OS.ShellOpen(WebUrl());
			bar.AddChild(web);
		}
		var close = PlayerOverlays.FlatButton("✕", "Close (Esc)");
		close.Pressed += Close;
		bar.AddChild(close);

		var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
		outer.AddChild(scroll);
		_page = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		_page.AddThemeConstantOverride("separation", 12); // .bm-section margin-top
		scroll.AddChild(_page);

		_scene = GetTree().CurrentScene;
		_scene?.SetProcessInput(false);

		Modulate = new Color(1, 1, 1, 0);
		CreateTween().SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out).TweenProperty(this, "modulate:a", 1f, 0.2f);

		if (_set?.beatmaps is not { Count: > 0 })
		{
			var box = Section(40);
			var v = new VBoxContainer();
			box.AddChild(v);
			v.AddChild(Centered(PlayerOverlays.Text("o-o", 32, bold: true)));
			v.AddChild(Centered(PlayerOverlays.Text("This beatmap doesn't exist (or isn't on Qlute yet).", 16, bold: true)));
			return;
		}
		_diffs = _set.beatmaps.OrderBy(d => d.level).ThenBy(d => d.count_circles).ToList();
		_favorited = _set.favorited;
		_favCount = _set.favourite_count;

		Hero();
		DiffSection();
		MetaSection();
		BoardSection();
		CommentSection();
		Select(_diffs[0]);
		LoadExtras();
	}

	public override void _ExitTree()
	{
		if (IsInstanceValid(_scene)) _scene.SetProcessInput(true);
		if (_set != null && PreviewPlayer.IsPlaying(_set.id)) PreviewPlayer.Stop();
	}

	public override void _Input(InputEvent e)
	{
		if (e.IsActionPressed("ui_cancel") && !(_commentText?.HasFocus() ?? false))
		{
			Close();
			GetViewport().SetInputAsHandled();
		}
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

	private double _tick;
	public override void _Process(double delta)
	{
		if (_set == null || _preview == null) return;
		bool playing = PreviewPlayer.IsPlaying(_set.id);
		_preview.Text = PreviewPlayer.IsLoading(_set.id) ? "Loading..." : playing ? "Stop" : "Preview";
		var glyph = playing ? PlayerOverlays.PauseGlyph : PlayerOverlays.PlayGlyph;
		if (_previewGlyph != glyph) { _previewGlyph = glyph; _preview.Icon = PlayerOverlays.SvgIcon(glyph, "#f3baff"); }
		_previewBar.Modulate = new Color(1, 1, 1, playing ? 1 : 0); // visibility: hidden, so the hero doesn't jump
		var progress = PreviewPlayer.Progress(_set.id);
		_previewFill.SizeFlagsStretchRatio = Mathf.Max(0.0001f, progress);
		_previewRest.SizeFlagsStretchRatio = Mathf.Max(0.0001f, 1 - progress);

		_tick += delta;
		if (_tick < 0.5) return;
		_tick = 0;
		var local = SettingsOperator.Beatmaps.FirstOrDefault(b => b.BeatmapSetID == _set.id);
		_download.Visible = local == null;
		_play.Visible = local != null;
		_play.SetMeta("song", local?.ID ?? -1);
		_send.Disabled = !BeatmapSocialApi.LoggedIn || string.IsNullOrWhiteSpace(_commentText.Text);
	}

	public override void _PhysicsProcess(double delta)
	{
		// CSS auto-fill grids, measured from the panel (a grid's own width follows its columns).
		var width = _panel.Size.X - SidePad * 2 - 12;
		if (IsInstanceValid(_stats)) _stats.Columns = PlayerOverlays.AutoColumns(width, 140, 10);
		if (IsInstanceValid(_meta)) _meta.Columns = PlayerOverlays.AutoColumns(width, 220, 10);
	}

	private string WebUrl() => $"{PlayerApi.Base.TrimEnd('/')}/beatmapset/{_set.id}" + (_current != null ? $"/{_current.id}" : "");

	// =====================================================================
	// Building blocks
	// =====================================================================

	/// <summary>A website ".infobox .bm-section": 15px sides, 20px top / bottom.</summary>
	private PanelContainer Section(int marginY = 20)
	{
		var p = PlayerOverlays.Panel(PlayerOverlays.Box, 0, SidePad, marginY);
		_page.AddChild(p);
		return p;
	}

	private static VBoxContainer Body(PanelContainer box, int separation = 8)
	{
		var v = new VBoxContainer();
		v.AddThemeConstantOverride("separation", separation);
		box.AddChild(v);
		return v;
	}

	private static Label Centered(Label l)
	{
		l.HorizontalAlignment = HorizontalAlignment.Center;
		return l;
	}

	private static Label Heading(string text) => PlayerOverlays.Text(text, 17, bold: true);

	private static Label Empty(string text) => Centered(PlayerOverlays.Text(text, 14, new Color("#ffffffaa")));

	/// <summary>.bm-button: rounded pill. In the hero the quiet ones sit on a dark glass.</summary>
	private static Button WebButton(string text, bool primary = false, string tooltip = "", bool hero = false)
	{
		var b = PlayerOverlays.PillButton(text, primary, tooltip, 15,
			primary ? null : hero ? HeroButton : new Color("#ffffff28"),
			primary ? null : hero ? HeroButtonHover : new Color("#ffffff40"));
		return b;
	}

	private static GridContainer AutoGrid(Control parent)
	{
		var grid = new GridContainer { Columns = 3, SizeFlagsHorizontal = SizeFlags.ExpandFill };
		grid.AddThemeConstantOverride("h_separation", 10);
		grid.AddThemeConstantOverride("v_separation", 10);
		parent.AddChild(grid);
		return grid;
	}

	private static string Length(int sec) => $"{sec / 60}:{sec % 60:00}";
	private static string Num(long n) => n.ToString("N0");

	// =====================================================================
	// Hero: cover, status, title, artist, mapper, chips, buttons
	// =====================================================================

	private void Hero()
	{
		var hero = new PanelContainer { CustomMinimumSize = new Vector2(0, 300), ClipContents = true };
		hero.AddThemeStyleboxOverride("panel", PlayerOverlays.Style(PlayerOverlays.Page, 0, 0, 0));
		_page.AddChild(hero);

		var cover = new TextureRect
		{
			Texture = _cover,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
			MouseFilter = MouseFilterEnum.Ignore,
			Modulate = new Color(0.62f, 0.6f, 0.62f), // filter: brightness(.6) saturate(1.1)
		};
		hero.AddChild(cover);
		if (!string.IsNullOrEmpty(_set.covers?.cover))
			_ = ApiOperator.DownloadImage(_set.covers.cover, tex =>
			{
				if (IsInstanceValid(cover)) { cover.Texture = tex; _cover = tex; }
			});

		hero.AddChild(PlayerOverlays.GradientRect(
			new[] { new Color("#00000000"), new Color("#221E2799"), new Color("#221E27f5") },
			new[] { 0f, 0.55f, 1f }, Vector2.Zero, new Vector2(0, 1)));

		var margin = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore };
		margin.AddThemeConstantOverride("margin_left", SidePad + 8);
		margin.AddThemeConstantOverride("margin_right", SidePad + 8);
		margin.AddThemeConstantOverride("margin_top", 24);
		margin.AddThemeConstantOverride("margin_bottom", 20);
		hero.AddChild(margin);

		var v = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
		v.AddThemeConstantOverride("separation", 4);
		margin.AddChild(v);

		var status = _diffs[0].ranked;
		var statusPill = PlayerOverlays.Panel(new Color("#000000b0"), 99, 11, 4);
		statusPill.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
		statusPill.AddChild(PlayerOverlays.Text(CardFunctions.StatusName(status), 13, PlayerOverlays.StatusAccent(status), bold: true));
		v.AddChild(statusPill);

		Label Shadowed(Label l)
		{
			l.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.8f));
			l.AddThemeConstantOverride("shadow_offset_y", 2);
			l.AddThemeConstantOverride("shadow_outline_size", 6);
			return l;
		}
		v.AddChild(Shadowed(PlayerOverlays.Text(_set.title ?? "", 36, wrap: true, bold: true)));
		var sub = new Color("#ffffffb0");
		if (!string.IsNullOrEmpty(_set.title_unicode) && _set.title_unicode != _set.title)
			v.AddChild(Shadowed(PlayerOverlays.Text(_set.title_unicode, 14, sub)));
		v.AddChild(Shadowed(PlayerOverlays.Text(_set.artist ?? "", 20, new Color("#ffffffe0"))));

		// "mapped by <b>NAME</b> · uploaded DATE" (the name opens the mapper's profile when the set is linked)
		var mapped = new HBoxContainer();
		mapped.AddThemeConstantOverride("separation", 0);
		mapped.AddChild(Shadowed(PlayerOverlays.Text("mapped by ", 14, sub)));
		if (!string.IsNullOrEmpty(_set.mapper_user))
		{
			var link = new Button { Text = _set.creator ?? "", Flat = true, FocusMode = FocusModeEnum.None, MouseDefaultCursorShape = CursorShape.PointingHand, TooltipText = $"{_set.mapper_user}'s profile" };
			link.AddThemeFontOverride("font", PlayerOverlays.Bold);
			link.AddThemeFontSizeOverride("font_size", 14);
			foreach (var key in new[] { "normal", "hover", "pressed", "hover_pressed", "focus" }) link.AddThemeStyleboxOverride(key, new StyleBoxEmpty());
			link.AddThemeColorOverride("font_color", Accent);
			link.AddThemeColorOverride("font_hover_color", Colors.White);
			var who = _set.mapper_user;
			link.Pressed += () => PlayerOverlays.OpenProfile(who);
			mapped.AddChild(link);
		}
		else mapped.AddChild(Shadowed(PlayerOverlays.Text(_set.creator ?? "", 14, bold: true)));
		if (_set.last_updated > 0)
			mapped.AddChild(Shadowed(PlayerOverlays.Text($" · uploaded {DateTimeOffset.FromUnixTimeSeconds((long)_set.last_updated).LocalDateTime:MMMM d, yyyy}", 14, sub)));
		v.AddChild(mapped);

		// .bm-hero-stats chips
		var chips = new HFlowContainer();
		chips.AddThemeConstantOverride("h_separation", 6);
		chips.AddThemeConstantOverride("v_separation", 6);
		var longest = _diffs.Max(d => d.total_length);
		var lvs = _diffs.Select(d => (int)Math.Round(d.level)).ToList();
		var plays = _set.play_count > 0 ? _set.play_count : _diffs.Sum(d => d.playcount);
		foreach (var (icon, text) in new[]
		{
			(PlayerOverlays.ClockGlyph, Length(longest)), ("♫", $"{_set.bpm:0.##} BPM"),
			("☰", $"{_diffs.Count} difficult{(_diffs.Count == 1 ? "y" : "ies")}"),
			("★", lvs.Min() == lvs.Max() ? $"Lv {lvs[0]}" : $"Lv {lvs.Min()}-{lvs.Max()}"),
			(PlayerOverlays.PlayGlyph, $"{Num(plays)} plays"),
		})
		{
			var chip = PlayerOverlays.Panel(new Color("#00000070"), 99, 11, 5);
			var h = new HBoxContainer();
			h.AddThemeConstantOverride("separation", 6);
			h.AddChild(icon.StartsWith("<") ? PlayerOverlays.IconRect(icon, Accent) : PlayerOverlays.Text(icon, 13, Accent));
			h.AddChild(PlayerOverlays.Text(text, 13, new Color("#ffffffe0")));
			chip.AddChild(h);
			chips.AddChild(chip);
		}
		v.AddChild(new Control { CustomMinimumSize = new Vector2(0, 8) });
		v.AddChild(chips);

		var buttons = new HFlowContainer();
		buttons.AddThemeConstantOverride("h_separation", 8);
		buttons.AddThemeConstantOverride("v_separation", 8);
		v.AddChild(new Control { CustomMinimumSize = new Vector2(0, 10) });
		v.AddChild(buttons);

		_download = WebButton("↓  Download", primary: true, "Download this set");
		_download.Pressed += () => ApiOperator.DownloadBeatmap(_set);
		buttons.AddChild(_download);

		_play = WebButton("Play", primary: true, "Already downloaded: open it in song select");
		_play.Icon = PlayerOverlays.SvgIcon(PlayerOverlays.PlayGlyph, "#221E27");
		_play.Visible = false;
		_play.Pressed += () =>
		{
			var id = (int)_play.GetMeta("song", -1);
			if (id < 0) return;
			GetNode<SettingsOperator>("/root/SettingsOperator").SelectSongID(id);
			Close();
			GetNode<SceneTransition>("/root/Transition").Switch("res://Panels/Screens/song_select.tscn");
		};
		buttons.AddChild(_play);

		_preview = WebButton("Preview", hero: true);
		_preview.Visible = !string.IsNullOrEmpty(_set.preview_url);
		_preview.Pressed += () => PreviewPlayer.Toggle(_set.preview_url, _set.id);
		buttons.AddChild(_preview);

		_favorite = WebButton("Favourite", hero: true);
		_favorite.Icon = GD.Load<Texture2D>("res://Resources/System/Heart.svg");
		_favorite.Pressed += ToggleFavorite;
		buttons.AddChild(_favorite);
		PaintFavorite();

		_copy = WebButton("🔗  Copy link", tooltip: "Copy a link to this difficulty", hero: true);
		_copy.Pressed += () =>
		{
			DisplayServer.ClipboardSet(WebUrl());
			_copy.Text = "Copied!";
			GetTree().CreateTimer(1.5).Timeout += () => { if (IsInstanceValid(_copy)) _copy.Text = "🔗  Copy link"; };
		};
		buttons.AddChild(_copy);

		// .bm-preview-bar
		_previewBar = PlayerOverlays.Track(4, new Color("#ffffff18"), out var fillRow);
		_previewBar.Modulate = new Color(1, 1, 1, 0);
		_previewFill = new ColorRect { Color = Accent, SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsStretchRatio = 0.0001f, MouseFilter = MouseFilterEnum.Ignore };
		fillRow.AddChild(_previewFill);
		_previewRest = new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore };
		fillRow.AddChild(_previewRest);
		v.AddChild(new Control { CustomMinimumSize = new Vector2(0, 6) });
		v.AddChild(_previewBar);
	}

	// =====================================================================
	// Favourite
	// =====================================================================

	private void PaintFavorite()
	{
		_favorite.Text = (_favorited ? "Favourited" : "Favourite") + (_favCount > 0 ? $"  {Num(_favCount)}" : "");
		_favorite.TooltipText = !BeatmapSocialApi.LoggedIn ? "Log in to favourite this map"
			: _favorited ? "Remove from your favourites" : "Add to your favourites (shown on your profile)";
		foreach (var key in new[] { "icon_normal_color", "icon_hover_color", "icon_pressed_color", "icon_hover_pressed_color", "icon_disabled_color" })
			_favorite.AddThemeColorOverride(key, Accent);
		// .bm-fav.on: lavender glass with an inset line
		if (_favorited) PlayerOverlays.PaintPill(_favorite, false, new Color("#f3baff40"), new Color("#f3baff4d"), new Color("#f3baff66"));
		else PlayerOverlays.PaintPill(_favorite, false, HeroButton, HeroButtonHover);
	}

	private async void ToggleFavorite()
	{
		if (!BeatmapSocialApi.LoggedIn) { Notify.Post("Log in to favourite beatmaps."); return; }
		_favorite.Disabled = true;
		var (on, count, error) = await BeatmapSocialApi.SetFavorite(_set.id, !_favorited);
		if (!IsInstanceValid(this)) return;
		_favorite.Disabled = false;
		if (error != null) { Notify.Post(error, Type: NotificationIcons.NotificationType.Warning); return; }
		_favorited = _set.favorited = on;
		_favCount = _set.favourite_count = count;
		PaintFavorite();
		PaintMeta();
		Notify.Post(on ? $"Added {_set.title} to your favourites." : $"Removed {_set.title} from your favourites.");
	}

	// =====================================================================
	// Difficulty tabs + stats
	// =====================================================================

	private void DiffSection()
	{
		var v = Body(Section());
		_tabs = new HFlowContainer();
		_tabs.AddThemeConstantOverride("h_separation", 8);
		_tabs.AddThemeConstantOverride("v_separation", 8);
		v.AddChild(_tabs);
		v.AddChild(new Control { CustomMinimumSize = new Vector2(0, 6) });
		_diffName = PlayerOverlays.Text("", 22, bold: true, wrap: true);
		v.AddChild(_diffName);
		_stats = AutoGrid(v);
	}

	/// <summary>.bm-diff: pill with a level-coloured "Lv N" badge and the name; the active one gets a ring in that colour.</summary>
	private void PaintTabs()
	{
		foreach (var c in _tabs.GetChildren()) c.QueueFree();
		foreach (var d in _diffs)
		{
			bool on = d == _current;
			bool black = PlayerOverlays.LevelIsBlack(d.level);
			var lv = black ? new Color("#ffffffcc") : PlayerOverlays.LevelColor(d.level, 0.70f);
			var b = new Button { TooltipText = d.version ?? "", FocusMode = FocusModeEnum.None, MouseDefaultCursorShape = CursorShape.PointingHand };
			StyleBoxFlat S(Color bg)
			{
				var s = PlayerOverlays.Style(bg, 99, 0, 0, on ? lv : Colors.Transparent, 2);
				s.ContentMarginLeft = 8; s.ContentMarginRight = 14; s.ContentMarginTop = s.ContentMarginBottom = 7;
				return s;
			}
			b.AddThemeStyleboxOverride("normal", S(on ? new Color("#ffffff14") : Tile));
			b.AddThemeStyleboxOverride("hover", S(new Color("#ffffff1c")));
			b.AddThemeStyleboxOverride("pressed", S(new Color("#ffffff1c")));
			b.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());

			// A Button doesn't lay out children: put the content in a box and size the button from it.
			var margin = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore };
			margin.AddThemeConstantOverride("margin_left", 8);
			margin.AddThemeConstantOverride("margin_right", 14);
			margin.AddThemeConstantOverride("margin_top", 7);
			margin.AddThemeConstantOverride("margin_bottom", 7);
			margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			var h = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
			h.AddThemeConstantOverride("separation", 8);
			margin.AddChild(h);
			var badge = PlayerOverlays.Panel(black ? Colors.Black : lv, 99, 8, 2);
			if (black) badge.AddThemeStyleboxOverride("panel", PlayerOverlays.Style(Colors.Black, 99, 8, 2, new Color("#ffffff40")));
			badge.MouseFilter = MouseFilterEnum.Ignore;
			badge.SizeFlagsVertical = SizeFlags.ShrinkCenter;
			badge.AddChild(PlayerOverlays.Text($"Lv {d.level:0}", 12, black ? Colors.White : PlayerOverlays.Ink, bold: true));
			h.AddChild(badge);
			var name = PlayerOverlays.Text(d.version ?? "", 14, on ? Colors.White : new Color("#ffffffcc"), bold: on);
			name.SizeFlagsVertical = SizeFlags.ShrinkCenter;
			name.ClipText = true;
			name.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
			var font = on ? PlayerOverlays.Bold : name.GetThemeFont("font");
			name.CustomMinimumSize = new Vector2(Mathf.Min(220, font.GetStringSize(name.Text, HorizontalAlignment.Left, -1, 14).X + 2), 0);
			h.AddChild(name);
			b.AddChild(margin);
			margin.MinimumSizeChanged += () => b.CustomMinimumSize = margin.GetCombinedMinimumSize();
			b.CustomMinimumSize = margin.GetCombinedMinimumSize();

			var diff = d;
			b.Pressed += () => Select(diff);
			_tabs.AddChild(b);
		}
	}

	private void Select(CatalogBeatmapInfoLegend d)
	{
		_current = d;
		PaintTabs();
		_diffName.Text = string.IsNullOrEmpty(d.version) ? "Difficulty" : d.version;
		PaintStats();
		PaintMeta();
		LoadBoard();
	}

	private void PaintStats()
	{
		foreach (var c in _stats.GetChildren()) c.QueueFree();
		var d = _current;
		_details.TryGetValue(d.id, out var extra);
		var tiles = new List<(string, string)>
		{
			("Level", $"{d.level:0}"),
			("Length", Length(d.total_length)),
			("BPM", $"{d.bpm:0.##}"),
			("Notes", Num(extra?.Notes > 0 ? extra.Notes : d.count_circles + d.count_sliders)),
			("Overall Difficulty", extra != null ? $"{extra.Od:0.#}" : "-"),
		};
		if (d.pp > 0) tiles.Add(("Max Performance", $"{Math.Round(d.pp):0}pp"));
		if (d.playcount > 0) tiles.Add(("Plays", Num(d.playcount)));
		foreach (var (k, val) in tiles)
		{
			var tile = PlayerOverlays.StatTile(k, val, 21);
			tile.AddThemeStyleboxOverride("panel", PlayerOverlays.Style(Tile, 12, 14, 12));
			tile.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			_stats.AddChild(tile);
		}
	}

	// =====================================================================
	// Details + tags
	// =====================================================================

	private HFlowContainer _tags;

	private void MetaSection()
	{
		var v = Body(Section());
		v.AddChild(Heading("Details"));
		_meta = AutoGrid(v);
		var tags = (_set.tags ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct().ToList();
		if (tags.Count == 0) return;
		v.AddChild(new Control { CustomMinimumSize = new Vector2(0, 4) });
		v.AddChild(PlayerOverlays.Caption("Tags"));
		_tags = new HFlowContainer();
		_tags.AddThemeConstantOverride("h_separation", 6);
		_tags.AddThemeConstantOverride("v_separation", 6);
		foreach (var tag in tags)
		{
			var chip = PlayerOverlays.Panel(new Color("#ffffff14"), 99, 11, 4);
			chip.AddChild(PlayerOverlays.Text(tag, 13, new Color("#ffffffd8")));
			_tags.AddChild(chip);
		}
		v.AddChild(_tags);
	}

	private void PaintMeta()
	{
		if (_meta == null) return;
		foreach (var c in _meta.GetChildren()) c.QueueFree();
		var rows = new List<(string key, string value, Color? colour)>();
		if (!string.IsNullOrEmpty(_set.source)) rows.Add(("Source", _set.source, Accent));
		rows.Add(("Mapper", _set.creator ?? "", Accent));
		rows.Add(("Status", CardFunctions.StatusName(_diffs[0].ranked), null));
		rows.Add(("Genre", string.IsNullOrEmpty(_set.genre_name) ? "Unspecified" : _set.genre_name, _set.genre > 1 ? Accent : null));
		rows.Add(("Language", string.IsNullOrEmpty(_set.language_name) ? "Unspecified" : _set.language_name, _set.language > 1 ? Accent : null));
		var extras = new List<string>();
		if (_set.nsfw) extras.Add("Explicit");
		if (Browse.ShowVideoStoryboard && _set.video) extras.Add("Video");
		if (Browse.ShowVideoStoryboard && _set.storyboard) extras.Add("Storyboard");
		rows.Add(("Content", extras.Count > 0 ? string.Join(", ", extras) : "No extras",
			extras.Count == 0 ? new Color("#ffffff80") : _set.nsfw ? new Color("#ff9a9a") : null));
		rows.Add(("Plays", Num(_set.play_count > 0 ? _set.play_count : _diffs.Sum(x => x.playcount)), null));
		rows.Add(("Favourites", Num(_favCount), null));
		if (!string.IsNullOrEmpty(_set.title_unicode) && _set.title_unicode != _set.title) rows.Add(("Title (original)", _set.title_unicode, null));
		if (!string.IsNullOrEmpty(_set.artist_unicode) && _set.artist_unicode != _set.artist) rows.Add(("Artist (original)", _set.artist_unicode, null));
		if (_set.last_updated > 0) rows.Add(("Added", DateTimeOffset.FromUnixTimeSeconds((long)_set.last_updated).LocalDateTime.ToString("MMMM d, yyyy"), null));
		rows.Add(("Beatmap set ID", _set.id.ToString(), null));
		rows.Add(("Beatmap ID", _current?.id.ToString() ?? "", null));
		foreach (var (k, val, colour) in rows)
		{
			var tile = PlayerOverlays.Panel(Tile, 12, 14, 11);
			tile.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			var t = new VBoxContainer();
			t.AddThemeConstantOverride("separation", 4);
			t.AddChild(PlayerOverlays.Caption(k));
			var value = PlayerOverlays.Text(val, 15, colour, bold: colour == Accent, trim: true);
			value.TooltipText = val;
			value.MouseFilter = MouseFilterEnum.Pass;
			t.AddChild(value);
			tile.AddChild(t);
			if (k == "Mapper" && !string.IsNullOrEmpty(_set.mapper_user))
			{
				tile.MouseDefaultCursorShape = CursorShape.PointingHand;
				tile.TooltipText = $"{_set.mapper_user}'s profile";
				var who = _set.mapper_user;
				tile.GuiInput += e => { if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true }) PlayerOverlays.OpenProfile(who); };
			}
			_meta.AddChild(tile);
		}
	}

	// =====================================================================
	// Leaderboard (.bm-table)
	// =====================================================================

	private void BoardSection()
	{
		var v = Body(Section(), 6);
		_boardTitle = Heading("Leaderboard");
		v.AddChild(_boardTitle);
		_board = new VBoxContainer();
		_board.AddThemeConstantOverride("separation", 5);
		v.AddChild(_board);
	}

	private static readonly (string head, int width, bool right)[] Columns =
	{
		("#", 40, false), ("", 40, false), ("Player", 0, false), ("Score", 96, true), ("Accuracy", 80, true),
		("Combo", 66, true), ("Perfect", 62, true), ("Great", 54, true), ("Meh", 46, true), ("Miss", 46, true),
		("pp", 58, true), ("Mods", 84, false), ("Set", 100, false),
	};

	private static HBoxContainer BoardRow(Control[] cells)
	{
		var h = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
		h.AddThemeConstantOverride("separation", 8);
		for (int i = 0; i < cells.Length; i++)
		{
			var (_, width, _) = Columns[i];
			cells[i].CustomMinimumSize = new Vector2(width, cells[i].CustomMinimumSize.Y);
			if (width == 0) cells[i].SizeFlagsHorizontal = SizeFlags.ExpandFill;
			cells[i].SizeFlagsVertical = SizeFlags.ShrinkCenter;
			h.AddChild(cells[i]);
		}
		return h;
	}

	private static Label Cell(string text, int i, Color? colour = null, bool bold = false, int size = 14)
	{
		var l = PlayerOverlays.Text(text, size, colour, bold: bold, trim: Columns[i].width == 0);
		if (Columns[i].right) l.HorizontalAlignment = HorizontalAlignment.Right;
		return l;
	}

	private static readonly Color[] Podium = { new("#ffd76a"), new("#d6dbe6"), new("#e3a679") };

	private async void LoadBoard()
	{
		var d = _current;
		int mine = ++_boardRequest;
		_boardTitle.Text = $"Leaderboard · {d.version}";
		foreach (var c in _board.GetChildren()) c.QueueFree();
		_board.AddChild(Empty("Loading scores..."));
		var (scores, error) = await BeatmapSocialApi.Leaderboard(d.id);
		if (!IsInstanceValid(this) || mine != _boardRequest) return;
		foreach (var c in _board.GetChildren()) c.QueueFree();
		if (scores == null) { _board.AddChild(Empty("Couldn't load the leaderboard. Try again in a bit.")); return; }
		if (scores.Count == 0)
		{
			_board.AddChild(Empty(d.ranked == 0 ? "Unranked maps don't have a leaderboard." : "No scores yet. Be the first to set one!"));
			return;
		}
		var header = Columns.Select((c, i) => (Control)Cell(c.head.ToUpperInvariant(), i, PlayerOverlays.Dim, size: 11)).ToArray();
		var headBox = PlayerOverlays.Panel(Colors.Transparent, 0, 10, 2);
		headBox.AddChild(BoardRow(header));
		_board.AddChild(headBox);

		int rank = 0;
		foreach (var s in scores.Take(50))
		{
			rank++;
			var (grade, acc) = BeatmapSocialApi.Grade(s);
			bool me = s.Username.Equals(ApiOperator.Username, StringComparison.OrdinalIgnoreCase);
			var bg = me ? new Color("#f3baff24") : Tile;
			StyleBoxFlat RowStyle(Color c)
			{
				var st = PlayerOverlays.Style(c, 8, 10, 7);
				if (rank <= 3) { st.BorderColor = Podium[rank - 1]; st.BorderWidthLeft = 4; st.ContentMarginLeft = 6; }
				return st;
			}
			var rowBox = new PanelContainer { MouseFilter = MouseFilterEnum.Pass };
			var normal = RowStyle(bg);
			var hover = RowStyle(me ? new Color("#f3baff33") : new Color("#ffffff1f"));
			rowBox.AddThemeStyleboxOverride("panel", normal);
			rowBox.MouseEntered += () => rowBox.AddThemeStyleboxOverride("panel", hover);
			rowBox.MouseExited += () => rowBox.AddThemeStyleboxOverride("panel", normal);

			var gradePill = PlayerOverlays.PillLabel(grade, PlayerOverlays.GradeColor(grade), 12);
			gradePill.AddThemeStyleboxOverride("panel", PlayerOverlays.Style(PlayerOverlays.GradeColor(grade), 7, 6, 2));
			gradePill.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;

			// .bm-player: round picture + name, opens their profile
			var player = new Button { FocusMode = FocusModeEnum.None, MouseDefaultCursorShape = CursorShape.PointingHand, TooltipText = $"Open {s.Username}'s profile" };
			foreach (var key in new[] { "normal", "hover", "pressed", "hover_pressed", "focus" }) player.AddThemeStyleboxOverride(key, new StyleBoxEmpty());
			var who = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
			who.AddThemeConstantOverride("separation", 9);
			who.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			if (!string.IsNullOrEmpty(s.Pfp)) who.AddChild(PlayerOverlays.Picture(s.Pfp, 28, 14));
			var nameLabel = PlayerOverlays.Text(s.Username, 14, bold: true, trim: true);
			nameLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			nameLabel.VerticalAlignment = VerticalAlignment.Center;
			who.AddChild(nameLabel);
			player.AddChild(who);
			player.CustomMinimumSize = new Vector2(0, 28);
			player.MouseEntered += () => nameLabel.AddThemeColorOverride("font_color", Accent);
			player.MouseExited += () => nameLabel.RemoveThemeColorOverride("font_color");
			var name = s.Username;
			player.Pressed += () => PlayerOverlays.OpenProfile(name);

			var mods = new[] { "DT", "HT", "SL", "BT", "RND", "NF" }.Where(m => s.Mods.Contains(m)).ToList();
			if (Math.Abs(s.Speed - 1) > 0.001) mods.Add($"{s.Speed:0.##}x");
			var modBox = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
			modBox.AddThemeConstantOverride("separation", 3);
			if (mods.Count == 0) modBox.AddChild(PlayerOverlays.Text("-", 13, new Color("#ffffff80")));
			foreach (var m in mods)
			{
				var pill = PlayerOverlays.PillLabel(m, PlayerOverlays.ModPill, 11);
				pill.AddThemeStyleboxOverride("panel", PlayerOverlays.Style(PlayerOverlays.ModPill, 5, 6, 2));
				modBox.AddChild(pill);
			}
			var when = Cell(s.Time > 0 ? PlayerApi.Ago(s.Time) : "-", 12, new Color("#ffffff80"), size: 13);
			if (s.Time > 0) { when.TooltipText = DateTimeOffset.FromUnixTimeSeconds(s.Time).LocalDateTime.ToString("f"); when.MouseFilter = MouseFilterEnum.Pass; }
			var cells = new Control[]
			{
				Cell($"#{rank}", 0, rank <= 3 ? Podium[rank - 1] : new Color("#ffffffb0"), bold: true),
				gradePill,
				player,
				Cell(Num(s.Score_), 3),
				Cell($"{acc:0.00}%", 4),
				Cell($"{Num(s.Combo)}x", 5),
				Cell(Num(s.Max), 6),
				Cell(Num(s.Good), 7),
				Cell(Num(s.Meh), 8),
				Cell(Num(s.Bad), 9),
				Cell($"{Math.Round(s.Points):0}pp", 10, Accent, bold: true),
				modBox,
				when,
			};
			rowBox.AddChild(BoardRow(cells));
			_board.AddChild(rowBox);
		}
	}

	// =====================================================================
	// Comments
	// =====================================================================

	private void CommentSection()
	{
		var v = Body(Section(), 10);
		var head = new HBoxContainer();
		head.AddThemeConstantOverride("separation", 8);
		head.AddChild(Heading("Comments"));
		_commentCount = PlayerOverlays.Text("", 16, new Color("#ffffff80"));
		_commentCount.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		head.AddChild(_commentCount);
		v.AddChild(head);

		var form = new VBoxContainer();
		form.AddThemeConstantOverride("separation", 8);
		v.AddChild(form);
		_commentText = new TextEdit
		{
			PlaceholderText = BeatmapSocialApi.LoggedIn ? "Say something about this map..." : "Log in to leave a comment.",
			CustomMinimumSize = new Vector2(0, 76),
			WrapMode = TextEdit.LineWrappingMode.Boundary,
			Editable = BeatmapSocialApi.LoggedIn,
		};
		_commentText.AddThemeFontSizeOverride("font_size", 15);
		var normal = PlayerOverlays.Style(new Color("#ffffff0d"), 12, 12, 10, new Color("#ffffff14"));
		var focus = PlayerOverlays.Style(new Color("#ffffff14"), 12, 12, 10, new Color("#f3baff99"));
		_commentText.AddThemeStyleboxOverride("normal", normal);
		_commentText.AddThemeStyleboxOverride("focus", focus);
		_commentText.AddThemeStyleboxOverride("read_only", normal);
		_commentText.TextChanged += () =>
		{
			if (_commentText.Text.Length > MaxComment)
			{
				var caret = _commentText.GetCaretColumn();
				_commentText.Text = _commentText.Text[..MaxComment];
				_commentText.SetCaretLine(_commentText.GetLineCount() - 1);
				_commentText.SetCaretColumn(Math.Min(caret, _commentText.GetLine(_commentText.GetLineCount() - 1).Length));
			}
			_commentLeft.Text = (MaxComment - _commentText.Text.Length).ToString();
		};
		_commentText.GuiInput += e =>
		{
			if (e is InputEventKey { Pressed: true, Keycode: Key.Enter, CtrlPressed: true })
			{
				PostComment();
				_commentText.AcceptEvent();
			}
		};
		form.AddChild(_commentText);

		var foot = new HBoxContainer();
		foot.AddThemeConstantOverride("separation", 12);
		_commentStatus = PlayerOverlays.Text(BeatmapSocialApi.LoggedIn ? "Ctrl+Enter to post" : "", 13, new Color("#ffffff80"), trim: true);
		_commentStatus.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		foot.AddChild(_commentStatus);
		_commentLeft = PlayerOverlays.Text(MaxComment.ToString(), 13, new Color("#ffffff80"));
		_commentLeft.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		foot.AddChild(_commentLeft);
		_send = WebButton("Post comment", primary: true);
		_send.Disabled = true;
		_send.Pressed += PostComment;
		foot.AddChild(_send);
		form.AddChild(foot);

		_comments = new VBoxContainer();
		_comments.AddThemeConstantOverride("separation", 8);
		v.AddChild(_comments);

		_more = WebButton("Show older comments");
		_more.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
		_more.Visible = false;
		_more.Pressed += LoadComments;
		v.AddChild(_more);
	}

	private Control CommentCard(BeatmapSocialApi.Comment c)
	{
		var card = PlayerOverlays.Panel(Tile, 12, 12, 10);
		card.SetMeta("comment", c.Id);
		var h = new HBoxContainer();
		h.AddThemeConstantOverride("separation", 12);
		card.AddChild(h);
		var pic = PlayerOverlays.Picture(c.Pfp, 40, 20);
		pic.SizeFlagsVertical = SizeFlags.ShrinkBegin;
		h.AddChild(pic);

		var body = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		body.AddThemeConstantOverride("separation", 2);
		h.AddChild(body);
		var head = new HBoxContainer();
		head.AddThemeConstantOverride("separation", 10);
		var name = new Button { Text = c.Username, Flat = true, FocusMode = FocusModeEnum.None, MouseDefaultCursorShape = CursorShape.PointingHand, TooltipText = $"Open {c.Username}'s profile" };
		name.AddThemeFontOverride("font", PlayerOverlays.Bold);
		name.AddThemeFontSizeOverride("font_size", 14);
		foreach (var key in new[] { "normal", "hover", "pressed", "focus" }) name.AddThemeStyleboxOverride(key, new StyleBoxEmpty());
		name.AddThemeColorOverride("font_hover_color", Accent);
		var who = c.Username;
		name.Pressed += () => PlayerOverlays.OpenProfile(who);
		head.AddChild(name);
		var when = PlayerOverlays.Text(PlayerApi.Ago(c.Created), 13, new Color("#ffffff80"));
		when.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		when.TooltipText = DateTimeOffset.FromUnixTimeSeconds(c.Created).LocalDateTime.ToString("f");
		when.MouseFilter = MouseFilterEnum.Pass;
		head.AddChild(when);
		head.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
		if (c.CanDelete)
		{
			var del = new Button { Text = "Delete", Flat = true, FocusMode = FocusModeEnum.None, MouseDefaultCursorShape = CursorShape.PointingHand, TooltipText = "Delete this comment" };
			del.AddThemeFontSizeOverride("font_size", 12);
			del.AddThemeColorOverride("font_color", new Color("#ff9a9a"));
			del.AddThemeColorOverride("font_hover_color", new Color("#ffbcbc"));
			del.AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
			del.AddThemeStyleboxOverride("hover", PlayerOverlays.Style(new Color("#ff7a7a22"), 5, 8, 2));
			bool armed = false;
			del.Pressed += async () =>
			{
				if (!armed) { armed = true; del.Text = "Really delete?"; return; } // two clicks instead of a dialog
				del.Disabled = true;
				var error = await BeatmapSocialApi.DeleteComment(c.Id);
				if (!IsInstanceValid(card)) return;
				if (error != null) { Notify.Post(error, Type: NotificationIcons.NotificationType.Warning); del.Disabled = false; return; }
				card.QueueFree();
				_commentTotal--; _commentShown--;
				PaintCommentCount();
			};
			head.AddChild(del);
		}
		body.AddChild(head);
		var text = PlayerOverlays.Text(c.Text, 14, new Color("#ffffffdd"), wrap: true);
		text.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		body.AddChild(text);
		return card;
	}

	private void PaintCommentCount()
	{
		_commentCount.Text = _commentTotal > 0 ? $"· {Num(_commentTotal)}" : "";
		_more.Visible = _commentShown < _commentTotal;
		var empty = _comments.GetNodeOrNull("Empty");
		if (_commentTotal <= 0 && empty == null)
		{
			var l = Empty("No comments yet. Say something nice!");
			l.Name = "Empty";
			_comments.AddChild(l);
		}
		else if (_commentTotal > 0) empty?.QueueFree();
	}

	private async void LoadComments()
	{
		_more.Disabled = true;
		if (_commentPage == 0) _comments.AddChild(Empty("Loading comments..."));
		var (total, list, error) = await BeatmapSocialApi.Comments(_set.id, _commentPage);
		if (!IsInstanceValid(this)) return;
		_more.Disabled = false;
		if (_commentPage == 0) foreach (var c in _comments.GetChildren()) c.QueueFree();
		if (list == null)
		{
			if (_commentPage == 0) _comments.AddChild(Empty("Couldn't load the comments. Try again in a bit."));
			return;
		}
		foreach (var c in list) _comments.AddChild(CommentCard(c));
		_commentTotal = total;
		_commentShown += list.Count;
		_commentPage++;
		PaintCommentCount();
	}

	private bool _posting;
	private async void PostComment()
	{
		var text = _commentText.Text.Trim();
		if (_posting || text.Length == 0 || !BeatmapSocialApi.LoggedIn) return;
		_posting = true;
		_send.Disabled = true;
		_commentStatus.Text = "Posting...";
		var (comment, error) = await BeatmapSocialApi.PostComment(_set.id, text, _current?.id ?? 0);
		if (!IsInstanceValid(this)) return;
		_posting = false;
		if (error != null)
		{
			_commentStatus.Text = error;
			_commentStatus.AddThemeColorOverride("font_color", new Color("#ff9a9a"));
			return;
		}
		_commentStatus.Text = "Ctrl+Enter to post";
		_commentStatus.AddThemeColorOverride("font_color", new Color("#ffffff80"));
		_commentText.Text = "";
		if (comment != null)
		{
			var card = CommentCard(comment);
			_comments.AddChild(card);
			_comments.MoveChild(card, 0);
			_commentTotal++; _commentShown++;
			PaintCommentCount();
		}
	}

	// =====================================================================
	// Data the catalog doesn't carry
	// =====================================================================

	private async void LoadExtras()
	{
		LoadComments();
		var favTask = BeatmapSocialApi.FavoriteState(_set.id);
		var diffTask = BeatmapSocialApi.Difficulties(_set.id);
		var (count, favorited) = await favTask;
		if (!IsInstanceValid(this)) return;
		_favCount = count;
		_favorited = favorited;
		PaintFavorite();
		PaintMeta();
		_details = await diffTask;
		if (!IsInstanceValid(this)) return;
		PaintStats();
	}
}
