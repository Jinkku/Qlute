using Godot;
using System.Collections.Generic;
using System.Linq;

/// <summary>Drop-down list of everyone online, in the website's style. Click a row to open the profile.</summary>
public partial class PlayerListPanel : PanelContainer
{
	private const double RefreshEvery = 10;
	private const float Width = 380;

	public bool Closing { get; private set; }

	private Label _count;
	private Label _hint;
	private LineEdit _search;
	private VBoxContainer _rows;
	private List<PlayerApi.Player> _players = new();
	private double _refresh;
	private bool _loading;
	private Tween _tween;

	public override void _Ready()
	{
		AddThemeStyleboxOverride("panel", PlayerOverlays.Style(PlayerOverlays.Page, 0, 0, 0));
		var view = GetViewportRect().Size;
		CustomMinimumSize = new Vector2(Width, 0);
		Size = new Vector2(Width, Mathf.Min(640, view.Y * 0.75f));
		Position = new Vector2(view.X - Width - 10, Top() - Size.Y);

		var v = new VBoxContainer();
		v.AddThemeConstantOverride("separation", 0);
		AddChild(v);

		var bar = PlayerOverlays.TitleBar(v, "players online");
		var me = PlayerOverlays.FlatButton("my profile");
		me.Disabled = !SettingsOperator.SessionConfig.Loggedin;
		me.Pressed += () => PlayerOverlays.OpenProfile(ApiOperator.Username);
		bar.AddChild(me);
		var close = PlayerOverlays.FlatButton("✕", "Close");
		close.Pressed += Close;
		bar.AddChild(close);

		var box = PlayerOverlays.Panel(PlayerOverlays.Box, 0, 15, 12);
		box.SizeFlagsVertical = SizeFlags.ExpandFill;
		v.AddChild(box);
		var inner = new VBoxContainer();
		inner.AddThemeConstantOverride("separation", 10);
		box.AddChild(inner);

		var head = new HBoxContainer();
		inner.AddChild(head);
		var title = PlayerOverlays.Text("Online", 18, bold: true);
		title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		head.AddChild(title);
		_count = PlayerOverlays.Text("", 18, bold: true);
		head.AddChild(_count);

		_search = new LineEdit { PlaceholderText = "Search players...", ClearButtonEnabled = true, TooltipText = "Filters the list. Press Enter to open any player's profile, online or not." };
		_search.AddThemeFontSizeOverride("font_size", 13);
		_search.AddThemeStyleboxOverride("normal", PlayerOverlays.Style(PlayerOverlays.Card, 5, 10, 6));
		_search.AddThemeStyleboxOverride("focus", PlayerOverlays.Style(PlayerOverlays.CardHover, 5, 10, 6));
		_search.TextChanged += _ => Rebuild();
		_search.TextSubmitted += t => { if (!string.IsNullOrWhiteSpace(t)) PlayerOverlays.OpenProfile(t.Trim()); };
		inner.AddChild(_search);

		_hint = PlayerOverlays.Text("Loading...", 13, PlayerOverlays.Dim, wrap: true);
		inner.AddChild(_hint);

		var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
		inner.AddChild(scroll);
		_rows = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		_rows.AddThemeConstantOverride("separation", 10);
		scroll.AddChild(_rows);

		_tween = CreateTween().SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		_tween.TweenProperty(this, "position:y", Top() + 8, 0.25f);
		Load();
	}

	private static float Top() => Mathf.Max(50, SettingsOperator.TopPanelPosition);

	public void Close()
	{
		if (Closing) return;
		Closing = true;
		_tween?.Kill();
		_tween = CreateTween().SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
		_tween.SetParallel(true);
		_tween.TweenProperty(this, "position:y", Top() - Size.Y, 0.2f);
		_tween.TweenProperty(this, "modulate:a", 0f, 0.2f);
		_tween.Chain().TweenCallback(Callable.From(QueueFree));
	}

	public override void _Process(double delta)
	{
		_refresh -= delta;
		if (_refresh <= 0) Load();
	}

	public override void _Input(InputEvent e)
	{
		if (e.IsActionPressed("ui_cancel")) { Close(); GetViewport().SetInputAsHandled(); }
	}

	private async void Load()
	{
		_refresh = RefreshEvery;
		if (_loading) return;
		_loading = true;
		var result = await PlayerApi.FetchOnline();
		_loading = false;
		if (!IsInstanceValid(this)) return;
		if (result == null)
		{
			_hint.Text = "Can't reach the server right now. Retrying...";
			_hint.Visible = true;
			return;
		}
		_players = result.Value.players;
		_count.Text = result.Value.count.ToString("N0");
		Rebuild();
	}

