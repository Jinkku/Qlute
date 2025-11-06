using Godot;
using System;

[Tool]
[GlobalClass]
public partial class DropDownMenu : Button
{
    private Tween _focus_animation;
    private Tween _menuTween;
    private bool Hovered = false;
    private bool _ready = false;

    private Color _idleColour = new Color("#0069ff");
    private string _textback = "Demo";
    private bool _menuToggle;
    
    private bool Flipped { get; set; }
    
    private Label _buttonText;
    private PanelContainer _menu;

    [Export]
    public bool MenuToggle
    {
        get => _menuToggle;
        set
        {
            _menuToggle = value;
            if (_ready) // only animate when ready!
                MenuAnimation(value);
        }
    }

    [Export] public Color FocusColour = new Color("#69a4ff");
    [Export] public Color HighlightColour = new Color("#b3d0fc");

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
                if (_menu != null)
                    _menu.SelfModulate = _idleColour / 1.5f;
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
    
    
    [Signal] // <-- this makes it appear in the editor
    public delegate void ButtonPressedEventHandler(int index);
    
    private Godot.Collections.Array<string> _items { get; set; } = new Godot.Collections.Array<string>();
    
    [Export]
    public Godot.Collections.Array<string> Items 
    {
        get => _items;
        set
        {
            _items = value;
            RefreshMenu();
        }
    }

    private void RefreshMenu()
    {
        if (MenuEntries == null)
            return;

        // Remove old nodes
        foreach (Node child in MenuEntries.GetChildren())
            child.QueueFree();

        // Add new labels for each item
        int index = 0;
        foreach (string item in _items)
        {
            var Button = new Button { Text = item };
            MenuEntries.AddChild(Button);

            int currentIndex = index; // capture the current index
            Button.Pressed += () =>
            {
                EmitSignal(SignalName.ButtonPressed, currentIndex); // use the captured value
                MenuToggle = !MenuToggle;
            };

            index++;
        }
    }

    private void ButtonSignal(int id)
    {
        GD.Print(id);
    }
    private void resize()
    {
        Size = new Vector2(Math.Max(100, _buttonText.Size.X + 20), Math.Max(50, Size.Y));
        CustomMinimumSize = new Vector2(Math.Max(100, _buttonText.Size.X + 20), Math.Max(50, CustomMinimumSize.Y));
    }

    private void Flipchecker()
    {
        if (Position.Y + _menu.Size.Y > GetViewportRect().Size.Y)
        {
            Flipped = true;
            _menu.Position = new Vector2(_menu.Position.X, -_menu.Size.Y);
        }
        else
        {
            Flipped = false;
            _menu.Position = new Vector2(_menu.Position.X, Size.Y);
        }
    }
    
    private VBoxContainer MenuEntries { get; set; }
    
    public override void _Ready()
    {
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
        _buttonText.Theme = GD.Load<Theme>("res://Panels/CustomMenus/blank.tres"); // Sets theme
        _buttonText.Text = StringText;
        
        // Menu Setup
        _menu = GD.Load<PackedScene>("res://Panels/CustomMenus/Options.tscn").Instantiate().GetNode<PanelContainer>(".");
        AddChild(_menu);
        _menu.Position = new Vector2(0, Position.Y - _menu.Size.Y);
        MenuEntries = _menu.GetNode<VBoxContainer>("Entries");
        RefreshMenu();
        
        // Connections
        ButtonDown += _highlight;
        ButtonUp += _unhold;
        Pressed += _toggle;
        MouseEntered += _focus;
        MouseExited += _unfocus;
        ItemRectChanged += Flipchecker;
        Flipchecker();
        
        if (_menu == null)
            GD.PrintErr("DropDownMenu: Menu node not found. Expected child at ./Menu");
        if (_buttonText == null)
            GD.PrintErr("DropDownMenu: ButtonText node not found. Expected child at ./ButtonText");

        SelfModulate = _idleColour;
        if (_menu != null)
            _menu.SelfModulate = _idleColour / 1.5f;

        _ready = true;

        // run initial state after ready
        MenuAnimation(_menuToggle);
    }

    private void _toggle()
    {
        MenuToggle = !MenuToggle;
    }
    
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
    private void MenuAnimation(bool toggle)
    {
        if (_menu == null)
        {
            GD.PrintErr("MenuAnimation called but Menu is null");
            return;
        }

        _menuTween?.Kill();
        _menuTween = CreateTween().SetParallel(true);
        var newsize = Size.Y;
        var takeover = -_menu.Size.Y;
        if (Flipped)
        {
            takeover = Size.Y;
            newsize = -_menu.Size.Y;
        }
        if (toggle)
        {
            _menu.MouseFilter = MouseFilterEnum.Stop;
            _menuTween.TweenProperty(_menu, "modulate", new Color(1, 1, 1, 1), 0.1f);
            _menuTween.TweenProperty(_menu, "position:y", newsize, 0.1f);
        }
        else
        {
            _menu.MouseFilter = MouseFilterEnum.Ignore;
            _menuTween.TweenProperty(_menu, "modulate", new Color(0, 0, 0, 0), 0.1f);
            _menuTween.TweenProperty(_menu, "position:y", takeover, 0.1f);
        }
        _menuTween.Play();
    }
    
}
