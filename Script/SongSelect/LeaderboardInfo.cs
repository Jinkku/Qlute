using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Song select's leaderboard, as a carousel: the wheel moves a focus one score at a time and the list glides to keep
/// it in view. The focused row pops out; the further a row is from it, the more it curves away (left) and dims.
/// Rows are Panels/SongSelectButtons/Leaderboard.tscn, stacked by the "Leaderboard" VBox (its separation is the gap).
/// </summary>
public partial class LeaderboardInfo : Control
{
	/// <summary>How far (px) rows curve away from the focused one, reached 4 rows away.</summary>
	[Export] public float Arc = 36f;
	/// <summary>How far (px) the focused row sticks out to the right.</summary>
	[Export] public float PopOut = 14f;
	/// <summary>Brightness lost per row of distance from the focus (up to 4 rows).</summary>
	[Export] public float Dim = 0.1f;
	/// <summary>Glide speed (higher = snappier).</summary>
	[Export] public float Glide = 10f;

	/// <summary>
	/// Test switch: run the game with <c>-- --fake-leaderboard</c> (12 scores) or <c>-- --fake-leaderboard=30</c> and every
	/// map shows made-up scores instead of the real ones, so the carousel can be tried on any map. 0 = off.
	/// </summary>
	private static readonly int FakeCount = ParseFakeCount();

	private static int ParseFakeCount()
	{
		foreach (var a in OS.GetCmdlineUserArgs())
		{
			if (a == "--fake-leaderboard") return 12;
			if (a.StartsWith("--fake-leaderboard=") && int.TryParse(a["--fake-leaderboard=".Length..], out var n)) return Math.Clamp(n, 0, 200);
		}
		return 0;
	}

	private static readonly string[] FakeNames =
	{
		"Kupachan", "reirin_", "eviee", "Essence", "-Kasane Teto-", "zanka", "canfrasTH", "Iridium", "Sotarks",
		"tapperruiii", "LukasDrakon", "Nooma", "aquapoki", "White Hare", "mapper_with_a_really_long_name", "Sympho",
	};

	/// <summary>Made-up scores for the selected map (the same ones every time for a map), best first.</summary>
	private static List<LeaderboardEntry> FakeScores(int count)
	{
		var rng = new Random(SettingsOperator.SessionConfig.SongID * 7919 + count);
		var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
		const int notes = 800;
		string[] mods = { "", "", "", "", "DT", "HD", "DT HD", "NF", "HT" };
		var list = new List<LeaderboardEntry>();
		for (int i = 0; i < count; i++)
		{
			// Worse the further down: more Greats / Mehs / misses.
			int bad = i == 0 ? 0 : rng.Next(0, 2 + i * 3);
			int meh = i == 0 ? 0 : rng.Next(0, 2 + i * 2);
			int good = i == 0 ? 0 : rng.Next(1, 4 + i * 6);
			int max = Math.Max(0, notes - bad - meh - good);
			bool you = i == 2 && !string.IsNullOrEmpty(ApiOperator.Username) && ApiOperator.Username != "Guest";
			list.Add(new LeaderboardEntry
			{
				username = you ? ApiOperator.Username : FakeNames[i % FakeNames.Length] + (i >= FakeNames.Length ? (i / FakeNames.Length + 1).ToString() : ""),
				MAX = max, GOOD = good, MEH = meh, BAD = bad,
				combo = bad == 0 ? notes : rng.Next(notes / (bad + 1), notes - bad),
				mods = mods[rng.Next(mods.Length)],
				time = now - (long)(rng.NextDouble() * 400 * 86400) - 60,
				points = Math.Round(200.0 * max / notes * (1 - i * 0.02), 2),
			});
		}
		// Score from the hits (Perfect 10 / Great 7 / Meh 3, 1M for all Perfect, minus misses), best first.
		foreach (var e in list) e.score = Math.Max(0, (int)((e.MAX * 10 + e.GOOD * 7 + e.MEH * 3) * 100000L / notes) - e.BAD * 900000 / notes);
		list.Sort((a, b) => b.score.CompareTo(a.score));
		return list;
	}

	private const string RowScene = "res://Panels/SongSelectButtons/Leaderboard.tscn";
	private static PackedScene _rowScene;

	private VBoxContainer Leaderboards { get; set; }
	private Label NoBeatmap { get; set; }
	private List<LeaderboardEntry> oldLeaderboardList { get; set; } = new List<LeaderboardEntry>();
	private readonly List<Leaderboard> _rows = new();
	private string _noScoresText;
	private int _focus;
	private float _focusSmooth, _scroll;

