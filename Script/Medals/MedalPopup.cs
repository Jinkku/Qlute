using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json.Nodes;

/// <summary>
/// "Medal Achieved" popups (autoload). The server records the first time each medal is earned (Chikara
/// online/medals.py) and hands every new one out exactly once: in the score-submit reply ("medals") and from
/// apiv2/medals/new, which this asks right after logging in and every few minutes after (medals earned on the website,
/// like Collector or Critic).
///
/// Each medal is a big round badge (Resources/System/MedalOverlay.png) in the middle of a dimmed screen: "Medal
/// Achieved", the medal's name, its description. Several in a row play one after another with page dots under the
/// badge; each stays <see cref="ShowSeconds"/>, a click moves on, Esc / right-click closes the rest. Nothing pops up
/// during gameplay; they wait for the results screen.
///
/// Dev preview: -- --medal-demo [--medal-demo-shot=/path/shot.png] (sample medals; saves shot.png and shot_2.png, then quits).
/// </summary>
public partial class MedalPopup : CanvasLayer
{
	public class Medal
	{
		public int Id;
		public string Title = "", Desc = "", Hint = "";
		public long AchievedAt;
	}

	public static MedalPopup Instance { get; private set; }

	public const double ShowSeconds = 4.5;
	private const double PollSeconds = 180;
	private const double MinShownBeforeClick = 0.45; // a click right as it appears doesn't skip it unseen
	private const string OverlayPath = "res://Resources/System/MedalOverlay.png";
	private const string FirstSound = "res://SelectableSkins/Slia/Sounds/comboclear.wav";
	private const string NextSound = "res://SelectableSkins/Slia/Sounds/notification.wav";

	private static readonly Color Backdrop = new(0.11f, 0f, 0.08f, 0.88f); // deep plum, like the website's medal screen

	private static readonly string[] Args = OS.GetCmdlineUserArgs();
	private static string ArgValue(string prefix) => Args.FirstOrDefault(a => a.StartsWith(prefix))?[prefix.Length..];

	private readonly Queue<Medal> _queue = new();
	private readonly HashSet<int> _shownIds = new(); // per account: a medal never pops twice in one session
	private string _shownFor;

	private Texture2D _overlay;
	private ColorRect _backdrop;
	private Control _footer;
	private HBoxContainer _dots;
	private Label _counter, _tip;
	private MedalView _view;
	private Tween _backdropTween;
	private bool _active, _switching;
	private int _batchShown, _batchTotal;
	private double _left, _shownFor_s;
	private double _time, _poll = PollSeconds;
	private bool _checking;
	private double _demoAt = -1;
	private readonly List<(double at, string path)> _shots = new();

	public override void _Ready()
	{
		Instance = this;
		Layer = 110; // over the player overlays (90), under the privacy prompt (120)
		ProcessMode = ProcessModeEnum.Always;
		_overlay = ResourceLoader.Exists(OverlayPath) ? GD.Load<Texture2D>(OverlayPath) : null;

		_backdrop = new ColorRect { Color = Backdrop, MouseFilter = Control.MouseFilterEnum.Stop, Visible = false };
		_backdrop.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_backdrop.GuiInput += OnBackdropInput;
		AddChild(_backdrop);

		// Under the badge: "2 / 3", page dots, "Click to continue"
		var footer = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Begin };
		footer.AddThemeConstantOverride("separation", 8);
		footer.AnchorLeft = footer.AnchorRight = 0.5f;
		footer.AnchorTop = footer.AnchorBottom = 0.5f;
		footer.OffsetLeft = -200;
		footer.OffsetRight = 200;
		_footer = footer;
		_backdrop.AddChild(footer);

		_dots = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
		_dots.AddThemeConstantOverride("separation", 8);
		footer.AddChild(_dots);
		_counter = Centered(PlayerOverlays.Text("", 14, new Color(1, 1, 1, 0.8f), bold: true));
		footer.AddChild(_counter);
		_tip = Centered(PlayerOverlays.Text("", 12, new Color(1, 1, 1, 0.45f)));
		footer.AddChild(_tip);

