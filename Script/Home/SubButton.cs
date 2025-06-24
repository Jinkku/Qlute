using Godot;
using System;

public partial class SubButton : PanelContainer
{
    private int _buttonID;
    public override void _Process(double _delta)
    {
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
