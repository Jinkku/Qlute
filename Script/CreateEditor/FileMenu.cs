using Godot;
using System;

public partial class FileMenu : PanelContainer
{
    private void _new(){
        Window vum = new Window();
        AddChild(vum);
        vum.Size = new Vector2I(100,100);
    }
}
