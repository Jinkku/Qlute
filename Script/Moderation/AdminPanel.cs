using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// The in-game admin panel: same tabs as the website's /admin (reports, players, bans, team).
/// Only reachable by team members (the top panel button and profile "moderate" button check ModerationApi.IsStaff);
/// the server checks again on every call.
/// </summary>
public partial class AdminPanel : Control
{
	private enum Tab { Reports, Players, User, Bans, Team, Notices }

	private Tab _tab = Tab.Reports;
	private string _arg = "";      // reports: open|closed, players: query, user: username, bans: active|all
	private long _reportId;        // the report a ban came from (user view)
	private readonly Dictionary<Tab, Button> _tabButtons = new();
	private VBoxContainer _page;
	private ScrollContainer _scroll;
	private Label _hint;
	private Node _scene;
	private int _render;
	private bool _closing;

	public AdminPanel() : this(null) { }
	public AdminPanel(string username)
	{
		if (!string.IsNullOrEmpty(username)) { _tab = Tab.User; _arg = username; }
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
		var panel = new PanelContainer { CustomMinimumSize = new Vector2(Mathf.Min(1000, view.X - 40), Mathf.Min(760, view.Y - 80)) };
		panel.AddThemeStyleboxOverride("panel", PlayerOverlays.Style(PlayerOverlays.Page, 0, 0, 0));
		center.AddChild(panel);
		var outer = new VBoxContainer();
		outer.AddThemeConstantOverride("separation", 0);
		panel.AddChild(outer);

		var bar = PlayerOverlays.TitleBar(outer, "admin panel");
		var web = PlayerOverlays.FlatButton("open on website");
		web.Pressed += () => OS.ShellOpen(PlayerApi.Base + "admin");
		bar.AddChild(web);
		var refresh = PlayerOverlays.FlatButton("⟳", "Refresh");
		refresh.Pressed += Render;
		bar.AddChild(refresh);
		var close = PlayerOverlays.FlatButton("✕", "Close (Esc)");
		close.Pressed += Close;
		bar.AddChild(close);

		var tabsBox = PlayerOverlays.Panel(PlayerOverlays.Box, 0, 15, 10);
		outer.AddChild(tabsBox);
		var tabs = ModUi.Row(8);
		tabsBox.AddChild(tabs);
		foreach (var (tab, text) in new[] { (Tab.Reports, "Reports"), (Tab.Players, "Players"), (Tab.Bans, "Bans"), (Tab.Team, "Team"),
			(Tab.Notices, "Notices") })
		{
			var b = ModUi.Button(text);
			b.Pressed += () => Go(tab, tab == Tab.Reports ? "open" : tab == Tab.Bans ? "active" : "");
			tabs.AddChild(b);
			_tabButtons[tab] = b;
		}

		_scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
		var pageBox = PlayerOverlays.Panel(PlayerOverlays.Box, 0, 15, 12);
		pageBox.SizeFlagsVertical = SizeFlags.ExpandFill;
		outer.AddChild(pageBox);
		pageBox.AddChild(_scroll);
		var pageCol = ModUi.Column(10);
		_scroll.AddChild(pageCol);
		_hint = ModUi.Para("", 14, PlayerOverlays.Dim);
		pageCol.AddChild(_hint);
		_page = ModUi.Column(10);
		pageCol.AddChild(_page);

		_scene = GetTree().CurrentScene;
		_scene?.SetProcessInput(false);
		Modulate = new Color(1, 1, 1, 0);
		CreateTween().SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out).TweenProperty(this, "modulate:a", 1f, 0.2f);
		if (_tab == Tab.Reports) _arg = "open";
		Render();
	}

	public override void _ExitTree()
	{
		if (IsInstanceValid(_scene)) _scene.SetProcessInput(true);
	}

	public override void _Input(InputEvent e)
	{
		if (e.IsActionPressed("ui_cancel")) { Close(); GetViewport().SetInputAsHandled(); }
	}

	public void Close()
	{
		if (_closing) return;
		_closing = true;
		var t = CreateTween().SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
		t.TweenProperty(this, "modulate:a", 0f, 0.15f);
		t.TweenCallback(Callable.From(QueueFree));
	}

	/// <summary>Switch view (and remember where we are).</summary>
	private void Go(Tab tab, string arg, long reportId = 0)
	{
		_tab = tab;
		_arg = arg ?? "";
		_reportId = reportId;
		_scroll.ScrollVertical = 0;
		Render();
	}

	public void OpenUser(string username, long reportId = 0) => Go(Tab.User, username, reportId);

	/// <summary>Opens a tab by name ("reports", "players", "bans", "team", "notices").</summary>
	public void OpenTab(string name)
	{
		if (Enum.TryParse<Tab>(name, true, out var tab) && tab != Tab.User)
			Go(tab, tab == Tab.Reports ? "open" : tab == Tab.Bans ? "active" : "");
	}

	private void Render()
	{
		var mine = ++_render;
		foreach (var (tab, b) in _tabButtons)
		{
			var active = tab == _tab || (tab == Tab.Players && _tab == Tab.User);
			b.AddThemeStyleboxOverride("normal", PlayerOverlays.Style(active ? PlayerOverlays.StatusStrip : PlayerOverlays.Pill, 5, 10, 4));
			if (tab == Tab.Reports) b.Text = ModerationApi.OpenReports > 0 ? $"Reports ({ModerationApi.OpenReports})" : "Reports";
			if (tab == Tab.Notices) b.Visible = ModerationApi.IsAdmin; // site content: admins only
		}
		foreach (var c in _page.GetChildren()) c.QueueFree();
		Hint("Loading...");
		switch (_tab)
		{
			case Tab.Reports: ShowReports(mine); break;
			case Tab.Players: ShowPlayers(mine); break;
			case Tab.User: ShowUser(mine); break;
			case Tab.Bans: ShowBans(mine); break;
			case Tab.Team: ShowTeam(mine); break;
			case Tab.Notices: ShowNotices(mine); break;
		}
	}

	/// <summary>Still the view that asked for this data?</summary>
	private bool Current(int mine) => IsInstanceValid(this) && mine == _render;

	private void Hint(string text)
	{
		_hint.Text = text ?? "";
		_hint.Visible = !string.IsNullOrEmpty(text);
	}

	private Control SubTabs(params (string text, bool active, Action go)[] items)
	{
		var row = ModUi.Row(6);
		foreach (var (text, active, go) in items)
		{
			var b = ModUi.Button(text);
			if (active) b.AddThemeStyleboxOverride("normal", PlayerOverlays.Style(PlayerOverlays.StatusStrip, 5, 10, 4));
			b.Pressed += go;
			row.AddChild(b);
		}
		return row;
	}

	private static Control SectionTitle(string text) => PlayerOverlays.Text(text, 16, bold: true);

	private BaseButton NameButton(string username)
	{
		var b = new LinkButton { Text = username, Underline = LinkButton.UnderlineMode.OnHover, MouseDefaultCursorShape = CursorShape.PointingHand };
		b.AddThemeFontSizeOverride("font_size", 15);
		b.AddThemeFontOverride("font", PlayerOverlays.Bold);
		b.AddThemeColorOverride("font_color", Colors.White);
		b.Pressed += () => OpenUser(username);
		return b;
	}

	// =====================================================================
	// Reports
	// =====================================================================

	private async void ShowReports(int mine)
	{
		var open = _arg != "closed";
		_page.AddChild(SubTabs(("Open", open, () => Go(Tab.Reports, "open")), ("Closed", !open, () => Go(Tab.Reports, "closed"))));
		var (list, _, error) = await ModerationApi.Reports(open ? "open" : "closed");
		if (!Current(mine)) return;
		_tabButtons[Tab.Reports].Text = ModerationApi.OpenReports > 0 ? $"Reports ({ModerationApi.OpenReports})" : "Reports";
		if (error != null) { Hint(error); return; }
		Hint(list.Count == 0 ? (open ? "No open reports. Nice and quiet ^-^" : "No closed reports yet.") : "");
		foreach (var r in list) _page.AddChild(ReportCard(r));
	}

	private Control ReportCard(ModerationApi.Report r)
	{
		var holder = ModUi.Column(0);
		var v = ModUi.Card(holder, alpha: r.Status == "open" ? 1 : 0.75f);

		var head = ModUi.Flow(8);
		head.AddChild(NameButton(r.Target));
		if (r.TargetRestricted) head.AddChild(ModUi.Pill("restricted", ModUi.Red));
		if (r.TargetOpenReports > 1) head.AddChild(ModUi.Pill($"{r.TargetOpenReports} open reports", ModUi.Purple));
		head.AddChild(r.Status == "open" ? ModUi.Pill("open", ModUi.Yellow, PlayerOverlays.Page)
			: ModUi.Pill(r.Status, r.Status == "resolved" ? ModUi.Green : PlayerOverlays.ModPill));
		head.AddChild(ModUi.Pill(r.CategoryText, PlayerOverlays.ModPill));
		v.AddChild(head);

		v.AddChild(ModUi.Para($"#{r.Id} reported by {r.Reporter} {ModUi.When(r.Created)} from the {(r.Source == "game" ? "game" : "website")}", 12, PlayerOverlays.Dim));
		v.AddChild(ModUi.Quote(r.Details));
		if (!string.IsNullOrEmpty(r.Context)) v.AddChild(ModUi.Para("Context: " + r.Context, 12, PlayerOverlays.Dim));
		if (r.Status != "open")
			v.AddChild(ModUi.Para($"{r.Status} by {r.HandledBy} {ModUi.When(r.HandledAt)}{(string.IsNullOrEmpty(r.Note) ? "" : ": " + r.Note)}", 12, PlayerOverlays.Dim));

		var msg = ModUi.Para("", 12, ModUi.Accent);
		var actions = ModUi.Flow(6);
		var openPlayer = ModUi.Button("Open player");
		openPlayer.Pressed += () => OpenUser(r.Target);
		actions.AddChild(openPlayer);
		if (r.Status == "open")
		{
			var ban = ModUi.Button("Ban...", ModUi.Red);
			ban.Pressed += () => OpenUser(r.Target, r.Id);
			actions.AddChild(ban);
			var note = ModUi.Input("Note (optional)", 500);
			note.CustomMinimumSize = new Vector2(200, 0);
			actions.AddChild(note);
			var resolve = ModUi.Button("Resolve", ModUi.Green);
			resolve.Pressed += () => Act(msg, () => ModerationApi.CloseReport(r.Id, "resolved", note.Text.Trim()), refreshMe: true);
			actions.AddChild(resolve);
			var dismiss = ModUi.Button("Dismiss");
			dismiss.Pressed += () => Act(msg, () => ModerationApi.CloseReport(r.Id, "dismissed", note.Text.Trim()), refreshMe: true);
			actions.AddChild(dismiss);
		}
		else
		{
			var reopen = ModUi.Button("Reopen");
			reopen.Pressed += () => Act(msg, () => ModerationApi.ReopenReport(r.Id), refreshMe: true);
			actions.AddChild(reopen);
		}
		v.AddChild(actions);
		v.AddChild(msg);
		return holder;
	}

	/// <summary>Runs a team action; shows its error in msg, or reloads the view.</summary>
	private async void Act(Label msg, Func<System.Threading.Tasks.Task<string>> call, bool refreshMe = false)
	{
		msg.Text = "Working...";
		var error = await call();
		if (!IsInstanceValid(this)) return;
		if (error != null) { if (IsInstanceValid(msg)) msg.Text = error; return; }
		if (refreshMe) ModerationApi.RefreshMe();
		Render();
	}

	// =====================================================================
	// Players
	// =====================================================================

	private LineEdit _search;
	private CheckBox _restrictedOnly;
	private VBoxContainer _results;
	private int _searchReq;

	private void ShowPlayers(int mine)
	{
		var bar = ModUi.Row(10);
		_search = ModUi.Input("Find a player by name... (Enter opens them)", 64);
		_search.Text = _arg;
		bar.AddChild(_search);
		_restrictedOnly = new CheckBox { Text = "Restricted only", FocusMode = FocusModeEnum.None };
		_restrictedOnly.AddThemeFontSizeOverride("font_size", 13);
		bar.AddChild(_restrictedOnly);
		_page.AddChild(bar);
		_results = ModUi.Column(8);
		_page.AddChild(_results);

		var typing = new Timer { OneShot = true, WaitTime = 0.3 };
		_page.AddChild(typing);
		typing.Timeout += () => Search(mine);
		_search.TextChanged += t => { _arg = t; typing.Start(); };
		_search.TextSubmitted += t => { if (!string.IsNullOrWhiteSpace(t)) OpenUser(t.Trim()); };
		_restrictedOnly.Toggled += _ => Search(mine);
		_search.CallDeferred(Control.MethodName.GrabFocus);
		Search(mine);
	}

	private async void Search(int mine)
	{
		var req = ++_searchReq;
		var (list, error) = await ModerationApi.Users(_search.Text.Trim(), _restrictedOnly.ButtonPressed);
		if (!Current(mine) || req != _searchReq) return;
		foreach (var c in _results.GetChildren()) c.QueueFree();
		if (error != null) { Hint(error); return; }
		Hint(list.Count == 0 ? "No players match." : "");
		foreach (var u in list)
		{
			var v = ModUi.Card(_results);
			var head = ModUi.Flow(8);
			head.AddChild(NameButton(u.Username));
			if (u.Restricted) head.AddChild(ModUi.Pill("restricted", ModUi.Red));
			if (u.Staff != "") head.AddChild(ModUi.Pill(u.Staff, ModUi.Purple));
			v.AddChild(head);
			v.AddChild(ModUi.Para($"{(u.Rank > 0 ? $"#{u.Rank:N0}" : "Unranked")} · joined {ModUi.When(u.Joined)} · last seen {ModUi.When(u.LastSeen)}", 12, PlayerOverlays.Dim));
		}
	}

	// =====================================================================
	// One player: ban / unban, history, reports about them
	// =====================================================================

	private static readonly (string text, double hours)[] Lengths =
	{
		("1 hour", 1), ("1 day", 24), ("3 days", 72), ("1 week", 168), ("30 days", 720), ("90 days", 2160), ("Custom...", -1), ("Permanent", 0),
	};

	private async void ShowUser(int mine)
	{
		var (u, error) = await ModerationApi.User(_arg);
		if (!Current(mine)) return;
		if (error != null) { Hint(error); return; }
		Hint("");

		var head = ModUi.Flow(10);
		head.AddChild(PlayerOverlays.Text(u.Username, 24, bold: true));
		head.AddChild(u.Restricted ? ModUi.Pill("restricted", ModUi.Red) : ModUi.Pill("in good standing", ModUi.Green));
		if (u.Staff != "") head.AddChild(ModUi.Pill(u.Staff, ModUi.Purple));
		var profile = ModUi.Button("view profile");
		profile.Pressed += () => PlayerOverlays.OpenProfile(u.Username);
		head.AddChild(profile);
		_page.AddChild(head);
		_page.AddChild(ModUi.Para(
			$"Rank: {(u.Rank > 0 ? $"#{u.Rank:N0}" : "unranked")}   {u.Points:N0}pp   {u.Accuracy:0}% acc   {u.Scores:N0} scores   " +
			$"joined {ModUi.When(u.Joined)}   last seen {ModUi.When(u.LastSeen)}   sent {u.ReportsSent} report{(u.ReportsSent == 1 ? "" : "s")}",
			13, new Color(1, 1, 1, 0.8f)));

		if (u.Restricted) UnbanForm(u);
		else if (u.CanBan) BanForm(u);
		else _page.AddChild(ModUi.Para("You can't ban this account (yourself, or a team member you don't outrank).", 13, PlayerOverlays.Dim));

		_page.AddChild(SectionTitle($"Bans ({u.Bans.Count})"));
		if (u.Bans.Count == 0) _page.AddChild(ModUi.Para("No bans.", 13, PlayerOverlays.Dim));
		foreach (var b in u.Bans) _page.AddChild(BanCard(b));

		_page.AddChild(SectionTitle($"Reports about them ({u.ReportsAgainst.Count})"));
		if (u.ReportsAgainst.Count == 0) _page.AddChild(ModUi.Para("No reports.", 13, PlayerOverlays.Dim));
		foreach (var r in u.ReportsAgainst) _page.AddChild(ReportCard(r));
	}

	private void BanForm(ModerationApi.UserRow u)
	{
		var v = ModUi.Card(_page, PlayerOverlays.Pill);
		v.AddChild(PlayerOverlays.Text($"Ban {u.Username}{(_reportId > 0 ? $" (from report #{_reportId})" : "")}", 15, bold: true));
		v.AddChild(PlayerOverlays.Text("Reason (the player sees this)", 13));
		var reason = ModUi.Input("e.g. Using an auto-player on ranked maps", 500);
		v.AddChild(reason);

		v.AddChild(PlayerOverlays.Text("Length", 13));
		var row = ModUi.Row(8);
		var length = ModUi.Options();
		foreach (var (text, _) in Lengths) length.AddItem(text);
		length.Selected = 3; // 1 week
		row.AddChild(length);
		var amount = new SpinBox { MinValue = 1, MaxValue = 3650, Value = 2, Step = 1, Visible = false, CustomMinimumSize = new Vector2(90, 0) };
		row.AddChild(amount);
		var unit = ModUi.Options("hours", "days", "weeks");
		unit.Selected = 1;
		unit.Visible = false;
		row.AddChild(unit);
		length.ItemSelected += i => amount.Visible = unit.Visible = Lengths[i].hours < 0;
		v.AddChild(row);

		var closeReports = new CheckBox { Text = "Mark their open reports as resolved", ButtonPressed = true, FocusMode = FocusModeEnum.None };
		closeReports.AddThemeFontSizeOverride("font_size", 13);
		v.AddChild(closeReports);

		var msg = ModUi.Para("", 12, ModUi.Accent);
		var ban = ModUi.ConfirmButton("Ban", "Click again to ban", ModUi.Red, () =>
		{
			var text = reason.Text.Trim();
			if (text.Length < 3) { msg.Text = "Give a reason; the player will see it."; return; }
			var hours = Lengths[length.Selected].hours;
			if (hours < 0) hours = amount.Value * (unit.Selected switch { 0 => 1, 1 => 24, _ => 168 });
			Act(msg, () => ModerationApi.BanUser(u.Username, text, hours, _reportId, closeReports.ButtonPressed), refreshMe: true);
		});
		var buttons = ModUi.Row();
		buttons.AddChild(ban);
		v.AddChild(buttons);
		v.AddChild(msg);
	}

	private void UnbanForm(ModerationApi.UserRow u)
	{
		var v = ModUi.Card(_page, PlayerOverlays.Pill);
		var active = u.Bans.Find(b => b.Active);
		v.AddChild(PlayerOverlays.Text(active == null ? "Restricted by hand (no ban record)"
			: active.Permanent ? "Banned permanently" : $"Banned, ends {ModUi.When(active.Expires)} ({ModUi.Date(active.Expires)})", 15, bold: true));
		if (active != null) v.AddChild(ModUi.Para($"Reason: {active.Reason} · by {active.IssuedBy} {ModUi.When(active.Created)}", 12, PlayerOverlays.Dim));
		var note = ModUi.Input("Note (optional): why lift it?", 500);
		v.AddChild(note);
		var msg = ModUi.Para("", 12, ModUi.Accent);
		var lift = ModUi.ConfirmButton("Lift ban / unrestrict", "Click again to unrestrict", ModUi.Green,
			() => Act(msg, () => ModerationApi.Unban(u.Username, note.Text.Trim())));
		var buttons = ModUi.Row();
		buttons.AddChild(lift);
		v.AddChild(buttons);
		v.AddChild(msg);
	}

	private Control BanCard(ModerationApi.Ban b)
	{
		var holder = ModUi.Column(0);
		var v = ModUi.Card(holder, alpha: b.Active ? 1 : 0.75f);
		var head = ModUi.Flow(8);
		head.AddChild(NameButton(b.Username));
		head.AddChild(b.Active ? ModUi.Pill(b.Permanent ? "permanent" : $"active, {b.TimeLeft} left", ModUi.Red)
			: !string.IsNullOrEmpty(b.LiftedBy) ? ModUi.Pill("lifted", ModUi.Green) : ModUi.Pill("expired", PlayerOverlays.ModPill));
		v.AddChild(head);
		v.AddChild(ModUi.Quote(b.Reason));
		v.AddChild(ModUi.Para($"By {b.IssuedBy} {ModUi.When(b.Created)} · {(b.Permanent ? "no end date" : b.Active ? $"ends {ModUi.When(b.Expires)} ({ModUi.Date(b.Expires)})" : $"was set to end {ModUi.Date(b.Expires)}")}" +
			(b.ReportId > 0 ? $" · report #{b.ReportId}" : ""), 12, PlayerOverlays.Dim));
		if (!string.IsNullOrEmpty(b.LiftedBy))
			v.AddChild(ModUi.Para($"Lifted by {b.LiftedBy} {ModUi.When(b.LiftedAt)}{(string.IsNullOrEmpty(b.LiftNote) ? "" : ": " + b.LiftNote)}", 12, PlayerOverlays.Dim));
		return holder;
	}

	// =====================================================================
	// Bans
	// =====================================================================

	private async void ShowBans(int mine)
	{
		var active = _arg != "all";
		_page.AddChild(SubTabs(("Active", active, () => Go(Tab.Bans, "active")), ("History", !active, () => Go(Tab.Bans, "all"))));
		var (list, error) = await ModerationApi.Bans(active);
		if (!Current(mine)) return;
		if (error != null) { Hint(error); return; }
		Hint(list.Count == 0 ? (active ? "Nobody is banned right now." : "No bans yet.") : "");
		foreach (var b in list) _page.AddChild(BanCard(b));
	}

	// =====================================================================
	// Team
	// =====================================================================

	private async void ShowTeam(int mine)
	{
		var (list, canEdit, error) = await ModerationApi.Staff();
		if (!Current(mine)) return;
		if (error != null) { Hint(error); return; }
		Hint("");
		_page.AddChild(ModUi.Para("Team members get the \"Making the game better\" medal (moderators also get \"Team Moderator\") and can open this panel (here and on the website). " +
			"Moderators handle reports and bans; admins can also ban moderators and change the team.", 13, PlayerOverlays.Dim));
		foreach (var s in list)
		{
			var v = ModUi.Card(_page);
			var head = ModUi.Flow(8);
			head.AddChild(NameButton(s.Username));
			head.AddChild(ModUi.Pill(s.Role, ModUi.Purple));
			head.AddChild(PlayerOverlays.Text($"added {ModUi.When(s.Added)}{(string.IsNullOrEmpty(s.AddedBy) ? "" : " by " + s.AddedBy)}", 12, PlayerOverlays.Dim));
			v.AddChild(head);
			if (!canEdit || s.Username == ApiOperator.Username) continue;
			var msg = ModUi.Para("", 12, ModUi.Accent);
			var actions = ModUi.Row(6);
			var other = s.Role == "admin" ? "mod" : "admin";
			var role = ModUi.Button($"Make {other}");
			var name = s.Username;
			role.Pressed += () => Act(msg, () => ModerationApi.SetStaff(name, other));
			actions.AddChild(role);
			actions.AddChild(ModUi.ConfirmButton("Remove from team", "Click again to remove", ModUi.Red,
				() => Act(msg, () => ModerationApi.RemoveStaff(name))));
			v.AddChild(actions);
			v.AddChild(msg);
		}
		if (!canEdit) return;
		_page.AddChild(SectionTitle("Add a team member"));
		var form = ModUi.Card(_page, PlayerOverlays.Pill);
		var row = ModUi.Row(8);
		var who = ModUi.Input("Username");
		row.AddChild(who);
		var newRole = ModUi.Options("Moderator", "Admin");
		row.AddChild(newRole);
		var addMsg = ModUi.Para("", 12, ModUi.Accent);
		var add = ModUi.Button("Add", ModUi.Green);
		add.Pressed += () =>
		{
			if (string.IsNullOrWhiteSpace(who.Text)) { addMsg.Text = "Type a username."; return; }
			Act(addMsg, () => ModerationApi.SetStaff(who.Text.Trim(), newRole.Selected == 1 ? "admin" : "mod"));
		};
		row.AddChild(add);
		form.AddChild(row);
		form.AddChild(addMsg);
	}

	// =====================================================================
	// Notices (home-screen banners): admins only. Card borders are managed on the website only.
	// =====================================================================

	/// <summary>Native "open image" dialog; calls back with the chosen path.</summary>
	private void PickImage(string title, Action<string> chosen)
	{
		var dialog = new FileDialog
		{
			Title = title, FileMode = FileDialog.FileModeEnum.OpenFile, Access = FileDialog.AccessEnum.Filesystem,
			Filters = new[] { "*.png, *.jpg, *.jpeg, *.webp ; Images" }, UseNativeDialog = true,
		};
		AddChild(dialog);
		dialog.FileSelected += path => { chosen(path); dialog.QueueFree(); };
		dialog.Canceled += dialog.QueueFree;
		dialog.PopupCentered(new Vector2I(900, 600));
	}

	/// <summary>"Choose image..." button that remembers the file and shows its name.</summary>
	private Control FilePicker(string title, Action<string> chosen, string buttonText = "Choose image...")
	{
		var row = ModUi.Row(8);
		var label = PlayerOverlays.Text("No file chosen", 12, PlayerOverlays.Dim, trim: true);
		label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		label.VerticalAlignment = VerticalAlignment.Center;
		var pick = ModUi.Button(buttonText);
		pick.Pressed += () => PickImage(title, path => { label.Text = System.IO.Path.GetFileName(path); chosen(path); });
		row.AddChild(pick);
		row.AddChild(label);
		return row;
	}

	private static TextureRect WebImage(string url, Vector2 size)
	{
		var rect = new TextureRect
		{
			CustomMinimumSize = size, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = MouseFilterEnum.Ignore,
		};
		PlayerApi.LoadPicture(url, tex => { if (IsInstanceValid(rect)) rect.Texture = tex; });
		return rect;
	}

	private async void ShowNotices(int mine)
	{
		var (list, error) = await ModerationApi.Notices();
		if (!Current(mine)) return;
		if (error != null) { Hint(error); return; }
		Hint("");
		_page.AddChild(ModUi.Para("These banners rotate on the home screen, top to bottom. Clicking one opens its link.", 13, PlayerOverlays.Dim));
		if (list.Count == 0) _page.AddChild(ModUi.Para("No notices yet. The home screen shows no banners.", 13, PlayerOverlays.Dim));
		for (int i = 0; i < list.Count; i++) _page.AddChild(NoticeCard(list[i], i, list.Count));

		_page.AddChild(SectionTitle("Add a notice"));
		var v = ModUi.Card(_page, PlayerOverlays.Pill);
		string file = null;
		v.AddChild(FilePicker("Pick the notice image", path => file = path));
		v.AddChild(PlayerOverlays.Text("Title", 13));
		var title = ModUi.Input("e.g. Halloween event", 200);
		v.AddChild(title);
		v.AddChild(PlayerOverlays.Text("Link when clicked (optional)", 13));
		var link = ModUi.Input("https://...", 500);
		v.AddChild(link);
		var msg = ModUi.Para("", 12, ModUi.Accent);
		var add = ModUi.Button("Add notice", ModUi.Green);
		add.Pressed += () =>
		{
			if (file == null) { msg.Text = "Pick an image."; return; }
			Act(msg, () => ModerationApi.AddNotice(file, title.Text.Trim(), link.Text.Trim()));
		};
		var buttons = ModUi.Row();
		buttons.AddChild(add);
		v.AddChild(buttons);
		v.AddChild(msg);
	}

	private Control NoticeCard(ModerationApi.NoticeRow n, int index, int count)
	{
		var holder = ModUi.Column(0);
		var v = ModUi.Card(holder, alpha: n.Active ? 1 : 0.75f);
		var row = ModUi.Row(14);
		v.AddChild(row);
		row.AddChild(WebImage(n.Image, new Vector2(200, 130)));
		var col = ModUi.Column(6);
		row.AddChild(col);

		var head = ModUi.Flow(8);
		head.AddChild(PlayerOverlays.Text(string.IsNullOrEmpty(n.Text) ? "(untitled)" : n.Text, 15, bold: true));
		head.AddChild(n.Active ? ModUi.Pill("shown", ModUi.Green) : ModUi.Pill("hidden", PlayerOverlays.ModPill));
		col.AddChild(head);
		var title = ModUi.Input("Title", 200);
		title.Text = n.Text;
		col.AddChild(title);
		var link = ModUi.Input("Link when clicked (none)", 500);
		link.Text = n.Redirect;
		col.AddChild(link);
		string file = null;
		col.AddChild(FilePicker("Pick a new image", path => file = path, "Replace image..."));

		var msg = ModUi.Para("", 12, ModUi.Accent);
		var actions = ModUi.Flow(6);
		var save = ModUi.Button("Save", ModUi.Green);
		save.Pressed += () => Act(msg, () => ModerationApi.EditNotice(n.Id, title.Text.Trim(), link.Text.Trim(), file));
		actions.AddChild(save);
		var toggle = ModUi.Button(n.Active ? "Hide" : "Show");
		toggle.Pressed += () => Act(msg, () => ModerationApi.ShowNotice(n.Id, !n.Active));
		actions.AddChild(toggle);
		var up = ModUi.Button("Move up");
		up.Disabled = index == 0;
		up.Pressed += () => Act(msg, () => ModerationApi.MoveNotice(n.Id, -1));
		actions.AddChild(up);
		var down = ModUi.Button("Move down");
		down.Disabled = index == count - 1;
		down.Pressed += () => Act(msg, () => ModerationApi.MoveNotice(n.Id, 1));
		actions.AddChild(down);
		actions.AddChild(ModUi.ConfirmButton("Delete", "Click again to delete", ModUi.Red, () => Act(msg, () => ModerationApi.DeleteNotice(n.Id))));
		col.AddChild(actions);
		col.AddChild(msg);
		return holder;
	}
}
