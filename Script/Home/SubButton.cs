using Godot;
using System;

public partial class SubButton : PanelContainer
{
    private int _buttonID;
    private PanelContainer MainmenuBar;
    public override void _Ready()
    {
        MainmenuBar = GetNode<PanelContainer>("../HomeButtonBG");
    }
    public override void _Process(double _delta)
    {
        if (HomeButtonID.ID != _buttonID)
        {

            Position = new Vector2(MainmenuBar.Size.X / 2 - (Size.X / 2), MainmenuBar.Position.Y + Size.Y);
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
