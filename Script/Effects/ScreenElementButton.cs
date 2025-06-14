using Godot;
using System;

public partial class ScreenElementButton : Button
{
    private Tween _focus_animation;
	private Color Idlecolour = new Color(0.26f, 0.32f, 0.41f, 1f); // Colour when idle
	private Color Focuscolour = new Color(0.36f, 0.42f, 0.51f, 1f); // Colour when focused
	private Color highlightcolour = new Color(0.56f, 0.62f, 0.71f, 1f); // Colour when highlighted

    public override void _Ready()
    {
        if (HasMeta("focus"))
        {
            Focuscolour = (Color)GetMeta("focus");
        }
        if (HasMeta("unfocus"))
        {
            Idlecolour = (Color)GetMeta("unfocus");
        }
        if (HasMeta("highlight"))
        {
            highlightcolour = (Color)GetMeta("highlight");
        }
        SelfModulate = Idlecolour; // Set the initial color to Idlecolour
    }
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
    }	private void _highlight()
	{
		AnimationButton(highlightcolour);
	}
	private void _focus()
	{
        AnimationButton(Focuscolour);
	}private void _unfocus()
	{
        AnimationButton(Idlecolour);
	}
}