	private void Rebuild()
	{
		foreach (var c in _rows.GetChildren()) c.QueueFree();
		var q = _search.Text.Trim();
		var shown = _players.Where(p => q.Length == 0 || p.Username.Contains(q, System.StringComparison.OrdinalIgnoreCase))
			.OrderByDescending(p => p.Username == ApiOperator.Username)
			.ThenBy(p => p.Rank > 0 ? p.Rank : long.MaxValue)
			.ThenBy(p => p.Username, System.StringComparer.OrdinalIgnoreCase)
			.ToList();
		_hint.Visible = shown.Count == 0;
		_hint.Text = _players.Count == 0 ? "Nobody is online right now -n-" : $"Nobody online matches \"{q}\". Press Enter to look them up anyway.";
		foreach (var p in shown) _rows.AddChild(Row(p));
	}

	// Same look as the game's own UserCard (Panels/UserCard/UserCard.tscn).
	private static readonly Color CardBg = new(0.19f, 0.19f, 0.19f);
	private static readonly Color CardBgHover = new(0.25f, 0.25f, 0.25f);
	private static readonly Color CardTop = new(0, 0, 0, 0.25f);
	private static readonly Color StatName = new(0.546875f, 0.861938f, 1);
	private static readonly Color StatValue = new(0.785156f, 0.828796f, 1);

	private static StyleBoxFlat Rounded(Color bg, int margin = 0)
	{
		var s = PlayerOverlays.Style(bg, 15, margin, margin);
		return s;
	}

	/// <summary>One player, laid out like the in-game user card: picture + name on top, rank and pp below.</summary>
	private static Control Row(PlayerApi.Player p)
	{
		var normal = Rounded(CardBg);
		var hover = Rounded(CardBgHover);
		var card = new PanelContainer { MouseFilter = MouseFilterEnum.Stop, MouseDefaultCursorShape = CursorShape.PointingHand, TooltipText = $"Open {p.Username}'s profile" };
		card.AddThemeStyleboxOverride("panel", normal);

		var split = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
		split.AddThemeConstantOverride("separation", 0);
		card.AddChild(split);

		// top: picture (top-left and bottom-right corners rounded) + name and status
		var top = new PanelContainer { CustomMinimumSize = new Vector2(0, 60), MouseFilter = MouseFilterEnum.Ignore };
		top.AddThemeStyleboxOverride("panel", Rounded(CardTop));
		split.AddChild(top);
		var topRow = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
		topRow.AddThemeConstantOverride("separation", 15);
		top.AddChild(topRow);

		var pic = PlayerOverlays.Picture(p.Pfp, 60, 0);
		var corners = PlayerOverlays.Style(Colors.White, 0, 0, 0);
		corners.CornerRadiusTopLeft = corners.CornerRadiusBottomRight = 15;
		pic.AddThemeStyleboxOverride("panel", corners);
		topRow.AddChild(pic);

		var info = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = MouseFilterEnum.Ignore };
		info.AddThemeConstantOverride("separation", 2);
		info.AddChild(PlayerOverlays.Text(p.Username + (p.Username == ApiOperator.Username ? " (you)" : ""), 13, trim: true));
		var statusRow = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
		statusRow.AddThemeConstantOverride("separation", 6);
		statusRow.AddChild(new ColorRect { Color = p.Online ? PlayerOverlays.OnlineDot : PlayerOverlays.OfflineDot, CustomMinimumSize = new Vector2(7, 7), SizeFlagsVertical = SizeFlags.ShrinkCenter, MouseFilter = MouseFilterEnum.Ignore });
		var status = PlayerOverlays.Text(PlayerOverlays.StatusLine(p), 11, PlayerOverlays.Dim, trim: true);
		status.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		statusRow.AddChild(status);
		info.AddChild(statusRow);
		var pad = new MarginContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore };
		pad.AddThemeConstantOverride("margin_right", 10);
		pad.AddChild(info);
		topRow.AddChild(pad);

		// bottom: Global Rank | Performance
		var bottom = new HBoxContainer { CustomMinimumSize = new Vector2(0, 56), MouseFilter = MouseFilterEnum.Ignore };
		bottom.AddThemeConstantOverride("separation", 15);
		split.AddChild(bottom);
		bottom.AddChild(Stat("Global Rank", $"#{p.Rank:N0}"));
		bottom.AddChild(Stat("Performance", $"{p.Points:N0}pp"));

		var name = p.Username;
		card.MouseEntered += () => card.AddThemeStyleboxOverride("panel", hover);
		card.MouseExited += () => card.AddThemeStyleboxOverride("panel", normal);
		card.GuiInput += e =>
		{
			if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false })
				PlayerOverlays.OpenProfile(name);
		};
		return card;
	}

	private static Control Stat(string caption, string value)
	{
		var v = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = MouseFilterEnum.Ignore };
		v.AddThemeConstantOverride("separation", 0);
		var n = PlayerOverlays.Text(caption, 13, StatName);
		n.HorizontalAlignment = HorizontalAlignment.Center;
		v.AddChild(n);
		var x = PlayerOverlays.Text(value, 13, StatValue, trim: true);
		x.HorizontalAlignment = HorizontalAlignment.Center;
		v.AddChild(x);
		return v;
	}
}
