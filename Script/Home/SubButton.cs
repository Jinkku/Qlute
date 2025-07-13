using Godot;
using System;
using System.Collections.Generic;

public partial class SubButton : PanelContainer
{
    private int _buttonID;
    private PanelContainer MainmenuBar;
    private Tween _tween { get; set; }
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

        Mainmenupos = new Vector2(MainmenuBar.Size.X / 2 - (Size.X / 2), MainmenuBar.Position.Y + MainmenuBar.Size.Y);
        if (_tween != null && !_tween.IsRunning())
            Position = Mainmenupos;


        if (HomeButtonID.ID != -1)
        {
            Visible = true;
            _tween?.Kill();
            _tween = CreateTween();
            _tween.TweenProperty(this, "position", Mainmenupos, 0.2f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
            _tween.Play();
        }
        else
        {
            _tween?.Kill();
            _tween = CreateTween();
            _tween.TweenProperty(this, "position", new Vector2(Mainmenupos.X, Mainmenupos.Y - Size.Y), 0.2f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
            _tween.TweenCallback(Callable.From(() => Visible = false));
        }
        if (HomeButtonID.ID != _buttonID)
        {

            _buttonID = HomeButtonID.ID;
            foreach (Button button in GetNode<HBoxContainer>("SubButtons").GetChildren())
            {
                if (button.HasMeta("ID") && (int)button.GetMeta("ID") == HomeButtonID.ID)
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
