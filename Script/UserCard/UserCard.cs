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
        // Clicking your own card opens your profile. Its inner panels (PanelContainers stop the mouse by default)
        // must let clicks through, or they never reach the card.
        MouseFilter = MouseFilterEnum.Stop;
        MouseDefaultCursorShape = CursorShape.PointingHand;
        TooltipText = "Open your profile";
        IgnoreMouse(this);
    }

    private static void IgnoreMouse(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            if (child is Control c) c.MouseFilter = MouseFilterEnum.Ignore;
            IgnoreMouse(child);
        }
    }

    public override void _GuiInput(InputEvent e)
    {
        if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false } && SettingsOperator.SessionConfig.Loggedin)
        {
            PlayerOverlays.OpenProfile(ApiOperator.Username);
            AccountButton.Instance?.CloseAccountPanel(); // the account dropdown it sits in
            AcceptEvent();
        }
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
