using Godot;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Player info in the game, laid out like the website's user page: name card, status strip, stats,
/// medals, best scores and last played, all on one scrolling page.
/// </summary>
public partial class PlayerProfilePanel : Control
{
	private readonly string _username;
	private PanelContainer _panel;
	private VBoxContainer _page;
	private Node _scene;

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
		_panel = new PanelContainer { CustomMinimumSize = new Vector2(Mathf.Min(1100, view.X - 40), Mathf.Min(760, view.Y - 80)) };
		_panel.AddThemeStyleboxOverride("panel", PlayerOverlays.Style(PlayerOverlays.Page, 0, 0, 0));
		center.AddChild(_panel);

		var outer = new VBoxContainer();
		outer.AddThemeConstantOverride("separation", 0);
		_panel.AddChild(outer);

		var bar = PlayerOverlays.TitleBar(outer, "user page");
		var self = _username == ApiOperator.Username;
		if (ModerationApi.IsStaff)
		{
			var moderate = PlayerOverlays.FlatButton("moderate", "Open this player in the admin panel");
			moderate.Pressed += () => PlayerOverlays.OpenAdmin(_username);
			bar.AddChild(moderate);
		}
		if (SettingsOperator.SessionConfig.Loggedin && !self)
		{
			var report = PlayerOverlays.FlatButton("report", "Report this player to the Qlute team");
			report.Pressed += () => PlayerOverlays.OpenReport(_username);
			bar.AddChild(report);
		}
		var web = PlayerOverlays.FlatButton("view on website");
		web.Pressed += () => OS.ShellOpen(PlayerApi.WebProfileUrl(_username));
		bar.AddChild(web);
		var close = PlayerOverlays.FlatButton("✕", "Close (Esc)");
		close.Pressed += Close;
		bar.AddChild(close);

