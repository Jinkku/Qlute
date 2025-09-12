using Godot;
using System;

public partial class ButtonBounce : Button
{
	[Export]
	public string Text { get; set; } = "Dummy Button";
	[Export]
	public Texture2D Texture { get; set; } = null;

	private TextureRect TextureNode { get; set; }
	private Label TextNode { get; set; }
	private Tween tween { get; set; }

	private float vsize = 95;
	public override void _Ready()
	{
		vsize = Size.Y;
		if (HasMeta("home") && (bool)GetMeta("home") == true)
		{
			TextureNode = GetNode<TextureRect>("Label/Pic");
			TextNode = GetNode<Label>("Label");
			TextNode.Text = Text;
			TextureNode.Texture = Texture;
		}
	}
    private void _focus()
	{
		Sample.PlaySample("res://SelectableSkins/Slia/Sounds/hover.wav");
		tween?.Kill();
		tween = GetTree().CreateTween();
		tween.TweenProperty(this, "custom_minimum_size", new Vector2(180, vsize), 0.2f)
			.SetTrans(Tween.TransitionType.Bounce)
			.SetEase(Tween.EaseType.Out);
		tween.Play();
	}
	private void _down()
	{
		Sample.PlaySample("res://SelectableSkins/Slia/Sounds/hover.wav");
		tween?.Kill();
		tween = GetTree().CreateTween();
		tween.TweenProperty(this, "custom_minimum_size", new Vector2(154, vsize), 0.2f)
			.SetTrans(Tween.TransitionType.Bounce)
			.SetEase(Tween.EaseType.Out);
		tween.Play();
	}

	private void _up()
	{
		Sample.PlaySample("res://SelectableSkins/Slia/Sounds/selected.wav");
		tween?.Kill();
		tween = GetTree().CreateTween();
		tween.TweenProperty(this, "custom_minimum_size", new Vector2(180, vsize), 0.2f)
			.SetTrans(Tween.TransitionType.Bounce)
			.SetEase(Tween.EaseType.Out);
		tween.Play();
	}
    private void _unfocus()
	{
		tween?.Kill();
		tween = GetTree().CreateTween();
		tween.TweenProperty(this, "custom_minimum_size", new Vector2(154, vsize), 0.2f)
			.SetTrans(Tween.TransitionType.Bounce)
			.SetEase(Tween.EaseType.Out);
		tween.Play();
	}
}
