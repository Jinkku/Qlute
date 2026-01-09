using Godot;
using System;

public partial class UserCard : PanelContainer
{
    private Label Username { get; set; }
    private Label Rank { get; set; }
    private Label Performance { get; set; }
    private TextureRect ProfilePicture { get; set; }
    [Export]
    public bool PauseRealTime { get; set; } = false;
    public override void _Ready()
    {
        ProfilePicture = GetNode<TextureRect>("SplitV/TopPart/Sections/RoundPicture/ProfilePicture");
        Username = GetNode<Label>("SplitV/TopPart/Sections/Username");
        Rank = GetNode<Label>("SplitV/BottomPart/Sections/Rank/Value");
        Performance = GetNode<Label>("SplitV/BottomPart/Sections/Performance/Value");
    }

    public override void _Process(double delta)
    {
        if (ProfilePicture.Texture != ApiOperator.PictureData)
        {
            ProfilePicture.Texture = ApiOperator.PictureData;
        }
        Username.Text = ApiOperator.Username;
        if (!PauseRealTime)
        {
            Rank.Text = "#" + (SettingsOperator.Rank).ToString("N0");
            Performance.Text = $"{SettingsOperator.ranked_points.ToString("N0")}pp";
        }
    }
}
