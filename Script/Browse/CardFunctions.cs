using Godot;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

public partial class CardFunctions : Button
{
    public  SettingsOperator SettingsOperator { get; set; }
    private Button Download { get; set; }
    private Button GoShortcut { get; set; }
    private PanelContainer Existant { get; set; }
    public Label Title { get; set; }
    public Label Artist { get; set; }
    public Label Mapper { get; set; }
    public Label Release { get; set; }
    public PanelContainer RankColour { get; set; }
    public Label RankText { get; set; }
    public Label LvStart { get; set; }
    public Label LvEnd { get; set; }
    public int BeatmapID { get; set; }
    public int Index { get; set; }
    private int ID { get; set; }
    [Export] public bool Downloaded { get; set; } = false;
    [Export] public string BannerPicture { get; set; }
    private TextureRect _backgroundPreview;
    private bool _isVisible = false;
    public override void _Ready()
    {
        if (this != null)
        {
            var StartAnimation = CreateTween();
            StartAnimation.TweenProperty(this, "modulate", new Color(1, 1, 1, 1), 1f)
                .SetTrans(Tween.TransitionType.Cubic)
                .SetEase(Tween.EaseType.Out);
            StartAnimation.Play();
            SettingsOperator = GetNode<SettingsOperator>("/root/SettingsOperator");
            Title = GetNode<Label>("Info/SongTitle");
            Artist = GetNode<Label>("Info/SongArtist");
            Mapper = GetNode<Label>("Info/SongMapper");
            Release = GetNode<Label>("Info/SongReleaseDate");
            RankColour = GetNode<PanelContainer>("InfoBar-Base/InfoBar-Space/InfoBar/RankColor");
            RankText = GetNode<Label>("InfoBar-Base/InfoBar-Space/InfoBar/RankColor/RankText");
            LvStart = GetNode<Label>("InfoBar-Base/InfoBar-Space/InfoBar/LvStartColor/LvStartText");
            LvEnd = GetNode<Label>("InfoBar-Base/InfoBar-Space/InfoBar/LvEndColor/LvEndText");
            Existant = GetNode<PanelContainer>("Existed");
            GoShortcut = GetNode<Button>("DownloadBar/VBoxContainer/Play");
            Download = GetNode<Button>("DownloadBar/VBoxContainer/Download");
            Release.Text = $"submitted at {Global.GetFormatTime(Browse.BrowseCatalog[Index].last_updated)}";

            Existance();
            
            _backgroundPreview = GetNode<TextureRect>("SongBackgroundPreview/BackgroundPreview");
            SelfModulate = Idlecolour;
        }
    }
    private bool IsOnScreen()
    {
        var viewport = GetViewport().GetVisibleRect();
        var rect = GetGlobalRect();
        return viewport.Intersects(rect);
    }
    private async Task LoadBanner()
    {
        if (BannerPicture != null)
            await ApiOperator.DownloadImage(BannerPicture, (ImageTexture texture) =>
            {
                // Guard: node might have gone off-screen while awaiting
                if (!_isVisible)
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
            _ = LoadBanner();
        }
        else if (!onScreen && _isVisible)
        {
            _isVisible = false;
            UnloadBanner();
        }
    }

    /// <summary>
    /// Checks if it's already downloaded.
    /// </summary>
    private void Existance()
    {
        var beatmap = SettingsOperator.Beatmaps.FirstOrDefault(b => b.BeatmapSetID == BeatmapID);
        Downloaded = beatmap != null;
        if (Downloaded)
        {
            ID = beatmap.ID;
            Download.Visible = false;
            Existant.Visible = true;
            GoShortcut.Visible = true;
        }
        else
        {
            Download.Visible = true;
            Existant.Visible = false;
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

    private Color Idlecolour = new Color(0.20f, 0.20f, 0.20f, 0.5f); // Colour when idle
    private Color Focuscolour = new Color(0.5f, 0.5f, 0.5f, 1); // Colour when focused

    // Use focus for animation for the card
    private  Tween _focus_animation;
    private void AnimationButton(Color colour)
    {
        if (_focus_animation != null)
        {
            _focus_animation.Kill();
        }
        _focus_animation = CreateTween();
        _focus_animation.TweenProperty(this, "self_modulate", colour, 0.2f)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.Out);
        _focus_animation.Play();
    }
    private void _focus()
    {
        AnimationButton(Focuscolour);
    }
    private void _unfocus()
    {
        AnimationButton(Idlecolour);
    }

    // To make button not open the overlay when clicked
    private bool ButtonPress = false;
    private void _buttoncard()
    {
        ButtonPress = true;
    }

    private void _buttoncardup()
    {
        ButtonPress = false;
    }

    private void _on_pressed()
    {
        if (!ButtonPress)
        {
            var Animation = CreateTween();
            var BeatmapInfoCard = GD.Load<PackedScene>("res://Panels/BrowseElements/BeatmapInfo.tscn").Instantiate().GetNode<BeatmapInfo>(".");
            BeatmapInfoCard.Position = new Vector2(0, GetViewportRect().Size.Y);
            BeatmapInfoCard.GetNode<TextureRect>("Pill/Poster").Texture = GetNode<TextureRect>("SongBackgroundPreview/BackgroundPreview").Texture;
            BeatmapInfoCard.Index = Index;
            BeatmapInfoCard.BeatmapID = BeatmapID;
            var Back = BeatmapInfoCard.GetNode<Button>("Back");
            Back.Position = new Vector2(0, Back.Position.Y);
            var Blank = new ColorRect();
            Blank.Color = new Color(0, 0, 0, 0.5f);
            Blank.AnchorLeft = 0;
            Blank.AnchorTop = 0;
            Blank.AnchorRight = 1;
            Blank.AnchorBottom = 1;
            Blank.Modulate = new Color(0, 0, 0, 0f);
            Blank.Name = "Blank-Chan";
            GetTree().CurrentScene.AddChild(Blank);
            GetTree().CurrentScene.AddChild(BeatmapInfoCard);
            Animation.SetParallel(true);
            Animation.TweenProperty(Blank, "modulate", new Color(1f, 1f, 1f, 1f), 0.5f)
                .SetTrans(Tween.TransitionType.Cubic)
                .SetEase(Tween.EaseType.Out);
            Animation.TweenProperty(BeatmapInfoCard, "position", new Vector2(0, 0), 0.5f)
                .SetTrans(Tween.TransitionType.Cubic)
                .SetEase(Tween.EaseType.Out);
            Animation.Play();
        }
    }
}
