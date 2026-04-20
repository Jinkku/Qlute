using Godot;
using System;

public partial class ButtonBounce : Button
{
	[Export]
#pragma warning disable CS0108 // Member hides inherited member; missing new keyword
    public string Text { get; set; } = "Dummy Button";
#pragma warning restore CS0108 // Member hides inherited member; missing new keyword
	[Export]
	public Texture2D Texture { get; set; } = null;
	[Export]
	public int ButtonID { get; set; } = -1;
	[Export]
	public bool SubButton { get; set; }

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

		if (Texture == null)
		{
			TextureNode.Visible = false;
			TextNode.AnchorLeft = 0.0f;
			TextNode.AnchorTop = 0.0f;
			TextNode.AnchorRight = 1.0f;
			TextNode.AnchorBottom = 1.0f;
			TextNode.OffsetBottom = 0.0f;
			TextNode.OffsetTop = 0.0f;
			TextNode.OffsetLeft = 0.0f;
			TextNode.OffsetRight = 0.0f;
			TextNode.GrowHorizontal = GrowDirection.Both;
			TextNode.GrowVertical = GrowDirection.Both;
			TextNode.VerticalAlignment = VerticalAlignment.Center;
		}
	}
    private void _focus()
	{
		if (!SubButton) HomeButtonID.ID = ButtonID;
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
