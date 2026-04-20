using Godot;
using System;
using System.Collections.Generic;

public partial class SubButton : PanelContainer
{
    private int _buttonID;
    private PanelContainer MainmenuBar;
    private Tween _tween { get; set; }
    private float Duration = 0.2f;
    private bool StartMoveDynamic { get; set; } = false;
    private List<Button> Buttons = new List<Button>();
    public override void _Ready()
    {
        Visible = false;
        MainmenuBar = GetNode<PanelContainer>("../HomeButtonBG");
    }

    private Vector2 Mainmenupos { get; set; }
    public override void _Process(double _delta)
    {
        Mainmenupos = new Vector2((MainmenuBar.Size.X / 2) - (Size.X / 2), MainmenuBar.Position.Y + MainmenuBar.Size.Y);
        if (_tween != null && !_tween.IsRunning())
            Position = Mainmenupos;


        if (HomeButtonID.ID != -1)
        {
            _tween?.Kill();
            _tween = CreateTween();
            _tween.TweenCallback(Callable.From(() => Visible = true));
            _tween.TweenProperty(this, "modulate:a", 0f, 0f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
            _tween.SetParallel(true);
            _tween.TweenProperty(this, "position", Mainmenupos, Duration).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
            _tween.TweenProperty(this, "modulate:a", 1f, Duration).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
            _tween.Play();
        }
        else
        {
            _tween?.Kill();
            _tween = CreateTween();
            _tween.TweenProperty(this, "modulate:a", 0f, 1f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
            _tween.SetParallel(true);
            _tween.TweenProperty(this, "position", new Vector2(Mainmenupos.X, Mainmenupos.Y - Size.Y), Duration).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
            _tween.TweenProperty(this, "modulate:a", 0f, Duration).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
            _tween.TweenCallback(Callable.From(() => Visible = false)).SetDelay(Duration);
        }
        if (HomeButtonID.ID != _buttonID)
        {
            _buttonID = HomeButtonID.ID;
            foreach (ButtonBounce button in GetNode<HBoxContainer>("SubButtons").GetChildren())
            {
                if (button.ButtonID == HomeButtonID.ID)
                {
                    button.Visible = true;
                }
                else
                {
                    button.Visible = false;
                }
            }
        }
    }
}
