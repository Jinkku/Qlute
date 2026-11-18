using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Marathon setup. Left: difficulty + length pills, stat tiles, Start. Right: the setlist (one song per artist,
/// easiest first), or how the last run went. Playing / ending a run is <see cref="MarathonRun"/>.
/// The scene keeps its title bar and Back button; everything else is built here.
/// </summary>
public partial class MarathonMode : ColorRect
{
	// ------------------------------------------------------------------ data

	/// <summary>Level ranges (inclusive). Expert+ is open-ended: levels go up to 100.</summary>
	private static readonly (int min, int max, string name, Color color)[] Ranges =
	{
		(0, 3, "Easy", new Color("#6fd99a")),
		(4, 6, "Normal", new Color("#6cbcf5")),
		(7, 12, "Hard", new Color("#f5cf6c")),
		(13, 24, "Expert", new Color("#f5946c")),
		(25, 100, "Expert+", new Color("#f56c9c")),
	};
	private static readonly (int count, string name)[] Lengths =
	{
		(5, "5 songs"),
		(10, "10 songs"),
		(20, "20 songs"),
		(int.MaxValue, "Every song"),
	};

	// Kept while the game runs, so coming back here shows the same setup.
	private static int DiffCount = 0;
	private static int LengthIndex = 1;
	private static int Seed = System.Environment.TickCount;
	/// <summary>The run whose summary was already opened, so the "Last run" tab only jumps open right after a run.</summary>
	private static MarathonRun.Summary _seenRun;

	private readonly List<int> Picked = new();

	// ------------------------------------------------------------------ style

	private static readonly Color Card = new(0.235f, 0.2f, 0.27f);
	private static readonly Color Inset = new(0f, 0f, 0f, 0.22f);
	private static readonly Color Accent = new("#f3baff");
	private static readonly Color Purple = new("#983aaf");
	private static readonly Color PurpleHover = new("#b65acc");
	private static readonly Color Dim = new(1f, 1f, 1f, 0.6f);
	private static Font _bold;
	private static Font Bold => _bold ??= GD.Load<Font>("res://Fonts/font-bold.otf");

	private static StyleBoxFlat Box(Color bg, int radius = 18, int padX = 18, int padY = 15, Color? border = null, int borderWidth = 0)
	{
		var s = new StyleBoxFlat
		{
			BgColor = bg,
			ContentMarginLeft = padX, ContentMarginRight = padX, ContentMarginTop = padY, ContentMarginBottom = padY,
			AntiAliasing = true,
		};
		s.SetCornerRadiusAll(radius);
		if (border is { } b)
		{
			s.BorderColor = b;
			s.SetBorderWidthAll(borderWidth);
		}
		return s;
	}

	private static Label Text(string text, int size = 18, Color? color = null, bool bold = false)
	{
		var l = new Label { Text = text, MouseFilter = MouseFilterEnum.Ignore };
		l.AddThemeFontSizeOverride("font_size", size);
		if (color is { } c) l.AddThemeColorOverride("font_color", c);
		if (bold) l.AddThemeFontOverride("font", Bold);
		return l;
	}

	private static Label Caption(string text) => Text(text.ToUpperInvariant(), 15, new Color(Accent, 0.75f), bold: true);

	private static void StyleButton(Button b, Color normal, Color hover, Color pressed, Color? border = null, int radius = 18)
	{
		int bw = border == null ? 0 : 3;
		b.AddThemeStyleboxOverride("normal", Box(normal, radius, 13, 10, border, bw));
		b.AddThemeStyleboxOverride("hover", Box(hover, radius, 13, 10, border, bw));
		b.AddThemeStyleboxOverride("pressed", Box(pressed, radius, 13, 10, border, bw));
		b.AddThemeStyleboxOverride("hover_pressed", Box(pressed, radius, 13, 10, border, bw));
		b.AddThemeStyleboxOverride("disabled", Box(new Color(normal, normal.A * 0.4f), radius, 13, 10));
		b.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
	}

	// ------------------------------------------------------------------ nodes

	public SettingsOperator SettingsOperator { get; set; }
	private readonly List<Button> DiffButtons = new();
	private readonly List<Button> LengthButtons = new();
	private Label SongsValue, DurationValue, LevelsValue, ListCount;
	private Button Start, Shuffle, SetlistTab, LastTab;
	private VBoxContainer List;
	private ScrollContainer ListScroll;
	private bool ShowingLast;

	public override void _Ready()
	{
		SettingsOperator = GetNode<SettingsOperator>("/root/SettingsOperator");
		SettingsOperator.Marathon = true; // the loading screen's Back comes here while this is set
		GetNode<Control>("Panel").Visible = false; // the old layout; replaced below
		AddSubtitle();
		Build();
		ShowingLast = MarathonRun.Last is { } last && last.Songs.Count > 0 && _seenRun != last;
		if (ShowingLast) _seenRun = MarathonRun.Last;
		Refresh();
	}

	private void AddSubtitle()
	{
		var sub = Text("Songs back to back. Esc pauses, and quitting keeps what you've played.", 19, Dim);
		sub.HorizontalAlignment = HorizontalAlignment.Right;
		sub.VerticalAlignment = VerticalAlignment.Bottom;
		var margin = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore };
		margin.AddThemeConstantOverride("margin_right", 35);
		margin.AddThemeConstantOverride("margin_bottom", 28);
		margin.AddChild(sub);
		GetNode<Control>("Title").AddChild(margin);
	}

	private void Build()
	{
		var root = new MarginContainer { Name = "MarathonUI" };
		root.SetAnchorsPreset(LayoutPreset.FullRect);
		root.OffsetTop = 187.5f;
		root.OffsetBottom = -62.5f;
		foreach (var (side, v) in new[] { ("left", 30), ("right", 30), ("top", 23), ("bottom", 20) })
			root.AddThemeConstantOverride("margin_" + side, v);
		AddChild(root);
		MoveChild(root, GetNode("Title").GetIndex()); // under the title bar and the Back button

		var columns = new HBoxContainer();
		columns.AddThemeConstantOverride("separation", 25);
		root.AddChild(columns);

		// ---- left: setup
		var left = new PanelContainer { CustomMinimumSize = new Vector2(587.5f, 0) };
		left.AddThemeStyleboxOverride("panel", Box(Card, 25, 25, 23));
		columns.AddChild(left);
		var setup = new VBoxContainer();
		setup.AddThemeConstantOverride("separation", 13);
		left.AddChild(setup);

		setup.AddChild(Caption("Difficulty"));
		var diffRow = new HBoxContainer();
		diffRow.AddThemeConstantOverride("separation", 10);
		setup.AddChild(diffRow);
		var diffGroup = new ButtonGroup();
		for (int i = 0; i < Ranges.Length; i++)
		{
			var (min, max, name, color) = Ranges[i];
			var b = PillButton(diffGroup, name, max >= 100 ? $"Lv {min}+" : $"Lv {min}-{max}", color);
			int index = i;
			b.Pressed += () => { DiffCount = index; ShowingLast = false; Refresh(); };
			diffRow.AddChild(b);
			DiffButtons.Add(b);
		}

		setup.AddChild(Spacer(8));
		setup.AddChild(Caption("Length"));
		var lengthRow = new HBoxContainer();
		lengthRow.AddThemeConstantOverride("separation", 10);
		setup.AddChild(lengthRow);
		var lengthGroup = new ButtonGroup();
		for (int i = 0; i < Lengths.Length; i++)
		{
			var b = PillButton(lengthGroup, Lengths[i].name, null, Accent);
			int index = i;
			b.Pressed += () => { LengthIndex = index; ShowingLast = false; Refresh(); };
			lengthRow.AddChild(b);
			LengthButtons.Add(b);
		}

		setup.AddChild(Spacer(8));
		var stats = new HBoxContainer();
		stats.AddThemeConstantOverride("separation", 10);
		setup.AddChild(stats);
		SongsValue = StatTile(stats, "Songs");
		DurationValue = StatTile(stats, "Duration");
		LevelsValue = StatTile(stats, "Levels");

		setup.AddChild(Spacer(5));
		var info = new PanelContainer();
		info.AddThemeStyleboxOverride("panel", Box(new Color(Accent, 0.07f), 18, 18, 13, new Color(Accent, 0.18f), 1));
		var infoText = Text("Every song is saved like a normal play: scores, pp and replays all count. "
			+ "The mods you picked in song select apply to the whole run.", 16, new Color(1, 1, 1, 0.75f));
		infoText.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		info.AddChild(infoText);
		setup.AddChild(info);
		setup.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill });

		var actions = new HBoxContainer();
		actions.AddThemeConstantOverride("separation", 13);
		setup.AddChild(actions);
		Start = new Button { Text = "Start Marathon", CustomMinimumSize = new Vector2(0, 75), SizeFlagsHorizontal = SizeFlags.ExpandFill };
		Start.AddThemeFontOverride("font", Bold);
		Start.AddThemeFontSizeOverride("font_size", 28);
		Start.AddThemeColorOverride("font_color", Colors.White);
		Start.AddThemeColorOverride("font_hover_color", Colors.White);
		Start.AddThemeColorOverride("font_pressed_color", Colors.White);
		StyleButton(Start, Purple, PurpleHover, new Color("#7e2f91"), radius: 20);
		Start.Pressed += _start;
		actions.AddChild(Start);

		Shuffle = new Button { Text = "New songs", CustomMinimumSize = new Vector2(162.5f, 75), TooltipText = "Pick a different set of songs from this range" };
		Shuffle.AddThemeFontSizeOverride("font_size", 20);
		StyleButton(Shuffle, Inset, new Color(1, 1, 1, 0.12f), new Color(1, 1, 1, 0.2f), radius: 20);
		Shuffle.Pressed += _reroll;
		actions.AddChild(Shuffle);

		// ---- right: setlist / last run
		var right = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		right.AddThemeStyleboxOverride("panel", Box(Card, 25, 23, 20));
		columns.AddChild(right);
		var rightCol = new VBoxContainer();
		rightCol.AddThemeConstantOverride("separation", 15);
		right.AddChild(rightCol);

		var header = new HBoxContainer();
		header.AddThemeConstantOverride("separation", 10);
		rightCol.AddChild(header);
		var tabGroup = new ButtonGroup();
		SetlistTab = TabButton(tabGroup, "Setlist");
		SetlistTab.Pressed += () => { ShowingLast = false; Refresh(); };
		header.AddChild(SetlistTab);
		LastTab = TabButton(tabGroup, "Last run");
		LastTab.Pressed += () => { ShowingLast = true; Refresh(); };
		header.AddChild(LastTab);
		header.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
		ListCount = Text("", 16, Dim);
		ListCount.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		header.AddChild(ListCount);

		ListScroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
		rightCol.AddChild(ListScroll);
		List = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		List.AddThemeConstantOverride("separation", 8);
		ListScroll.AddChild(List);
	}

	private static Control Spacer(int h) => new() { CustomMinimumSize = new Vector2(0, h) };

	/// <summary>A toggle pill: bold name and an optional small line under it, outlined in <paramref name="color"/> when picked.</summary>
	private static Button PillButton(ButtonGroup group, string name, string sub, Color color)
	{
		var b = new Button
		{
			ToggleMode = true, ButtonGroup = group, CustomMinimumSize = new Vector2(0, sub == null ? 52.5f : 70),
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		StyleButton(b, Inset, new Color(1, 1, 1, 0.1f), new Color(color, 0.22f), null, 15);
		b.AddThemeStyleboxOverride("pressed", Box(new Color(color, 0.22f), 15, 13, 10, color, 3));
		b.AddThemeStyleboxOverride("hover_pressed", Box(new Color(color, 0.3f), 15, 13, 10, color, 3));
		var col = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
		col.SetAnchorsPreset(LayoutPreset.FullRect);
		col.AddThemeConstantOverride("separation", 0);
		var title = Text(name, 18, Colors.White, bold: true);
		title.HorizontalAlignment = HorizontalAlignment.Center;
		col.AddChild(title);
		if (sub != null)
		{
			var small = Text(sub, 14, new Color(color, 0.95f));
			small.HorizontalAlignment = HorizontalAlignment.Center;
			col.AddChild(small);
		}
		b.AddChild(col);
		return b;
	}

	private static Button TabButton(ButtonGroup group, string text)
	{
		var b = new Button { Text = text, ToggleMode = true, ButtonGroup = group, CustomMinimumSize = new Vector2(137.5f, 47.5f) };
		b.AddThemeFontOverride("font", Bold);
		b.AddThemeFontSizeOverride("font_size", 19);
		b.AddThemeColorOverride("font_pressed_color", new Color("#2a2430"));
		b.AddThemeColorOverride("font_hover_pressed_color", new Color("#2a2430"));
		StyleButton(b, Inset, new Color(1, 1, 1, 0.1f), Accent, null, 24);
		return b;
	}

	private static Label StatTile(Container parent, string caption)
	{
		var tile = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		tile.AddThemeStyleboxOverride("panel", Box(Inset, 18, 18, 13));
		var col = new VBoxContainer();
		col.AddThemeConstantOverride("separation", 0);
		tile.AddChild(col);
		var value = Text("-", 30, Colors.White, bold: true);
		col.AddChild(value);
		col.AddChild(Text(caption, 15, Dim));
		parent.AddChild(tile);
		return value;
	}

	// ------------------------------------------------------------------ content

	private static Color LevelColor(float level)
	{
		foreach (var r in Ranges)
			if ((int)level <= r.max) return r.color;
		return Ranges[^1].color;
	}

	private static string Clock(double ms)
	{
		var t = TimeSpan.FromMilliseconds(Math.Max(0, ms));
		return t.TotalHours >= 1 ? t.ToString(@"h\:mm\:ss") : t.ToString(@"m\:ss");
	}

	/// <summary>Rate mods change how long songs take. (The fail animation can leave the pitch near 0: ignore odd values.)</summary>
	private static float Rate()
	{
		float rate = AudioPlayer.Instance?.PitchScale ?? 1f;
		return rate < 0.5f || rate > 2f ? 1f : rate;
	}

	private void Pick()
	{
		var (min, max, _, _) = Ranges[DiffCount];
		var rng = new Random(Seed);
		Picked.Clear();
		Picked.AddRange(Enumerable.Range(0, SettingsOperator.Beatmaps.Count)
			.Where(i => SettingsOperator.Beatmaps[i] is { } b && (int)b.Levelrating >= min && (int)b.Levelrating <= max && b.Timetotal > 0)
			.OrderBy(_ => rng.Next())
			.GroupBy(i => (SettingsOperator.Beatmaps[i].Artist ?? "").Trim().ToLowerInvariant()) // one song per artist
			.Select(g => g.First())
			.Take(Lengths[LengthIndex].count)
			.OrderBy(i => SettingsOperator.Beatmaps[i].Levelrating) // warm up first
			.ThenBy(i => SettingsOperator.Beatmaps[i].Timetotal));
	}

	private void Refresh()
	{
		DiffCount = Math.Clamp(DiffCount, 0, Ranges.Length - 1);
		LengthIndex = Math.Clamp(LengthIndex, 0, Lengths.Length - 1);
		// SetPressedNoSignal doesn't release the group's other button, so set every one.
		for (int i = 0; i < DiffButtons.Count; i++) DiffButtons[i].SetPressedNoSignal(i == DiffCount);
		for (int i = 0; i < LengthButtons.Count; i++) LengthButtons[i].SetPressedNoSignal(i == LengthIndex);
		Pick();

		double ms = Picked.Sum(i => (double)SettingsOperator.Beatmaps[i].Timetotal) / Rate();
		SongsValue.Text = Picked.Count.ToString("N0");
		DurationValue.Text = Picked.Count == 0 ? "-" : Clock(ms);
		LevelsValue.Text = Picked.Count == 0 ? "-"
			: $"{SettingsOperator.Beatmaps[Picked[0]].Levelrating:0}-{SettingsOperator.Beatmaps[Picked[^1]].Levelrating:0}";
		Start.Disabled = Picked.Count == 0;
		Shuffle.Disabled = Picked.Count == 0;

		bool hasLast = MarathonRun.Last is { } last && last.Songs.Count > 0;
		LastTab.Visible = hasLast;
		if (!hasLast) ShowingLast = false;
		SetlistTab.SetPressedNoSignal(!ShowingLast);
		LastTab.SetPressedNoSignal(ShowingLast);

		foreach (Node child in List.GetChildren()) child.QueueFree();
		if (ShowingLast) FillLastRun(MarathonRun.Last);
		else FillSetlist();
		ListScroll.ScrollVertical = 0;
	}

	private void FillSetlist()
	{
		ListCount.Text = Picked.Count == 0 ? "" : $"{Picked.Count} song{(Picked.Count == 1 ? "" : "s")} · easiest first";
		if (Picked.Count == 0)
		{
			var empty = Text("No beatmaps in this level range yet.\nPick another difficulty, or get more maps from Browse.", 20, Dim);
			empty.HorizontalAlignment = HorizontalAlignment.Center;
			empty.VerticalAlignment = VerticalAlignment.Center;
			empty.CustomMinimumSize = new Vector2(0, 275);
			List.AddChild(empty);
			return;
		}
		float rate = Rate();
		for (int n = 0; n < Picked.Count; n++)
		{
			var b = SettingsOperator.Beatmaps[Picked[n]];
			var row = SongRow(n + 1, b.Title ?? "", $"{b.Artist} · {b.Version}", Clock(b.Timetotal / rate), $"Lv {b.Levelrating:0}", LevelColor(b.Levelrating));
			row.TooltipText = $"{b.Artist} - {b.Title} [{b.Version}]\nmapped by {b.Mapper}";
			List.AddChild(row);
			FadeIn(row, n);
		}
	}

	private void FillLastRun(MarathonRun.Summary last)
	{
		ListCount.Text = last.Completed ? "Finished!" : "Stopped early";
		var tiles = new HBoxContainer();
		tiles.AddThemeConstantOverride("separation", 10);
		List.AddChild(tiles);
		StatTile(tiles, "Accuracy").Text = $"{last.Accuracy * 100:0.00}%";
		StatTile(tiles, "Score").Text = last.TotalScore.ToString("N0");
		StatTile(tiles, "Songs").Text = $"{last.Songs.Count}/{last.Planned}";
		StatTile(tiles, "Time").Text = last.Time.TotalHours >= 1 ? last.Time.ToString(@"h\:mm\:ss") : last.Time.ToString(@"m\:ss");
		List.AddChild(Text($"{last.TotalPp:N0}pp earned in total · {last.Misses:N0} misses", 16, Dim));
		for (int n = 0; n < last.Songs.Count; n++)
		{
			var s = last.Songs[n];
			var color = s.Accuracy >= 0.95f ? Ranges[0].color : s.Accuracy >= 0.85f ? Ranges[2].color : Ranges[4].color;
			var row = SongRow(n + 1, s.Title, $"{s.Score:N0} score · {s.MaxCombo}x combo · {s.Bad} misses", $"{s.Pp:0}pp", $"{s.Accuracy * 100:0.00}%", color);
			List.AddChild(row);
			FadeIn(row, n + 2);
		}
	}

	/// <summary>Number bubble, title + subtitle, a right-hand note and a colored badge.</summary>
	private static PanelContainer SongRow(int number, string title, string sub, string note, string badge, Color color)
	{
		var row = new PanelContainer { MouseFilter = MouseFilterEnum.Pass };
		row.AddThemeStyleboxOverride("panel", Box(Inset, 15, 15, 10));
		var h = new HBoxContainer();
		h.AddThemeConstantOverride("separation", 15);
		row.AddChild(h);

		var bubble = new PanelContainer { CustomMinimumSize = new Vector2(42.5f, 42.5f), SizeFlagsVertical = SizeFlags.ShrinkCenter };
		bubble.AddThemeStyleboxOverride("panel", Box(new Color(Accent, 0.14f), 21, 0, 0));
		var num = Text(number.ToString(), 18, Accent, bold: true);
		num.HorizontalAlignment = HorizontalAlignment.Center;
		num.VerticalAlignment = VerticalAlignment.Center;
		bubble.AddChild(num);
		h.AddChild(bubble);

		var text = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkCenter };
		text.AddThemeConstantOverride("separation", 0);
		var t = Text(title, 19, Colors.White, bold: true);
		t.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
		t.ClipText = true;
		var s = Text(sub, 15, Dim);
		s.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
		s.ClipText = true;
		text.AddChild(t);
		text.AddChild(s);
		h.AddChild(text);

		var right = Text(note, 16, Dim);
		right.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		h.AddChild(right);

		var pill = new PanelContainer { SizeFlagsVertical = SizeFlags.ShrinkCenter, CustomMinimumSize = new Vector2(80, 0) };
		pill.AddThemeStyleboxOverride("panel", Box(new Color(color, 0.18f), 13, 10, 4, new Color(color, 0.8f), 1));
		var p = Text(badge, 16, color, bold: true);
		p.HorizontalAlignment = HorizontalAlignment.Center;
		pill.AddChild(p);
		h.AddChild(pill);
		return row;
	}

	private static void FadeIn(Control row, int index)
	{
		if (index > 24) return; // only the first screenful animates
		row.Modulate = new Color(1, 1, 1, 0);
		var tween = row.CreateTween();
		tween.TweenInterval(0.025 * index);
		tween.TweenProperty(row, "modulate:a", 1f, 0.25f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
	}

	// ------------------------------------------------------------------ actions

	/// <summary>Old scene's level dropdown (hidden now, still connected).</summary>
	private void _changedmode(int value)
	{
		DiffCount = value;
		Refresh();
	}

	private void _reroll()
	{
		Seed = System.Environment.TickCount;
		ShowingLast = false;
		Sample.PlaySample("res://SelectableSkins/Slia/Sounds/score-tick.wav");
		Refresh();
	}

	private void _exit()
	{
		SettingsOperator.Marathon = false;
		GetNode<SceneTransition>("/root/Transition").Switch("res://Panels/Screens/home_screen.tscn");
	}

	private void _start()
	{
		if (Picked.Count == 0) return;
		Sample.PlaySample("res://SelectableSkins/Slia/Sounds/play.wav");
		Start.Disabled = true; // no double start
		MarathonRun.Begin(this, Picked);
	}
}
