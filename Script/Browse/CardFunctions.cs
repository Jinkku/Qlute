using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class CardFunctions : Button
{
	public SettingsOperator SettingsOperator { get; set; } = null!;
	public Label Title { get; private set; } = null!;
	public Label Artist { get; private set; } = null!;
	public Label Mapper { get; private set; } = null!;
	public int BeatmapID { get; set; }
	public int Index { get; set; }
	/// <summary>The apiv2/search (or apiv2/favorites) set this card shows.</summary>
	public BrowseCatalogLegend? Set { get; private set; }
	[Export] public bool Downloaded { get; set; } = false;
	[Export] public string? BannerPicture { get; set; }
	public TextureRect Cover { get; private set; } = null!;

	private const int Thumb = 88, Radius = 10;
	private int _status;
	private int ID { get; set; }
	private bool _isVisible, _hovered, _favorited;
	private int _favCount;
	private double _existCheck;

	private TextureRect _backdrop = null!;
	private ColorRect _stripe = null!;
	private Button _preview = null!, _fav = null!, _download = null!, _play = null!;
	private Control _actions = null!, _diffs = null!, _row = null!;
	private HBoxContainer _diffRow = null!, _head = null!;
	private PanelContainer _statusPill = null!;
	private Label _submitted = null!, _mapperPrefix = null!;
	private Button _mapperLink = null!;
	private Tween? _hoverTween;

	public static readonly Color Dim = new(1, 1, 1, 0.67f);
	private static readonly Color CardBg = PlayerOverlays.CardBg;
	private static readonly Color Pink = new("#ff6fa8");

	// ---------------------------------------------------------------- icons (beatmapcard.js HEART / DOWNLOAD)
	private const string HeartPath = "M12 21.35l-1.45-1.32C5.4 15.36 2 12.28 2 8.5 2 5.42 4.42 3 7.5 3c1.74 0 3.41.81 4.5 2.09C13.09 3.81 14.76 3 16.5 3 19.58 3 22 5.42 22 8.5c0 3.78-3.4 6.86-8.55 11.54L12 21.35z";
	private static ImageTexture? _heartHollow, _heartFull, _downloadIcon;

	private static ImageTexture Svg(string body)
	{
		var img = new Image();
		img.LoadSvgFromString($"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\" width=\"24\" height=\"24\">{body}</svg>", 2f);
		return ImageTexture.CreateFromImage(img);
	}

	private static ImageTexture HeartHollow => _heartHollow ??= Svg($"<path d=\"{HeartPath}\" fill=\"none\" stroke=\"#fff\" stroke-width=\"2.2\"/>");
	private static ImageTexture HeartFull => _heartFull ??= Svg($"<path d=\"{HeartPath}\" fill=\"#fff\"/>");
	private static ImageTexture DownloadIcon => _downloadIcon ??= Svg("<path d=\"M5 20h14v-2H5v2zM19 9h-4V3H9v6H5l7 7 7-7z\" fill=\"#fff\"/>");

	// ---------------------------------------------------------------- build

	public override void _EnterTree()
	{
		SettingsOperator = GetNode<SettingsOperator>("/root/SettingsOperator");
		if (Title != null) return; // re-entering the tree (moved between grids)
		FocusMode = FocusModeEnum.None;
		MouseDefaultCursorShape = CursorShape.PointingHand;
		CustomMinimumSize = new Vector2(CustomMinimumSize.X, Thumb);
		PaintCard(false);
		Build();
	}

	private void PaintCard(bool hover)
	{
		var s = PlayerOverlays.Style(CardBg, Radius, 0, 0, new Color(hover ? "#f3baff55" : "#ffffff0f"));
		s.ShadowColor = new Color(hover ? "#00000066" : "#00000047");
		s.ShadowSize = hover ? 16 : 8;
		s.ShadowOffset = new Vector2(0, hover ? 6 : 2);
		foreach (var key in new[] { "normal", "hover", "pressed", "hover_pressed", "disabled" }) AddThemeStyleboxOverride(key, s);
		AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
	}

	private static T Fill<T>(T c) where T : Control
	{
		c.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		c.MouseFilter = MouseFilterEnum.Ignore;
		return c;
	}

	private void Build()
	{
		// the cover, faded in behind everything (::before / ::after)
		var back = Fill(new PanelContainer { ClipChildren = ClipChildrenMode.Only });
		back.AddThemeStyleboxOverride("panel", PlayerOverlays.Style(Colors.White, Radius, 0, 0));
		AddChild(back);
		_backdrop = Fill(new TextureRect { ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered });
		_backdrop.Modulate = new Color(1, 1, 1, 0.22f);
		back.AddChild(_backdrop);
		back.AddChild(Fill(PlayerOverlays.GradientRect(new[] { CardBg, new Color(CardBg, 0.9f), new Color(CardBg, 0.7f) },
			new[] { 0f, 0.45f, 1f }, Vector2.Zero, new Vector2(1, 0))));

		_row = Fill(new HBoxContainer());
		((HBoxContainer)_row).AddThemeConstantOverride("separation", 0);
		AddChild(_row);

		// thumbnail: cover, status colour on the right edge, preview button over it
		var thumb = new PanelContainer { ClipChildren = ClipChildrenMode.AndDraw, CustomMinimumSize = new Vector2(Thumb, Thumb), MouseFilter = MouseFilterEnum.Ignore };
		var thumbStyle = PlayerOverlays.Style(PlayerOverlays.Page, 0, 0, 0);
		thumbStyle.CornerRadiusTopLeft = thumbStyle.CornerRadiusBottomLeft = Radius;
		thumb.AddThemeStyleboxOverride("panel", thumbStyle);
		_row.AddChild(thumb);
		Cover = new TextureRect { ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered, MouseFilter = MouseFilterEnum.Ignore };
		thumb.AddChild(Cover);
		var edge = new Control { MouseFilter = MouseFilterEnum.Ignore };
		thumb.AddChild(edge);
		_stripe = new ColorRect { MouseFilter = MouseFilterEnum.Ignore };
		_stripe.SetAnchorsAndOffsetsPreset(LayoutPreset.RightWide);
		_stripe.OffsetLeft = -3;
		edge.AddChild(_stripe);
		_preview = new Button { Icon = PlayerOverlays.SvgIcon(PlayerOverlays.PlayGlyph), ExpandIcon = true, IconAlignment = HorizontalAlignment.Center,
			TooltipText = "Play preview", FocusMode = FocusModeEnum.None, MouseDefaultCursorShape = CursorShape.PointingHand };
		_preview.AddThemeConstantOverride("icon_max_width", 18);
		_preview.AddThemeFontSizeOverride("font_size", 17);
		_preview.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 1));
		_preview.AddThemeConstantOverride("shadow_outline_size", 4);
		foreach (var key in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_hover_pressed_color" }) _preview.AddThemeColorOverride(key, Colors.White);
		_preview.AddThemeStyleboxOverride("normal", PlayerOverlays.Style(new Color("#0000007a"), 0, 0, 0));
		_preview.AddThemeStyleboxOverride("hover", PlayerOverlays.Style(new Color("#f3baff66"), 0, 0, 0));
		_preview.AddThemeStyleboxOverride("pressed", PlayerOverlays.Style(new Color("#f3baff66"), 0, 0, 0));
		_preview.AddThemeStyleboxOverride("hover_pressed", PlayerOverlays.Style(new Color("#f3baff66"), 0, 0, 0));
		_preview.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
		_preview.Modulate = new Color(1, 1, 1, 0);
		_preview.Pressed += () => { if (Set != null) PreviewPlayer.Toggle(Set.preview_url, Set.id); };
		thumb.AddChild(_preview);

		// body
		var margin = new MarginContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore };
		margin.AddThemeConstantOverride("margin_left", 12);
		margin.AddThemeConstantOverride("margin_top", 9);
		margin.AddThemeConstantOverride("margin_right", 10);
		margin.AddThemeConstantOverride("margin_bottom", 10);
		_row.AddChild(margin);
		var body = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = MouseFilterEnum.Ignore };
		body.AddThemeConstantOverride("separation", 1);
		margin.AddChild(body);

		_head = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
		_head.AddThemeConstantOverride("separation", 7);
		body.AddChild(_head);
		Title = PlayerOverlays.Text("", 14, bold: true, trim: true);
		_head.AddChild(Title);
		_statusPill = new PanelContainer { SizeFlagsVertical = SizeFlags.ShrinkCenter, MouseFilter = MouseFilterEnum.Ignore };
		_statusPill.AddChild(PlayerOverlays.Text("", 9, bold: true));
		_head.AddChild(_statusPill);
		_head.Resized += FitTitle;

		Artist = PlayerOverlays.Text("", 12, new Color("#ffffffd0"), trim: true);
		Artist.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		body.AddChild(Artist);

		var mapperRow = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
		mapperRow.AddThemeConstantOverride("separation", 0);
		body.AddChild(mapperRow);
		_mapperPrefix = PlayerOverlays.Text("mapped by ", 11, new Color("#ffffff80"));
		mapperRow.AddChild(_mapperPrefix);
		Mapper = PlayerOverlays.Text("", 11, PlayerOverlays.Accent, trim: true);
		Mapper.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		mapperRow.AddChild(Mapper);
		// a claimed / self-uploaded set's mapper opens their profile (catalog.py mapper_user)
		_mapperLink = new Button { Flat = true, Visible = false, FocusMode = FocusModeEnum.None, MouseDefaultCursorShape = CursorShape.PointingHand, ClipText = true, Alignment = HorizontalAlignment.Left };
		_mapperLink.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		_mapperLink.AddThemeFontSizeOverride("font_size", 11);
		foreach (var key in new[] { "normal", "hover", "pressed", "hover_pressed", "focus" }) _mapperLink.AddThemeStyleboxOverride(key, new StyleBoxEmpty());
		_mapperLink.AddThemeColorOverride("font_color", PlayerOverlays.Accent);
		_mapperLink.AddThemeColorOverride("font_hover_color", Colors.White);
		_mapperLink.AddThemeColorOverride("font_pressed_color", Colors.White);
		_mapperLink.Pressed += () => { if (!string.IsNullOrEmpty(Set?.mapper_user)) PlayerOverlays.OpenProfile(Set.mapper_user); };
		mapperRow.AddChild(_mapperLink);

		_submitted = PlayerOverlays.Text("", 11, new Color("#ffffff59"), trim: true);
		_submitted.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		body.AddChild(_submitted);

		// level pills: one line, clipped (catalog-diffs is nowrap + overflow hidden)
		_diffs = new Control { CustomMinimumSize = new Vector2(0, 20), ClipContents = true, MouseFilter = MouseFilterEnum.Ignore };
		body.AddChild(_diffs);
		_diffRow = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore, Position = new Vector2(0, 5) };
		_diffRow.AddThemeConstantOverride("separation", 4);
		_diffs.AddChild(_diffRow);

		// actions: favourite + download / play, over a fade on the right
		// inside a rounded mask, so the fade keeps the card's right corners round
		var actionsClip = Fill(new Panel { ClipChildren = ClipChildrenMode.Only });
		actionsClip.AddThemeStyleboxOverride("panel", PlayerOverlays.Style(Colors.White, Radius, 0, 0));
		AddChild(actionsClip);
		_actions = new Control { MouseFilter = MouseFilterEnum.Ignore };
		_actions.SetAnchorsAndOffsetsPreset(LayoutPreset.RightWide);
		actionsClip.AddChild(_actions);
		var fade = PlayerOverlays.GradientRect(new[] { new Color(CardBg, 0), CardBg, CardBg }, new[] { 0f, 0.35f, 1f }, Vector2.Zero, new Vector2(1, 0));
		_actions.AddChild(Fill(fade));
		var buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End, MouseFilter = MouseFilterEnum.Ignore };
		buttons.AddThemeConstantOverride("separation", 6);
		buttons.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		buttons.OffsetLeft = 40;
		buttons.OffsetRight = -10;
		_actions.AddChild(buttons);

		_fav = RoundButton(HeartHollow);
		_fav.Pressed += ToggleFavorite;
		buttons.AddChild(_fav);
		_download = RoundButton(DownloadIcon, new Color("#f3baff40"), new Color("#f3baff66"), new Color("#f3baffaa"));
		_download.TooltipText = "Download";
		_download.Pressed += () => { if (Set != null) ApiOperator.DownloadBeatmap(Set); };
		buttons.AddChild(_download);
		_play = PlayerOverlays.PillButton("Play", primary: true, "Already downloaded: open it in song select", 13);
		_play.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		_play.Visible = false;
		_play.Pressed += PlayLocal;
		buttons.AddChild(_play);
		// width of the strip: fade (40) + buttons + right padding (10)
		buttons.MinimumSizeChanged += () => { _actionsWidth = buttons.GetCombinedMinimumSize().X + 50; SetActions(_actions.Modulate.A); };
		_actionsWidth = 120;
		SetActions(0);

		Pressed += OpenInfo;
		_row.MinimumSizeChanged += FitHeight;
	}

	private static Button RoundButton(Texture2D icon, Color? bg = null, Color? hover = null, Color? border = null)
	{
		var b = new Button { Icon = icon, ExpandIcon = true, FocusMode = FocusModeEnum.None, MouseDefaultCursorShape = CursorShape.PointingHand,
			CustomMinimumSize = new Vector2(32, 32), SizeFlagsVertical = SizeFlags.ShrinkCenter, IconAlignment = HorizontalAlignment.Center };
		b.AddThemeConstantOverride("icon_max_width", 16);
		PaintRound(b, bg ?? new Color("#ffffff14"), hover ?? new Color("#ffffff2e"), border ?? new Color("#ffffff2e"));
		return b;
	}

	private static void PaintRound(Button b, Color bg, Color hover, Color border)
	{
		StyleBox S(Color c) => new PillBox(c, border, 7, 7); // one smooth ring (a StyleBoxFlat border shows light joins)
		b.AddThemeStyleboxOverride("normal", S(bg));
		b.AddThemeStyleboxOverride("hover", S(hover));
		b.AddThemeStyleboxOverride("pressed", S(hover));
		b.AddThemeStyleboxOverride("hover_pressed", S(hover));
		b.AddThemeStyleboxOverride("disabled", S(new Color(bg, bg.A * 0.5f)));
		b.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
	}

	/// <summary>A Button doesn't grow with its children, so follow the layout's height by hand.</summary>
	private void FitHeight() => CustomMinimumSize = new Vector2(CustomMinimumSize.X, Mathf.Max(Thumb, _row.GetCombinedMinimumSize().Y));

	/// <summary>The title takes its text width (up to what's left), so the status pill sits right after it like the website.</summary>
	private void FitTitle()
	{
		if (Title == null) return;
		var font = Title.GetThemeFont("font");
		var want = font.GetStringSize(Title.Text, HorizontalAlignment.Left, -1, Title.GetThemeFontSize("font_size")).X + 2;
		var room = _head.Size.X - _statusPill.GetCombinedMinimumSize().X - 7;
		var w = Mathf.Max(0, Mathf.Min(want, room));
		if (!Mathf.IsEqualApprox(Title.CustomMinimumSize.X, w)) Title.CustomMinimumSize = new Vector2(w, 0);
	}

	public override void _Ready()
	{
		var startAnimation = CreateTween();
		startAnimation.TweenProperty(this, "modulate", new Color(1, 1, 1, 1), 0.4f)
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.Out);
		FitHeight();
		Existance();
	}

	public override void _ExitTree()
	{
		if (Set != null && PreviewPlayer.IsPlaying(Set.id)) PreviewPlayer.Stop();
	}

	// ---------------------------------------------------------------- data

	/// <summary>Fills the card from one apiv2/search set. <paramref name="status"/> is the qlute status to show.</summary>
	public void Setup(BrowseCatalogLegend set, int index, int status)
	{
		Set = set;
		Index = index;
		BeatmapID = set.id;
		_status = status;
		BannerPicture = set.covers?.card;
		if (!string.IsNullOrEmpty(set.preview_url) && set.preview_url.StartsWith("//")) set.preview_url = "https:" + set.preview_url;
		_preview.Visible = !string.IsNullOrEmpty(set.preview_url);

		Title.Text = set.title ?? "";
		TooltipText = $"{set.artist} - {set.title}";
		Artist.Text = set.artist ?? "";
		var creator = set.creator ?? "unknown";
		bool linked = !string.IsNullOrEmpty(set.mapper_user);
		Mapper.Text = creator;
		Mapper.Visible = !linked;
		_mapperLink.Text = creator;
		_mapperLink.TooltipText = linked ? $"{set.mapper_user}'s profile" : "";
		_mapperLink.Visible = linked;

		_submitted.Visible = set.last_updated > 0;
		if (set.last_updated > 0)
			_submitted.Text = "submitted " + DateTimeOffset.FromUnixTimeSeconds((long)set.last_updated).LocalDateTime.ToString("MMM d, yyyy");

		// status pill + the matching colour on the thumbnail edge
		var accent = PlayerOverlays.StatusAccent(status);
		_stripe.Color = accent;
		_statusPill.AddThemeStyleboxOverride("panel", new PillBox(new Color(accent, 0.16f), new Color(accent, 0.45f), 6, 0));
		var statusLabel = _statusPill.GetChild<Label>(0);
		statusLabel.Text = StatusName(status).ToUpperInvariant();
		statusLabel.AddThemeColorOverride("font_color", accent);
		CallDeferred(MethodName.FitTitle);

		foreach (Node c in _diffRow.GetChildren()) c.QueueFree();
		var diffs = (set.beatmaps ?? new List<CatalogBeatmapInfoLegend>()).OrderBy(d => d.level).ToList();
		foreach (var d in diffs.Take(8))
		{
			var tip = (d.recommended ? "Recommended for you - " : "") + (d.version ?? "") + (d.pp > 0 ? $" - {Math.Round(d.pp)}pp" : "");
			_diffRow.AddChild(LevelPill(d.level, tip, d.recommended));
		}
		if (diffs.Count > 8) _diffRow.AddChild(PlayerOverlays.Text($"+{diffs.Count - 8}", 10, new Color("#ffffff80")));
		if (set.nsfw) _diffRow.AddChild(Badge("EXPLICIT", true));
		if (Browse.ShowVideoStoryboard && set.video) _diffRow.AddChild(Badge("VIDEO", false));
		if (Browse.ShowVideoStoryboard && set.storyboard) _diffRow.AddChild(Badge("STORYBOARD", false));

		_favorited = set.favorited;
		_favCount = set.favourite_count;
		PaintFavorite();
	}

	public static string StatusName(int status) => status switch
	{
		1 => "Ranked",
		2 or 4 => "Special",
		_ => "Unranked",
	};

	public static Color StatusColour(int status) => PlayerOverlays.StatusAccent(status);

	/// <summary>".catalog-diffs .mod": a glowing dot + "Lv N" in the level's colour (black from Lv 70).</summary>
	public static Control LevelPill(double level, string tooltip = "", bool recommended = false, int size = 10)
	{
		bool black = PlayerOverlays.LevelIsBlack(level);
		var lv = PlayerOverlays.LevelColor(level);
		Color bg = black ? Colors.Black : new Color(lv, 0.14f), border = black ? new Color("#ffffff40") : new Color(lv, 0.3f), text = lv;
		if (recommended) { bg = new Color("#f3baff38"); border = new Color("#f3baffcc"); text = Colors.White; }
		var p = new PanelContainer();
		var style = new PillBox(bg, border, 6, 0);
		style.ContentMarginLeft = 5 + style.BorderWidth;
		p.AddThemeStyleboxOverride("panel", style);
		p.TooltipText = tooltip;
		p.MouseFilter = string.IsNullOrEmpty(tooltip) ? MouseFilterEnum.Ignore : MouseFilterEnum.Pass;
		p.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		var h = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
		h.AddThemeConstantOverride("separation", 4);
		p.AddChild(h);
		var dot = new Panel { CustomMinimumSize = new Vector2(5, 5), SizeFlagsVertical = SizeFlags.ShrinkCenter, MouseFilter = MouseFilterEnum.Ignore };
		var dotStyle = PlayerOverlays.Style(black ? Colors.White : lv, 3, 0, 0);
		dotStyle.ShadowColor = new Color(black ? PlayerOverlays.Accent : lv, 0.8f);
		dotStyle.ShadowSize = 3;
		dot.AddThemeStyleboxOverride("panel", dotStyle);
		h.AddChild(dot);
		h.AddChild(PlayerOverlays.Text($"Lv {level:0}", size, text));
		return p;
	}

	private static PanelContainer Badge(string text, bool warn)
	{
		var p = PlayerOverlays.Panel(Colors.Transparent, 4, 6, 0);
		var s = PlayerOverlays.Style(Colors.Transparent, 4, 6, 0, warn ? new Color("#ff7a7a99") : new Color("#ffffff33"));
		s.DrawCenter = false;
		p.AddThemeStyleboxOverride("panel", s);
		p.MouseFilter = MouseFilterEnum.Ignore;
		p.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		p.AddChild(PlayerOverlays.Text(text, 9, warn ? new Color("#ff9a9a") : new Color("#ffffffbb"), bold: true));
		return p;
	}

	// ---------------------------------------------------------------- favourite (apiv2/favorites/add|remove)

	private void PaintFavorite()
	{
		_fav.Icon = _favorited ? HeartFull : HeartHollow;
		if (_favorited) PaintRound(_fav, new Color(Pink, 0.25f), new Color(Pink, 0.33f), new Color(Pink, 0.67f));
		else PaintRound(_fav, new Color("#ffffff14"), new Color(Pink, 0.2f), new Color("#ffffff2e"));
		var label = !BeatmapSocialApi.LoggedIn ? "Log in to favourite" : _favorited ? "Remove from favourites" : "Add to favourites";
		_fav.TooltipText = label + (_favCount > 0 ? $" ({_favCount:N0})" : "");
	}

	private async void ToggleFavorite()
	{
		if (Set == null) return;
		if (!BeatmapSocialApi.LoggedIn) { Notify.Post("Log in to favourite beatmaps."); return; }
		_fav.Disabled = true;
		var (on, count, error) = await BeatmapSocialApi.SetFavorite(Set.id, !_favorited);
		if (!IsInstanceValid(this)) return;
		_fav.Disabled = false;
		if (error != null) { Notify.Post(error, Type: NotificationIcons.NotificationType.Warning); return; }
		_favorited = Set.favorited = on;
		_favCount = Set.favourite_count = count;
		PaintFavorite();
		if (on)
		{
			_fav.PivotOffset = _fav.Size / 2;
			_fav.Scale = new Vector2(0.6f, 0.6f);
			var t = CreateTween();
			t.TweenProperty(_fav, "scale", new Vector2(1.35f, 1.35f), 0.2f).SetEase(Tween.EaseType.Out);
			t.TweenProperty(_fav, "scale", Vector2.One, 0.15f);
		}
	}

	// ---------------------------------------------------------------- hover, cover loading, downloaded state

	private float _actionsWidth;

	/// <summary>t = 0 hidden, 1 shown: fades in while sliding 10px from the right.</summary>
	private void SetActions(float t)
	{
		_actions.Modulate = new Color(1, 1, 1, t);
		var shift = 10 * (1 - t);
		_actions.OffsetLeft = -_actionsWidth + shift;
		_actions.OffsetRight = shift;
		var filter = t > 0.5f ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
		_fav.MouseFilter = _download.MouseFilter = _play.MouseFilter = filter;
	}

	private void SetHovered(bool on)
	{
		_hovered = on;
		PaintCard(on);
		_hoverTween?.Kill();
		_hoverTween = CreateTween().SetParallel().SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
		_hoverTween.TweenProperty(_backdrop, "modulate:a", on ? 0.36f : 0.22f, 0.2f);
		_hoverTween.TweenProperty(_preview, "modulate:a", on ? 1f : 0f, 0.15f);
		_hoverTween.TweenMethod(Callable.From<float>(SetActions), _actions.Modulate.A, on ? 1f : 0f, 0.16f);
	}

	private bool IsOnScreen() => GetViewport().GetVisibleRect().Intersects(GetGlobalRect());

	public override void _Process(double delta)
	{
		// Polled: entering a child button counts as leaving the card for mouse_exited.
		bool over = IsVisibleInTree() && GetGlobalRect().HasPoint(GetGlobalMousePosition());
		if (over != _hovered) SetHovered(over);
		if (Set != null)
		{
			bool playing = PreviewPlayer.IsPlaying(Set.id);
			_preview.Icon = PlayerOverlays.SvgIcon(playing ? PlayerOverlays.PauseGlyph : PlayerOverlays.PlayGlyph);
			if (playing && !_hovered) _preview.Modulate = Colors.White;
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		// The downloaded check walks every installed map, so don't do it every tick.
		_existCheck += delta;
		if (_existCheck > 0.5)
		{
			_existCheck = 0;
			Existance();
		}

		bool onScreen = IsOnScreen();
		if (onScreen && !_isVisible)
		{
			_isVisible = true;
			LoadBanner().ContinueWith(t =>
			{
				if (t.IsFaulted)
					GD.PrintErr($"[CardFunctions] LoadBanner failed: {t.Exception?.GetBaseException().Message}");
			}, TaskScheduler.FromCurrentSynchronizationContext());
		}
		else if (!onScreen && _isVisible)
		{
			_isVisible = false;
			UnloadBanner();
		}
	}

	private async Task LoadBanner()
	{
		if (string.IsNullOrEmpty(BannerPicture)) return;
		await ApiOperator.DownloadImage(BannerPicture, (ImageTexture texture) =>
		{
			// Node may have scrolled off-screen while we were awaiting
			if (!IsInstanceValid(this) || !_isVisible)
			{
				texture.Dispose();
				return;
			}
			Cover.Texture = texture;
			_backdrop.Texture = texture;
		});
	}

	private void UnloadBanner()
	{
		var old = Cover.Texture as ImageTexture;
		Cover.Texture = null;
		_backdrop.Texture = null;
		old?.Dispose();
	}

	/// <summary>Checks if the beatmap is already downloaded: Play instead of Download.</summary>
	private void Existance()
	{
		var beatmap = SettingsOperator.Beatmaps.FirstOrDefault(b => b.BeatmapSetID == BeatmapID);
		Downloaded = beatmap != null;
		if (beatmap != null) ID = beatmap.ID;
		_download.Visible = !Downloaded;
		_play.Visible = Downloaded;
	}

	private void PlayLocal()
	{
		SettingsOperator.SelectSongID(ID);
		GetNode<SceneTransition>("/root/Transition").Switch("res://Panels/Screens/song_select.tscn");
	}

	private void OpenInfo()
	{
		// A copy: the card frees its own texture when it scrolls out of view.
		var cover = Cover.Texture is ImageTexture tex && tex.GetImage() is Image img ? ImageTexture.CreateFromImage(img) : null;
		PlayerOverlays.OpenBeatmap(Set, cover);
	}
}
