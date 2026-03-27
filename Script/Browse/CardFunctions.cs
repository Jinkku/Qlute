using Godot;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

public partial class CardFunctions : Button
{
	public SettingsOperator SettingsOperator { get; set; } = null!;
	private Button Download { get; set; } = null!;
	private Button GoShortcut { get; set; } = null!;
	private PanelContainer Existant { get; set; } = null!;
	public Label Title { get; set; } = null!;
	public Label Artist { get; set; } = null!;
	public Label Mapper { get; set; } = null!;
	public Label Release { get; set; } = null!;
	public PanelContainer RankColour { get; set; } = null!;
	public Label RankText { get; set; } = null!;
	public Label LvStart { get; set; } = null!;
	public Label LvEnd { get; set; } = null!;
	public int BeatmapID { get; set; }
	public int Index { get; set; }
	private int ID { get; set; }
	[Export] public bool Downloaded { get; set; } = false;
	[Export] public string? BannerPicture { get; set; }
	private TextureRect _backgroundPreview = null!;
	private bool _isVisible = false;

	private readonly Color Idlecolour  = new(0.20f, 0.20f, 0.20f, 0.5f);
	private readonly Color Focuscolour = new(0.5f,  0.5f,  0.5f,  1f);
	private Tween? _focus_animation;

	// Prevents the card press from opening the overlay when a child button is clicked
	private bool ButtonPress = false;

	public override void _Ready()
	{
		var startAnimation = CreateTween();
		startAnimation.TweenProperty(this, "modulate", new Color(1, 1, 1, 1), 1f)
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.Out);
		startAnimation.Play();

		SettingsOperator = GetNode<SettingsOperator>("/root/SettingsOperator");
		Title      = GetNode<Label>("Info/SongTitle");
		Artist     = GetNode<Label>("Info/SongArtist");
		Mapper     = GetNode<Label>("Info/SongMapper");
		Release    = GetNode<Label>("Info/SongReleaseDate");
		RankColour = GetNode<PanelContainer>("InfoBar-Base/InfoBar-Space/InfoBar/RankColor");
		RankText   = GetNode<Label>("InfoBar-Base/InfoBar-Space/InfoBar/RankColor/RankText");
		LvStart    = GetNode<Label>("InfoBar-Base/InfoBar-Space/InfoBar/LvStartColor/LvStartText");
		LvEnd      = GetNode<Label>("InfoBar-Base/InfoBar-Space/InfoBar/LvEndColor/LvEndText");
		Existant   = GetNode<PanelContainer>("Existed");
		GoShortcut = GetNode<Button>("DownloadBar/VBoxContainer/Play");
		Download   = GetNode<Button>("DownloadBar/VBoxContainer/Download");

		Release.Text = $"submitted at {Global.GetFormatTime(Browse.BrowseCatalog[Index].last_updated)}";

		Existance();

		_backgroundPreview = GetNode<TextureRect>("SongBackgroundPreview/BackgroundPreview");
		SelfModulate = Idlecolour;
	}

	private bool IsOnScreen()
	{
		var viewport = GetViewport().GetVisibleRect();
		var rect = GetGlobalRect();
		return viewport.Intersects(rect);
	}

	private async Task LoadBanner()
	{
		if (BannerPicture == null) return;

		await ApiOperator.DownloadImage(BannerPicture, (ImageTexture texture) =>
		{
			// Node may have scrolled off-screen while we were awaiting
			if (!_isVisible || !IsInstanceValid(this))
			{
				texture.Dispose();
				return;
			}
			_backgroundPreview.Texture = texture;
		});
	}

	private void UnloadBanner()
	{
		if (_backgroundPreview.Texture is ImageTexture old)
			old.Dispose();

		_backgroundPreview.Texture = null;
	}

	public override void _PhysicsProcess(double delta)
	{
		Existance();
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

	/// <summary>Checks if the beatmap is already downloaded and updates the UI accordingly.</summary>
	private void Existance()
	{
		var beatmap = SettingsOperator.Beatmaps.FirstOrDefault(b => b.BeatmapSetID == BeatmapID);
		Downloaded = beatmap != null;

		if (Downloaded && beatmap != null)
		{
			ID = beatmap.ID;
			Download.Visible   = false;
			Existant.Visible   = true;
			GoShortcut.Visible = true;
		}
		else
		{
			Download.Visible   = true;
			Existant.Visible   = false;
			GoShortcut.Visible = false;
		}
	}

	private void _play()
	{
		SettingsOperator.SelectSongID(ID);
		GetNode<SceneTransition>("/root/Transition").Switch("res://Panels/Screens/song_select.tscn");
	}

	private void _download()
	{
		ApiOperator.DownloadBeatmap(BeatmapID, Index);
	}

	private void AnimationButton(Color colour)
	{
		_focus_animation?.Kill();
		_focus_animation = CreateTween();
		_focus_animation.TweenProperty(this, "self_modulate", colour, 0.2f)
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.Out);
		_focus_animation.Play();
	}

	private void _focus()   => AnimationButton(Focuscolour);
	private void _unfocus() => AnimationButton(Idlecolour);

	private void _buttoncard()    => ButtonPress = true;
	private void _buttoncardup()  => ButtonPress = false;

	private void _on_pressed()
	{
		if (ButtonPress) return;

		var animation   = CreateTween();
		var infoCard    = GD.Load<PackedScene>("res://Panels/BrowseElements/BeatmapInfo.tscn")
			.Instantiate()
			.GetNode<BeatmapInfo>(".");

		infoCard.Position = new Vector2(0, GetViewportRect().Size.Y);
		infoCard.GetNode<TextureRect>("Pill/Poster").Texture =
			GetNode<TextureRect>("SongBackgroundPreview/BackgroundPreview").Texture;
		infoCard.Index     = Index;
		infoCard.BeatmapID = BeatmapID;

		var back = infoCard.GetNode<Button>("Back");
		back.Position = new Vector2(0, back.Position.Y);

		var blank = new ColorRect
		{
			Color        = new Color(0, 0, 0, 0.5f),
			AnchorLeft   = 0,
			AnchorTop    = 0,
			AnchorRight  = 1,
			AnchorBottom = 1,
			Modulate     = new Color(0, 0, 0, 0f),
			Name         = "Blank-Chan"
		};

		GetTree().CurrentScene.AddChild(blank);
		GetTree().CurrentScene.AddChild(infoCard);

		animation.SetParallel(true);
		animation.TweenProperty(blank,    "modulate", new Color(1f, 1f, 1f, 1f), 0.5f)
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.Out);
		animation.TweenProperty(infoCard, "position",  new Vector2(0, 0),        0.5f)
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.Out);
		animation.Play();
	}
}