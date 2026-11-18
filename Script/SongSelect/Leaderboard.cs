using Godot;
using System;
using Game;

/// <summary>
/// One song select leaderboard row (Panels/SongSelectButtons/Leaderboard.tscn): rank, avatar, name, how long ago,
/// max combo, accuracy, score with its mods and a grade tab. Clicking it opens the score's results.
/// The parts are found by unique name, so the look is edited in the scene.
/// </summary>
public partial class Leaderboard : Button
{
	public LeaderboardEntry Info { get; set; }
	/// <summary>Position in the list (1 = best).</summary>
	public int Rank { get; set; } = 1;

	/// <summary>Values that are as good as they get (full combo, 100%) use this colour.</summary>
	private static readonly Color Perfect = new(0.66f, 0.95f, 0.5f);
	private static readonly Color OwnTint = new(0.45f, 0.3f, 0.85f, 0.9f);
	private static readonly Color[] Podium = { new(1f, 0.84f, 0.3f), new(0.8f, 0.82f, 0.88f), new(0.86f, 0.58f, 0.36f) };

	private Control _slide;
	private PanelContainer _card;
	private StyleBoxFlat _cardStyle;
	private TextureRect _picture;
	private Label _ago;
	private Texture2D _guest;
	private long _shownAgo = -1;

	public override void _Ready()
	{
		_slide = GetNode<Control>("%Slide");
		_card = GetNode<PanelContainer>("%Card");
		_picture = GetNode<TextureRect>("%Picture");
		_ago = GetNode<Label>("%Ago");
		_guest = _picture.Texture;
		// The rounded-corner shader needs the picture's size (it can be changed in the scene).
		_picture.Resized += () => (_picture.Material as ShaderMaterial)?.SetShaderParameter("size", _picture.Size);
		if (Info == null) return;

		var rank = GetNode<Label>("%Rank");
		rank.Text = $"#{Rank}";
		rank.AddThemeColorOverride("font_color", Rank <= 3 ? Podium[Rank - 1] : new Color(1, 1, 1, 0.85f));

		GetNode<Label>("%Username").Text = Info.username;
		GetNode<Label>("%Score").Text = Info.score.ToString("N0");

		var combo = GetNode<Label>("%Combo");
		combo.Text = Info.combo.ToString("N0") + "x";
		if (Info.BAD == 0 && Info.combo > 0) combo.AddThemeColorOverride("font_color", Perfect); // full combo

		var acc = GetNode<Label>("%Accuracy");
		acc.Text = (Info.Accuracy * 100).ToString("0.00") + "%";
		if (Info.Accuracy >= 1) acc.AddThemeColorOverride("font_color", Perfect);

		string grade = Grade(Info);
		var tab = GetNode<PanelContainer>("%GradeTab");
		var tabStyle = (StyleBoxFlat)tab.GetThemeStylebox("panel").Duplicate();
		tabStyle.BgColor = PlayerOverlays.GradeColor(grade);
		tab.AddThemeStyleboxOverride("panel", tabStyle);
		GetNode<Label>("%Grade").Text = grade;

		var pill = GetNode<Label>("%ModPill");
		foreach (var mod in (Info.mods ?? "").Split(new[] { ' ', ',', '+', '|' }, StringSplitOptions.RemoveEmptyEntries))
		{
			var p = (Label)pill.Duplicate();
			p.Text = mod;
			p.Visible = true;
			pill.GetParent().AddChild(p);
		}

		// Your own scores get a violet tint; hover lightens the card (a copy of the scene's style, so it stays editable).
		_cardStyle = (StyleBoxFlat)_card.GetThemeStylebox("panel").Duplicate();
		if (!string.IsNullOrEmpty(ApiOperator.Username) && Info.username == ApiOperator.Username)
			_cardStyle.BgColor = _cardStyle.BgColor.Lerp(OwnTint, 0.45f);
		_card.AddThemeStyleboxOverride("panel", _cardStyle);
		_baseBg = _cardStyle.BgColor;
		MouseEntered += () => _cardStyle.BgColor = _baseBg.Lightened(0.12f);
		MouseExited += () => _cardStyle.BgColor = _baseBg;
	}

