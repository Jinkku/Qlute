using Godot;
using System;
using System.Collections.Generic;

public partial class MarathonMode : ColorRect
{
    private Label Duration { get; set; }
    private Label Points { get; set; }
    private Label SongCount { get; set; }
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
        foreach (BeatmapLegend beatmap in SettingsOperator.Beatmaps)
        {
            if (beatmap.Levelrating >= Mini && beatmap.Levelrating <= Max)
            {
                Dur += beatmap.Timetotal;
                PointsTotal += beatmap.pp;
                SongCountTotal++;
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
