using Godot;
using System;

public partial class LeaderboardRank : Label
{
    public override void _Ready()
    {
        // Initialize the rank label with a default value
        if (GetNode<Button>("../../").HasMeta("rank"))
        {
            UpdateRank((int)GetNode<Button>("../../").GetMeta("rank"));
        }
    }
    public override void _Process(double delta)
    {
        // This method can be used for any real-time updates if needed
        // For example, you could animate the rank label or update it based on game events
    }
    public void UpdateRank(int rank)
    {
        // Update the text of the label to reflect the new rank
        Text = $"#{rank}";

        // Optionally, you can change the color based on the rank
        if (rank == 1)
        {
            SelfModulate = new Color(1, 0.84f, 0); // Gold for first place
        }
        else if (rank == 2)
        {
            SelfModulate = new Color(0.75f, 0.75f, 0.75f); // Silver for second place
        }
        else if (rank == 3)
        {
            SelfModulate = new Color(0.8f, 0.52f, 0.25f); // Bronze for third place
        }
        else
        {
            SelfModulate = new Color(1, 1, 1); // Default color for other ranks
        }
    }
}
