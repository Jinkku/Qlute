using Godot;
using System;

public partial class UserCard : PanelContainer
{
    private Label Username { get; set; }
    private Label Rank { get; set; }
    private Label Performance { get; set; }
    [Export]
    public bool PauseRealTime { get; set; } = false;
    public override void _Ready()
    {
        Username = GetNode<Label>("SplitV/TopPart/Sections/Username");
        Rank = GetNode<Label>("SplitV/BottomPart/Sections/Rank/Value");
        Performance = GetNode<Label>("SplitV/BottomPart/Sections/Performance/Value");
    }

    public override void _Process(double delta)
    {
        Username.Text = ApiOperator.Username;
        if (!PauseRealTime)
        {
            Rank.Text = "#" + ((int)SettingsOperator.Sessioncfg["ranknumber"]).ToString("N0");
            Performance.Text = $"{SettingsOperator.ranked_points.ToString("N0")}pp";
        }
    }
}