		if (Args.Contains("--medal-demo"))
		{
			_demoAt = 1.5;
			if (ArgValue("--medal-demo-shot=") is { } shot)
			{
				_shots.Add((4.0, shot));
				_shots.Add((7.4, shot.EndsWith(".png") ? shot[..^4] + "_2.png" : shot + "_2.png"));
			}
		}
	}

	private static Label Centered(Label l)
	{
		l.HorizontalAlignment = HorizontalAlignment.Center;
		return l;
	}

	// ------------------------------------------------------------------ public

	/// <summary>Queues medals to pop up (main thread).</summary>
	public static void Show(IEnumerable<Medal> medals)
	{
		if (Instance == null || medals == null) return;
		Instance.Enqueue(medals);
	}

	/// <summary>Pops up the "medals" of a server reply (the score submission's), if it has any.</summary>
	public static void ShowFromReply(string json)
	{
		try { Show(Read(JsonNode.Parse(json)?["medals"])); }
		catch (Exception e) { GD.PrintErr($"[Medals] couldn't read the reply: {e.Message}"); }
	}

	/// <summary>Asks the server for new medals shortly (after logging in).</summary>
	public static void CheckSoon(double seconds = 2)
	{
		if (Instance != null) Instance._poll = Math.Min(Instance._poll, seconds);
	}

	public static async void CheckNow()
	{
		var me = Instance;
		if (me == null || me._checking || !OnlineApi.Available || SettingsOperator.NoConnectionToGameServer) return;
		me._checking = true;
		var who = ApiOperator.Username;
		try
		{
			using var req = PlayerApi.Authed(HttpMethod.Get, "apiv2/medals/new");
			using var res = await ApiOperator.Http.SendAsync(req);
			if (!res.IsSuccessStatusCode) return; // older server without medals, or logged out meanwhile
			var json = JsonNode.Parse(await res.Content.ReadAsStringAsync());
			if (who == ApiOperator.Username) Show(Read(json?["medals"]));
		}
		catch (Exception e)
		{
			GD.Print($"[Medals] couldn't check for new medals: {e.Message}");
		}
		finally
		{
			me._checking = false;
		}
	}

	private static readonly HashSet<string> Claimed = new(); // "account/name" already sent this session

	/// <summary>
	/// A secret medal only the game can see (the Konami code, a rage quit...): asks the server to record it
	/// (POST apiv2/medals/claim/NAME, NAME from Chikara views.CLIENT_MEDALS) and pops it up the first time.
	/// Logged-out players and spectating don't count.
	/// </summary>
	public static async void Claim(string name)
	{
		if (Instance == null || !OnlineApi.Available || SettingsOperator.SpectatorMode || SettingsOperator.NoConnectionToGameServer) return;
		var who = ApiOperator.Username;
		if (!Claimed.Add(who + "/" + name)) return;
		try
		{
			using var req = PlayerApi.Authed(HttpMethod.Post, "apiv2/medals/claim/" + Uri.EscapeDataString(name));
			using var res = await ApiOperator.Http.SendAsync(req);
			if (!res.IsSuccessStatusCode)
			{
				if ((int)res.StatusCode >= 500) Claimed.Remove(who + "/" + name); // try again next time
				return;
			}
			var json = JsonNode.Parse(await res.Content.ReadAsStringAsync());
			if (who == ApiOperator.Username) Show(Read(json?["medals"]));
		}
		catch (Exception e)
		{
			Claimed.Remove(who + "/" + name);
			GD.Print($"[Medals] couldn't claim {name}: {e.Message}");
		}
	}

	// ↑↑↓↓←→←→ B A, anywhere in the game: "Cheat Code"
	private static readonly Key[] Konami = { Key.Up, Key.Up, Key.Down, Key.Down, Key.Left, Key.Right, Key.Left, Key.Right, Key.B, Key.A };
	private int _konami;

	private void TrackKonami(InputEvent e)
	{
		if (e is not InputEventKey { Pressed: true, Echo: false } k) return;
		var key = k.PhysicalKeycode != Key.None ? k.PhysicalKeycode : k.Keycode;
		_konami = key == Konami[_konami] ? _konami + 1 : key == Konami[0] ? 1 : 0;
		if (_konami == Konami.Length)
		{
			_konami = 0;
			Claim("konami");
		}
	}

	public static List<Medal> Read(JsonNode node) =>
		(node as JsonArray)?.OfType<JsonObject>().Select(m => new Medal
		{
			Id = (int)OnlineApi.Long(m, "id", -1),
			Title = OnlineApi.Str(m, "title") ?? "",
			Desc = OnlineApi.Str(m, "desc") ?? "",
			Hint = OnlineApi.Str(m, "hint") ?? "",
			AchievedAt = OnlineApi.Long(m, "achieved_at"),
		}).Where(m => m.Title != "").ToList() ?? new();

	// ------------------------------------------------------------------ queue

	private void Enqueue(IEnumerable<Medal> medals)
	{
		if (_shownFor != ApiOperator.Username)
		{
			_shownFor = ApiOperator.Username;
			_shownIds.Clear();
		}
		int added = 0;
		foreach (var m in medals)
			if (m.Id < 0 || _shownIds.Add(m.Id))
			{
				_queue.Enqueue(m);
				added++;
			}
		if (_active && added > 0)
		{
			_batchTotal += added;
			UpdateFooter();
		}
	}

	public override void _Process(double delta)
	{
		_time += delta;

		if (OnlineApi.Available)
		{
			_poll -= delta;
			if (_poll <= 0)
			{
				_poll = PollSeconds;
				CheckNow();
			}
		}

		if (!_active && _queue.Count > 0 && !SettingsOperator.inGameplay) Begin();

		if (_active && !_switching && IsInstanceValid(_view))
		{
			_shownFor_s += delta;
			_left -= delta;
			_view.Progress = (float)Math.Clamp(_left / ShowSeconds, 0, 1);
			if (_left <= 0) Advance();
		}
		if (_active) Layout();

		if (_demoAt >= 0 && _time >= _demoAt)
		{
			_demoAt = -1;
			Show(DemoMedals());
		}
		if (_shots.Count > 0 && _time >= _shots[0].at)
		{
			var path = _shots[0].path;
			_shots.RemoveAt(0);
			GetViewport().GetTexture().GetImage().SavePng(path);
			GD.Print($"[Medals] demo shot saved to {path}");
			if (_shots.Count == 0) GetTree().Quit();
		}
	}

	/// <summary>Badge diameter for this window: about half its height.</summary>
	private float Diameter => Mathf.Clamp(GetViewport().GetVisibleRect().Size.Y * 0.5f, 240f, 440f);

	private void Layout()
	{
		float d = Diameter;
		_footer.OffsetTop = d / 2 + 22;
		_footer.OffsetBottom = d / 2 + 120;
		if (IsInstanceValid(_view) && !Mathf.IsEqualApprox(_view.Diameter, d)) _view.Resize(d);
	}

	private void Begin()
	{
		_active = true;
		_batchShown = 0;
		_batchTotal = _queue.Count;
		_backdrop.Visible = true;
		_backdropTween?.Kill();
		_backdropTween = CreateTween();
		_backdropTween.TweenProperty(_backdrop, "modulate:a", 1f, 0.3).From(0f);
		Next();
	}

	private void Next()
	{
		_switching = false;
		if (_queue.Count == 0)
		{
			End();
			return;
		}
		var m = _queue.Dequeue();
		_batchShown++;
		_view = new MedalView(m, _overlay, Diameter);
		_backdrop.AddChild(_view);
		_backdrop.MoveChild(_footer, -1);
		_view.Enter();
		_left = ShowSeconds;
		_shownFor_s = 0;
		Sample.PlaySample(_batchShown == 1 ? FirstSound : NextSound, _batchShown == 1 ? 1f : 1.1f);
		UpdateFooter();
	}

	private void Advance()
	{
		if (_switching) return;
		_switching = true;
		var old = _view;
		_view = null;
		if (IsInstanceValid(old)) old.Leave();
		if (_queue.Count > 0)
			GetTree().CreateTimer(0.22, processAlways: true).Timeout += Next;
		else
			End();
	}

	private void End()
	{
		_switching = false;
		_backdropTween?.Kill();
		_backdropTween = CreateTween();
		_backdropTween.TweenProperty(_backdrop, "modulate:a", 0f, 0.3);
		_backdropTween.TweenCallback(Callable.From(() =>
		{
			_backdrop.Visible = false;
			_active = false;
		}));
	}

	/// <summary>Closes this one and skips the rest.</summary>
	private void DismissAll()
	{
		_queue.Clear();
		Advance();
	}

	private void OnBackdropInput(InputEvent e)
	{
		if (!_active || e is not InputEventMouseButton { Pressed: true } b) return;
		if (b.ButtonIndex == MouseButton.Left && _shownFor_s >= MinShownBeforeClick) Advance();
		else if (b.ButtonIndex == MouseButton.Right) DismissAll();
		_backdrop.AcceptEvent();
	}

	public override void _Input(InputEvent e)
	{
		TrackKonami(e);
		if (!_active || !_backdrop.Visible) return;
		if (e.IsActionPressed("ui_cancel"))
		{
			DismissAll();
			GetViewport().SetInputAsHandled();
		}
		else if (e.IsActionPressed("ui_accept") && _shownFor_s >= MinShownBeforeClick)
		{
			Advance();
			GetViewport().SetInputAsHandled();
		}
	}

	private void UpdateFooter()
	{
		bool many = _batchTotal > 1;
		_counter.Visible = many;
		_counter.Text = $"{_batchShown} / {_batchTotal}";
		_tip.Text = _batchShown < _batchTotal ? "Click to continue  ·  Esc to skip all" : "Click to close";

		foreach (Node c in _dots.GetChildren()) c.QueueFree();
		_dots.Visible = many && _batchTotal <= 12;
		if (!_dots.Visible) return;
		for (int i = 1; i <= _batchTotal; i++)
		{
			bool current = i == _batchShown;
			var dot = new Panel
			{
				CustomMinimumSize = new Vector2(current ? 22 : 8, 8),
				MouseFilter = Control.MouseFilterEnum.Ignore,
			};
			var col = current ? Colors.White : i < _batchShown ? new Color(1, 1, 1, 0.55f) : new Color(1, 1, 1, 0.2f);
			dot.AddThemeStyleboxOverride("panel", PlayerOverlays.Style(col, 4, 0, 0));
			_dots.AddChild(dot);
		}
	}

	private static List<Medal> DemoMedals() => new()
	{
		new() { Id = 1001, Title = ">~<", Desc = "You have made the impossible possible o-o", Hint = "Chain a combo of over 1,000 without dropping it" },
		new() { Id = 1002, Title = "Rising Star", Desc = "Someone's been practising ✨", Hint = "Reach 100 pp" },
		new() { Id = 1003, Title = "Making the game better", Desc = "You are making the community better!", Hint = "Join the Qlute team" },
	};
}

