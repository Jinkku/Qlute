using Godot;
using System;

public partial class FileMenu : PanelContainer
{
    private void _new()
    {
    }

    private void _open()
    {
        GetNode<SceneTransition>("/root/Transition").Switch("res://Panels/Screens/song_select.tscn");
        SettingsOperator.CreateSelectingBeatmap = true;
    }
}