	public override void _Ready()
	{
		_rowScene ??= GD.Load<PackedScene>(RowScene);
		Leaderboards = GetNode<VBoxContainer>("Leaderboard");
		NoBeatmap = GetNode<Label>("NoBeatmap");
		_noScoresText = NoBeatmap.Text;
		MouseFilter = MouseFilterEnum.Stop; // takes the wheel (rows pass it up)
		refreshl();
	}

	private void refreshl()
	{
		oldLeaderboardList = ApiOperator.LeaderboardList;
		foreach (var row in _rows) row.QueueFree();
		_rows.Clear();
		_focus = 0;
		_focusSmooth = 0;
		_scroll = 0;
		var scores = FakeCount > 0 ? FakeScores(FakeCount) : ApiOperator.LeaderboardList;
		if (scores.Count < 1 && (FakeCount > 0 || ApiOperator.LeaderboardStatus == 2))
		{
			// The server recognises maps by the hash of the .osu: say why there's nothing when it didn't recognise ours.
			NoBeatmap.Text = FakeCount == 0 && SettingsOperator.LeaderboardType == 1 ? ApiOperator.LeaderboardMapStatus switch
			{
				"modified" => "Your copy of this beatmap is different from the server's\n(it was edited or is outdated), so it has no leaderboard.\nRe-download it to see and submit scores.",
				"unknown" => "This beatmap isn't on the server,\nso it has no leaderboard.",
				_ => _noScoresText,
			} : _noScoresText;
			NoBeatmap.Visible = true;
			Leaderboards.Visible = false;
			return;
		}
		NoBeatmap.Visible = false;
		Leaderboards.Visible = true;
		var rank = 1;
		foreach (var entry in scores)
		{
			entry.Accuracy = Gameplay.ReloadAccuracy(entry.MAX, entry.GOOD, entry.MEH, entry.BAD);
			var row = _rowScene.Instantiate<Leaderboard>();
			row.Info = entry;
			row.Rank = rank;
			Leaderboards.AddChild(row);
			_rows.Add(row);
			// Fade in one after another (only the first screenful waits; the rest are off screen anyway).
			row.Modulate = new Color(1, 1, 1, 0);
			var t = row.CreateTween();
			t.TweenInterval(Math.Min(rank - 1, 8) * 0.035f);
			t.TweenProperty(row, "modulate:a", 1f, 0.2f);
			rank++;
		}
	}

	public override void _GuiInput(InputEvent e)
	{
		if (_rows.Count == 0 || e is not InputEventMouseButton { Pressed: true } mb) return;
		if (mb.ButtonIndex == MouseButton.WheelUp) _focus = Math.Max(0, _focus - 1);
		else if (mb.ButtonIndex == MouseButton.WheelDown) _focus = Math.Min(_rows.Count - 1, _focus + 1);
		else return;
		AcceptEvent();
	}

	public override void _Process(double delta)
	{
		if (oldLeaderboardList != ApiOperator.LeaderboardList)
			refreshl();
		if (_rows.Count == 0) return;

		float k = 1f - Mathf.Exp(-(float)delta * Glide);
		_focusSmooth = Mathf.Lerp(_focusSmooth, _focus, k);

		// Keep the focused row in the middle where the list allows (no empty space above the first row or below the last).
		var focusRow = _rows[Math.Clamp(_focus, 0, _rows.Count - 1)];
		float content = Leaderboards.GetCombinedMinimumSize().Y;
		float target = Mathf.Clamp(focusRow.Position.Y + focusRow.Size.Y / 2 - Size.Y / 2, 0, Mathf.Max(0, content - Size.Y));
		_scroll = Mathf.Lerp(_scroll, target, k);
		Leaderboards.Position = new Vector2(0, -_scroll);
		Leaderboards.Size = new Vector2(Size.X, content);

		float reach = Arc + PopOut; // cards are this much narrower than the list, so every position fits
		for (int i = 0; i < _rows.Count; i++)
		{
			float dist = Mathf.Min(Mathf.Abs(i - _focusSmooth), 4f);
			float t = dist / 4f;
			float x = Arc - Arc * t * t + PopOut * Mathf.Max(0f, 1f - dist);
			_rows[i].SetPlacement(x, reach, 1f - Dim * dist);
		}
	}
}