/// <summary>
/// One medal: the round badge (MedalOverlay.png) with "Medal Achieved", the medal name and its description inside,
/// slowly turning light rays and a sparkle burst behind it, and a thin ring around the rim that counts down.
/// </summary>
public partial class MedalView : Control
{
	public float Diameter { get; private set; }
	public float Progress = 1;

	private readonly MedalPopup.Medal _medal;
	private readonly Texture2D _overlay;
	private Control _content;
	private Label _header, _title, _desc;
	private double _t, _burstT = -1;
	private bool _leaving;

	private struct Spark { public float Angle, Speed, Life, Size; public bool Tint; }
	private readonly List<Spark> _sparks = new();
	private readonly Random _rng = new();

	private static readonly Color Ray = new(0.75f, 0.78f, 1f, 0.035f);
	private static readonly Color Blue = new("#6f7cff");

	public MedalView(MedalPopup.Medal medal, Texture2D overlay, float diameter)
	{
		_medal = medal;
		_overlay = overlay;
		MouseFilter = MouseFilterEnum.Ignore;
		AnchorLeft = AnchorRight = AnchorTop = AnchorBottom = 0.5f;
		Build(diameter);
	}

	private void Build(float d)
	{
		Diameter = d;
		OffsetLeft = OffsetTop = -d / 2;
		OffsetRight = OffsetBottom = d / 2;
		PivotOffset = new Vector2(d / 2, d / 2);

		foreach (Node c in GetChildren()) c.QueueFree();

		if (_overlay != null)
		{
			var face = new TextureRect
			{
				Texture = _overlay,
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
				MouseFilter = MouseFilterEnum.Ignore,
			};
			face.SetAnchorsPreset(LayoutPreset.FullRect);
			AddChild(face);
		}

		// Time left: a thin white arc running along the grey rim (over the picture, so it's its own child)
		var rim = new RimTimer { View = this, MouseFilter = MouseFilterEnum.Ignore };
		rim.SetAnchorsPreset(LayoutPreset.FullRect);
		AddChild(rim);

		// Text sits in the blue face (the image's inner circle is ~88% of it); keep inside its middle ~64%.
		float w = d * 0.64f;
		var content = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
		content.AnchorLeft = content.AnchorRight = content.AnchorTop = content.AnchorBottom = 0.5f;
		content.OffsetLeft = -w / 2;
		content.OffsetRight = w / 2;
		content.OffsetTop = -d * 0.34f;
		content.OffsetBottom = d * 0.34f;
		content.AddThemeConstantOverride("separation", (int)(d * 0.05f));
		AddChild(content);
		_content = content;

		_header = Line("Medal Achieved", Mathf.RoundToInt(d * 0.068f), false);
		_title = Line(_medal.Title, TitleSize(_medal.Title, d), true);
		_desc = Line(_medal.Desc, Mathf.RoundToInt(d * 0.04f), false);
		_desc.Modulate = new Color(1, 1, 1, 0.92f);
		content.AddChild(_header);
		content.AddChild(_title);
		if (!string.IsNullOrEmpty(_medal.Desc)) content.AddChild(_desc);

		Label Line(string text, int size, bool bold)
		{
			var l = PlayerOverlays.Text(text, size, Colors.White, wrap: true, bold: bold);
			l.HorizontalAlignment = HorizontalAlignment.Center;
			l.CustomMinimumSize = new Vector2(w, 0);
			return l;
		}
	}