	private Color _baseBg;

	/// <summary>
	/// Places the row's content for the carousel without changing the row's own rect: <paramref name="x"/> in from the
	/// left (the content is <paramref name="reach"/> narrower than the row, so x up to reach still fits) and how bright.
	/// </summary>
	public void SetPlacement(float x, float reach, float brightness)
	{
		if (_slide == null) return;
		_slide.OffsetLeft = x;
		_slide.OffsetRight = x - reach;
		_slide.Modulate = new Color(brightness, brightness, brightness);
	}

	/// <summary>Same grading as the website and the profile pages.</summary>
	public static string Grade(LeaderboardEntry e)
	{
		int total = e.MAX + e.GOOD + e.MEH + e.BAD;
		if (total == 0) return "?";
		double acc = (e.MAX + e.GOOD / 2.0 + e.MEH / 3.0) / total * 100;
		if (acc >= 100) return "SS";
		if (acc > 95 && e.BAD == 0) return "S";
		if (acc > 90) return "A";
		if (acc > 85) return "B";
		if (acc > 69) return "C";
		if (acc < 1) return "?";
		return "D";
	}

	/// <summary>"45s", "12m", "3h", "5d", "2w", "3mo", "1y".</summary>
	public static string Ago(long unixSeconds)
	{
		long s = Math.Max(0, DateTimeOffset.UtcNow.ToUnixTimeSeconds() - unixSeconds);
		if (s < 60) return $"{s}s";
		if (s < 3600) return $"{s / 60}m";
		if (s < 86400) return $"{s / 3600}h";
		if (s < 604800) return $"{s / 86400}d";
		if (s < 2629800) return $"{s / 604800}w";
		if (s < 31557600) return $"{s / 2629800}mo";
		return $"{s / 31557600}y";
	}

	public void _stats()
	{
		SettingsOperator.ResetScore();

		SettingsOperator.Gameplaycfg.Max = Info.MAX;
		SettingsOperator.Gameplaycfg.Great = Info.GOOD;
		SettingsOperator.Gameplaycfg.Meh = Info.MEH;
		SettingsOperator.Gameplaycfg.Bad = Info.BAD;
		SettingsOperator.Gameplaycfg.Score = Info.score;
		SettingsOperator.Gameplaycfg.MaxCombo = Info.combo;
		SettingsOperator.Gameplaycfg.pp = Info.points;
		SettingsOperator.Gameplaycfg.Username = Info.username;
		SettingsOperator.Gameplaycfg.EpochTime = Info.time;
		SettingsOperator.Gameplaycfg.Rank = Rank;
		SettingsOperator.Gameplaycfg.Accuracy = Gameplay.ReloadAccuracy(SettingsOperator.Gameplaycfg.Max, SettingsOperator.Gameplaycfg.Great, SettingsOperator.Gameplaycfg.Meh, SettingsOperator.Gameplaycfg.Bad);
		if (!string.IsNullOrEmpty(Info.FilePath))
		{
			Replay.FilePath = Info.FilePath;
		}
		// Online score: its replay can be downloaded from the results screen.
		Replay.OnlineEntry = string.IsNullOrEmpty(Info.FilePath) && Info.replay && Info.id > 0 ? Info : null;
		ModsOperator.SetMods(Info.mods);
		AudioPlayer.Instance.PitchScale = Info.speed_multi;
		GetNode<SceneTransition>("/root/Transition").Switch("res://Panels/Screens/ResultScreenv2.tscn");
	}

	public override void _Process(double _delta)
	{
		if (Info == null) return;
		var want = Info.ProfilePicture ?? _guest;
		if (_picture.Texture != want) _picture.Texture = want;
		long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
		if (now != _shownAgo)
		{
			_shownAgo = now;
			_ago.Text = Ago(Info.time);
		}
	}
}
