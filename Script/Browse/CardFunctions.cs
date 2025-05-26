using Godot;
using System;

public partial class CardFunctions : Button
{
    public async override void _Ready()
    {
        await Browse.DownloadImage(GetMeta("pic").ToString(), (ImageTexture texture) =>
        {
            GetNode<TextureRect>("SongBackgroundPreview/BackgroundPreview").Texture = texture;
        });
        SelfModulate = Idlecolour;
        var StartAnimation = CreateTween();
        StartAnimation.TweenProperty(this, "modulate", new Color(1,1,1,1), 1f)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.Out);
        StartAnimation.Play();

    }
    private Color Idlecolour = new Color(0.20f, 0.20f, 0.20f, 0.5f); // Colour when idle
    private Color Focuscolour = new Color(0.5f, 0.5f, 0.5f, 1); // Colour when focused
    private void _setid()
    {
        if (HasMeta("beatmap"))
        {
            GetNode<Button>("DownloadBar/VBoxContainer/Download").SetMeta("beatmap", GetMeta("beatmap"));
        }
        else
        {
            GD.PrintErr("Meta key 'beatmap' does not exist.");
        }
    }

    // Use focus for animation for the card
    private Tween _focus_animation;
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
            var BeatmapInfoCard = GD.Load<PackedScene>("res://Panels/BrowseElements/BeatmapInfo.tscn").Instantiate().GetNode<Control>(".");
            BeatmapInfoCard.Position = new Vector2(0, GetViewportRect().Size.Y);
            BeatmapInfoCard.GetNode<Label>("Pill/Padding/Info/SongTitle").Text = GetNode<Label>("SongTitle").Text;
            BeatmapInfoCard.GetNode<Label>("Pill/Padding/Info/SongArtist").Text = GetNode<Label>("SongArtist").Text;
            BeatmapInfoCard.GetNode<TextureRect>("Pill/Poster").Texture = GetNode<TextureRect>("SongBackgroundPreview/BackgroundPreview").Texture;
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