	/// <summary>Big for short names like "&gt;~&lt;", smaller (and up to two lines) for long ones.</summary>
	private static int TitleSize(string title, float d)
	{
		int n = title.Length;
		float k = n <= 4 ? 0.2f : n <= 8 ? 0.15f : n <= 14 ? 0.11f : 0.085f;
		return Mathf.RoundToInt(d * k);
	}

	public void Resize(float d)
	{
		Build(d);
		_header.Modulate = _title.Modulate = new Color(1, 1, 1, 1);
	}

	public void Enter()
	{
		Scale = new Vector2(0.35f, 0.35f);
		Rotation = Mathf.DegToRad(-10);
		Modulate = new Color(1, 1, 1, 0);
		foreach (var l in new[] { _header, _title, _desc }) l.Modulate = new Color(1, 1, 1, 0);
		_title.PivotOffset = new Vector2(_title.CustomMinimumSize.X / 2, 0);

		var t = CreateTween().SetParallel();
		t.TweenProperty(this, "modulate:a", 1f, 0.2);
		t.TweenProperty(this, "scale", Vector2.One, 0.7).SetTrans(Tween.TransitionType.Elastic).SetEase(Tween.EaseType.Out);
		t.TweenProperty(this, "rotation", 0f, 0.6).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		t.TweenCallback(Callable.From(Burst)).SetDelay(0.12);
		t.TweenProperty(_header, "modulate:a", 1f, 0.3).SetDelay(0.18);
		t.TweenProperty(_title, "modulate:a", 1f, 0.3).SetDelay(0.3);
		t.TweenProperty(_title, "scale", Vector2.One, 0.45).From(new Vector2(1.25f, 1.25f)).SetDelay(0.3)
			.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		t.TweenProperty(_desc, "modulate:a", 0.92f, 0.35).SetDelay(0.45);
	}

