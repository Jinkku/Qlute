using Godot;
using System;

[Tool]
[GlobalClass]
public partial class ButtonFade : Button
{
    private string _textback = "Test";
    private bool _ready = false;
    private Color _idleColour = new Color("#373738");
    
    
    [Export] public Color FocusColour = new Color("#1c6de9");
    [Export] public Color HighlightColour = new Color("#6aa5ff");

    [Export]
    public Color IdleColour
    {
	    get => _idleColour;
	    set
	    {
		    _idleColour = value;
		    if (_ready)
		    {
			    SelfModulate = _idleColour;
		    }
	    }
    }
    [Export]
    public string StringText
    {
        get => _textback;
        set
        {
            _textback = value;
            if (_ready && _buttonText != null)
                _buttonText.Text = _textback;
        }
    }
    
    private Label _buttonText { get; set; }

    private float oldsizex = 0;

    private float oldsizey = 0;
    
    private void resize()
    {
	    if (_buttonText.Size.X + 20 > Size.X)
	    {
		    Size = new Vector2(Math.Max(oldsizex, _buttonText.Size.X + 20), Math.Max(oldsizey, _buttonText.Size.Y + 20));
	    }
    }
    private bool Hovered = false;
    private Tween _focus_animation;
    
    private void AnimationButton(Color colour)
    {
	    _focus_animation?.Kill();
	    _focus_animation = CreateTween();
	    _focus_animation.TweenProperty(this, "self_modulate", colour, 0.2f)
		    .SetTrans(Tween.TransitionType.Cubic)
		    .SetEase(Tween.EaseType.Out);
	    _focus_animation.Play();
    }	private void _highlight()
    {
	    AnimationButton(HighlightColour);
    }
    private void _focus()
    {
	    Hovered = true;
	    AnimationButton(FocusColour);
    }private void _unfocus()
    {
	    Hovered = false;
	    AnimationButton(IdleColour);
    }

    private void _unhold()
    {
	    if (Hovered)
	    {
		    AnimationButton(FocusColour);
	    }
	    else
	    {
		    AnimationButton(IdleColour);
	    }
    }
    
    
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		oldsizex = Size.X;
		oldsizey = Size.Y;
		// Sets up Button Label
		_buttonText = new Label();
		AddChild(_buttonText);
		_buttonText.Name = "ButtonText";
		_buttonText.HorizontalAlignment = HorizontalAlignment.Center;
		_buttonText.VerticalAlignment = VerticalAlignment.Center;
		_buttonText.AnchorLeft = 0.5f;
		_buttonText.AnchorRight = 0.5f;
		_buttonText.AnchorTop = 0.5f;
		_buttonText.AnchorBottom = 0.5f;
		_buttonText.GrowHorizontal = GrowDirection.Both;
		_buttonText.GrowVertical = GrowDirection.Both;
		_buttonText.OffsetLeft = 0.5f;
		_buttonText.OffsetRight = 0.5f;
		_buttonText.OffsetTop = 0.5f;
		_buttonText.OffsetBottom = 0.5f;
		_buttonText.Resized += resize;
		_buttonText.Text = StringText;
		_ready = true;
		SelfModulate = _idleColour;
		ButtonDown += _highlight;
		ButtonUp += _unhold;
		MouseEntered += _focus;
		MouseExited += _unfocus;
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}
}
