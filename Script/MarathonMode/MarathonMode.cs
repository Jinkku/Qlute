using Godot;
using System;
using System.Collections.Generic;

public partial class MarathonMode : ColorRect
{
    private Label Duration { get; set; }
    private Label Points { get; set; }
    private Label SongCount { get; set; }
    private VBoxContainer ArtistContainer { get; set; }
    private int DiffCount { get; set; } = 0;
    private void _changedmode(int value)
    {
        DiffCount = value;
        resetcounter();
    }
    private void resetcounter()
    {
        float Dur = 0f;
        double PointsTotal = 0;
        int SongCountTotal = 0;
        int Mini = 0;
        float per = 1f;
        int Max = 3;
        if (DiffCount == 0)
        {
            Mini = 0;
            Max = 3;
        }else if (DiffCount == 1)
        {
            Mini = 4;
            Max = 6;
        }
        else if (DiffCount == 2)
        {
            Mini = 6;
            Max = 12;
        }
        else if (DiffCount == 3)
        {
            Mini = 12;
            Max = 24;
        }
        else if (DiffCount == 4)
        {
            Mini = 24;
            Max = 48;
        }
        foreach (Node child in ArtistContainer.GetChildren()) // Remove all children from the ArtistContainer
        {
            child.QueueFree();
        }


        foreach (BeatmapLegend beatmap in SettingsOperator.Beatmaps)
        {
            if (beatmap.Levelrating >= Mini && beatmap.Levelrating <= Max)
            {
                Dur += beatmap.Timetotal;
                PointsTotal += Math.Max(0, beatmap.pp * per);
                per -= 0.2f; // Decrease the multiplier for each song
                SongCountTotal++;
                Label artistLabel = new Label
                {
                    Text = $"{beatmap.Artist} - {beatmap.Title} [{beatmap.Version}]",
                };
                ArtistContainer.AddChild(artistLabel);
            }
        }
        Duration.Text = "Duration:" + TimeSpan.FromMilliseconds(Dur / AudioPlayer.Instance.PitchScale).ToString(@"hh\:mm\:ss") ?? "00:00:00";
        Points.Text = $"You can earn {PointsTotal.ToString("N0")}pp from this Marathon.";
        SongCount.Text = $"You will play {SongCountTotal.ToString("N0")} songs in this Marathon.";
    }
    public override void _Ready()
    {
        Duration = GetNode<Label>("Panel/HBoxContainer/Settings/SettingsPill/MarathonDuration");
        Points = GetNode<Label>("Panel/HBoxContainer/Settings/SettingsPill/MarathonPoints");
        SongCount = GetNode<Label>("Panel/HBoxContainer/Settings/SettingsPill/MarathonSongCount");
        ArtistContainer = GetNode<VBoxContainer>("Panel/HBoxContainer/Settings/SettingsPill/Settings/SettingsPill/Artist/List");
        resetcounter();

    }
    private void _exit()
    {
        GetNode<SceneTransition>("/root/Transition").Switch("res://Panels/Screens/home_screen.tscn");
    }
    private void _start()
    {
        GetNode<SceneTransition>("/root/Transition").Switch("res://Panels/Screens/home_screen.tscn");
    }
}