	public void Leave()
	{
		if (_leaving) return;
		_leaving = true;
		var t = CreateTween().SetParallel();
		t.TweenProperty(this, "modulate:a", 0f, 0.22);
		t.TweenProperty(this, "scale", new Vector2(1.12f, 1.12f), 0.22).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		t.Chain().TweenCallback(Callable.From(QueueFree));
	}

	private void Burst()
	{
		_burstT = 0;
		_sparks.Clear();
		for (int i = 0; i < 26; i++)
			_sparks.Add(new Spark
			{
				Angle = (float)(i / 26.0 * Math.Tau + _rng.NextDouble() * 0.25),
				Speed = 0.12f + (float)_rng.NextDouble() * 0.16f,
				Life = 0.6f + (float)_rng.NextDouble() * 0.5f,
				Size = 0.012f + (float)_rng.NextDouble() * 0.014f,
				Tint = i % 3 == 0,
			});
	}

	public override void _Process(double delta)
	{
		_t += delta;
		if (_burstT >= 0) _burstT += delta;
		QueueRedraw();
	}

	public override void _Draw()
	{
		float d = Diameter, r = d / 2;
		var c = new Vector2(r, r);
		float t = (float)_t;

		// Light rays behind the badge, slowly turning
		float spin = t * 0.25f;
		for (int i = 0; i < 16; i++)
		{
			float a = spin + i * Mathf.Tau / 16;
			float w = 0.045f;
			float len = r * (i % 2 == 0 ? 1.55f : 1.3f);
			DrawColoredPolygon(new[]
			{
				c,
				c + new Vector2(Mathf.Cos(a - w), Mathf.Sin(a - w)) * len,
				c + new Vector2(Mathf.Cos(a + w), Mathf.Sin(a + w)) * len,
			}, Ray);
		}

		// Soft halo right around the rim
		for (int i = 1; i <= 6; i++)
			DrawCircle(c, r + i * d * 0.012f, new Color(Blue, 0.035f));

		// Shockwave ring + sparkles when it lands
		if (_burstT >= 0)
		{
			float bt = (float)_burstT;
			if (bt < 0.7f)
			{
				float k = bt / 0.7f;
				float ease = 1 - (1 - k) * (1 - k);
				DrawArc(c, r * (1.0f + 0.45f * ease), 0, Mathf.Tau, 96, new Color(1, 1, 1, 0.5f * (1 - k)), d * 0.012f * (1 - k) + 1, true);
			}
			foreach (var s in _sparks)
			{
				if (bt > s.Life) continue;
				float k = bt / s.Life;
				float ease = 1 - (1 - k) * (1 - k);
				var p = c + new Vector2(Mathf.Cos(s.Angle), Mathf.Sin(s.Angle)) * (r * 0.95f + d * s.Speed * ease);
				var col = s.Tint ? new Color(Blue.Lightened(0.35f), 1 - k) : new Color(1, 1, 1, 1 - k);
				Sparkle(p, d * s.Size * (1 - k * 0.4f), col);
			}
		}
	}