		var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
		outer.AddChild(scroll);
		_page = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		_page.AddThemeConstantOverride("separation", 0);
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
	}

	public override void _Input(InputEvent e)
	{
		if (e.IsActionPressed("ui_cancel")) { Close(); GetViewport().SetInputAsHandled(); }
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
			NameCard(null);
			Section(_page).AddChild(PlayerOverlays.Text(error ?? "Couldn't load this profile.", 14, PlayerOverlays.Dim, wrap: true));
			return;
		}
		NameCard(p);
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
		Stats(p);
		Medals(p.Medals);
		Scores("Best Scores", p.Best, weighted: true, empty: "No Ranked Plays -n-");
		Scores("Last Played", p.Recent, weighted: false, empty: "No Recent Plays -n-");
	}

	// =====================================================================
	// Sections (each one is a website ".infobox")
	// =====================================================================

	private static PanelContainer Section(Control parent, int marginY = 12, Color? bg = null)
	{
		var box = PlayerOverlays.Panel(bg ?? PlayerOverlays.Box, 0, 15, marginY);
		parent.AddChild(box);
		return box;
	}

	private static Label SectionTitle(string text) => PlayerOverlays.Text(text, 14, bold: true);

	private void NameCard(PlayerApi.Profile p)
	{
		var box = Section(_page, 10);
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 15);
		box.AddChild(row);
		if (p != null)
			row.AddChild(PlayerOverlays.Picture(p.Pfp, 64));
		if (p != null && !p.Restricted && p.TeamRole is "mod" or "admin")
		{
			// Same badge as the website's name card.
			var badge = PlayerOverlays.PillLabel(p.TeamRole == "admin" ? "Team Admin" : "Team Moderator",
				new Color(p.TeamRole == "admin" ? "#6620d6" : "#983aaf"), 14);
			badge.SizeFlagsVertical = SizeFlags.ShrinkCenter;
			row.AddChild(badge);
		}
		var name = PlayerOverlays.Text(p?.Username ?? _username, 28, bold: true, trim: true);
		name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		name.HorizontalAlignment = HorizontalAlignment.Right;
		name.VerticalAlignment = VerticalAlignment.Center;
		row.AddChild(name);

		if (p == null || p.Restricted) return;
		var strip = Section(_page, 10, PlayerOverlays.StatusStrip);
		var s = new HBoxContainer();
		s.AddThemeConstantOverride("separation", 8);
		strip.AddChild(s);
		if (p.Online) s.AddChild(new ColorRect { Color = PlayerOverlays.OnlineDot, CustomMinimumSize = new Vector2(8, 8), SizeFlagsVertical = SizeFlags.ShrinkCenter });
		var text = PlayerOverlays.Text(PlayerOverlays.StatusLine(p), 13, trim: true);
		text.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		s.AddChild(text);
		if (p.Joined > 0) s.AddChild(PlayerOverlays.Text($"Joined {System.DateTimeOffset.FromUnixTimeSeconds(p.Joined).ToLocalTime():d MMMM yyyy}", 12, PlayerOverlays.Dim));
	}

	private void Stats(PlayerApi.Profile p)
	{
		var box = Section(_page, 12);
		var v = new VBoxContainer();
		v.AddThemeConstantOverride("separation", 10);
		box.AddChild(v);
		v.AddChild(SectionTitle("Stats"));

		var block = new HBoxContainer();
		block.AddThemeConstantOverride("separation", 30);
		v.AddChild(block);
		block.AddChild(BigStat("Global Rank", p.Rank > 0 ? $"#{p.Rank:N0}" : "#?"));
		if (p.Level > 0) block.AddChild(BigStat("Level", p.Level.ToString("N0")));

		// Right column, right-aligned like ".statinfo"
		var lines = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		lines.AddThemeConstantOverride("separation", 9);
		block.AddChild(lines);
		void Line(string text)
		{
			var l = PlayerOverlays.Text(text, 13);
			l.HorizontalAlignment = HorizontalAlignment.Right;
			lines.AddChild(l);
		}
		Line($"Ranked Points: {p.Points:N0}pp");
		Line($"Total Play Time: {p.PlaytimeText}");
		if (p.Score > 0) Line($"Ranked Score: {p.Score:N0}");
		Line($"Accuracy: {p.Accuracy:0.##}%");
		Line($"Total Perfect: {p.JMax}");
		Line($"Total Great: {p.JGreat}");
		Line($"Total Meh: {p.JMeh}");
		Line($"Total Bad: {p.JBad}");
		Line($"Max Combo: {p.MaxCombo}x");
	}

	private static Control BigStat(string name, string value)
	{
		var v = new VBoxContainer { CustomMinimumSize = new Vector2(170, 0), SizeFlagsVertical = SizeFlags.ShrinkBegin };
		v.AddThemeConstantOverride("separation", 4);
		v.AddChild(PlayerOverlays.Text(name, 12));
		v.AddChild(PlayerOverlays.Text(value, 26, bold: true));
		return v;
	}

	private void Medals(List<PlayerApi.Medal> medals)
	{
		if (medals.Count == 0) return;
		var box = Section(_page, 12);
		var flow = new HFlowContainer { Alignment = FlowContainer.AlignmentMode.Center };
		flow.AddThemeConstantOverride("h_separation", 10);
		flow.AddThemeConstantOverride("v_separation", 10);
		box.AddChild(flow);
		foreach (var m in medals) // website order; locked ones faded to 20%
		{
			var tile = PlayerOverlays.Panel(PlayerOverlays.MedalBox, 0, 10, 8);
			tile.Modulate = m.Achieved ? Colors.White : new Color(1, 1, 1, 0.2f);
			tile.TooltipText = m.Achieved ? "Unlocked" : "Locked";
			var v = new VBoxContainer();
			v.AddThemeConstantOverride("separation", 2);
			v.AddChild(PlayerOverlays.Text(m.Title, 13, bold: true));
			v.AddChild(PlayerOverlays.Text(m.Desc, 11));
			tile.AddChild(v);
			flow.AddChild(tile);
		}
	}

	private void Scores(string title, List<PlayerApi.Play> plays, bool weighted, string empty)
	{
		var box = Section(_page, 12);
		var v = new VBoxContainer();
		v.AddThemeConstantOverride("separation", 10);
		box.AddChild(v);
		v.AddChild(SectionTitle(title));
		if (plays.Count == 0) { v.AddChild(PlayerOverlays.Text(empty, 16, bold: true)); return; }
		double share = 1;
		foreach (var s in plays)
		{
			v.AddChild(ScoreCard(s, weighted ? share : -1));
			share -= 0.02;
		}
	}

	/// <summary>Website ".score-card": grade badge, "Title by Artist", "Difficulty - When (weighted N%)", mods and pp pills.</summary>
	private static Control ScoreCard(PlayerApi.Play s, double share)
	{
		var card = PlayerOverlays.Panel(PlayerOverlays.Card, 8, 10, 8);
		var h = new HBoxContainer();
		h.AddThemeConstantOverride("separation", 10);
		card.AddChild(h);

		var grade = PlayerApi.Grade(s);
		var badge = PlayerOverlays.PillLabel(grade, PlayerOverlays.GradeColor(grade), 15, 50);
		badge.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		h.AddChild(badge);

		var info = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, Alignment = BoxContainer.AlignmentMode.Center };
		info.AddThemeConstantOverride("separation", 2);
		var line1 = new HBoxContainer();
		line1.AddThemeConstantOverride("separation", 0);
		var t = PlayerOverlays.Text(s.Beatmap, 12, bold: true);
		line1.AddChild(t);
		var by = PlayerOverlays.Text(string.IsNullOrEmpty(s.Artist) ? "" : $" by {s.Artist}", 12, trim: true);
		by.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		line1.AddChild(by);
		info.AddChild(line1);
		var line2 = $"{s.Difficulty} - {PlayerApi.Ago(s.Created)}";
		if (System.Math.Abs(s.Speed - 1) > 0.001) line2 += $" ({s.Speed:0.##}x)";
		if (share >= 0) line2 += $" (weighted {(int)System.Math.Round(share * 100)}%)";
		info.AddChild(PlayerOverlays.Text(line2, 11, PlayerOverlays.Dim, trim: true));
		h.AddChild(info);

		var right = new HBoxContainer { SizeFlagsVertical = SizeFlags.ShrinkCenter };
		right.AddThemeConstantOverride("separation", 5);
		foreach (var mod in Mods(s.Mods)) right.AddChild(PlayerOverlays.PillLabel(mod, PlayerOverlays.ModPill, 12));
		if (share >= 0) right.AddChild(PlayerOverlays.PillLabel($"{(int)(s.Points * share)}pp*", PlayerOverlays.Pill, 12));
		right.AddChild(PlayerOverlays.PillLabel($"{(int)s.Points}pp", PlayerOverlays.Pill, 12));
		h.AddChild(right);
		return card;
	}

	private static readonly string[] ModAliases = { "AT", "DT", "HT", "SL", "BT", "RND", "NF" }; // views.py modsaliasab
	private static IEnumerable<string> Mods(string mods) => ModAliases.Where(a => !string.IsNullOrEmpty(mods) && mods.Contains(a));
}