	private void Sparkle(Vector2 p, float size, Color col)
	{
		float w = size * 0.28f;
		DrawColoredPolygon(new[] { p + new Vector2(0, -size), p + new Vector2(w, 0), p + new Vector2(0, size), p + new Vector2(-w, 0) }, col);
		DrawColoredPolygon(new[] { p + new Vector2(-size, 0), p + new Vector2(0, -w), p + new Vector2(size, 0), p + new Vector2(0, w) }, col);
	}
}

/// <summary>The countdown arc on the badge's grey rim (MedalOverlay.png: blue face ~88% of the width, rim outside it).</summary>
public partial class RimTimer : Control
{
	public MedalView View;

	public override void _Process(double delta) => QueueRedraw();

	public override void _Draw()
	{
		if (!IsInstanceValid(View)) return;
		float r = Size.X / 2, p = Mathf.Clamp(View.Progress, 0, 1);
		var c = Size / 2;
		float radius = r * 0.94f, width = Mathf.Max(2, r * 0.022f);
		DrawArc(c, radius, 0, Mathf.Tau, 128, new Color(0.18f, 0.2f, 0.45f, 0.12f), width, true);
		if (p > 0.001f)
			DrawArc(c, radius, -Mathf.Pi / 2, -Mathf.Pi / 2 + Mathf.Tau * p, Math.Max(8, (int)(128 * p)), new Color("#6f7cffcc"), width, true);
	}
}
