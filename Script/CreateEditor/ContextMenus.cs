using Godot;
using System;

public partial class ContextMenus : Button
{
    public PanelContainer ContextMenu {get;set;}
    public override void _Ready()
    {
        ContextMenu = GetNode<PanelContainer>("ContextMenu");
    }
    private void _pressed(){
        ContextMenu.Visible = !ContextMenu.Visible;
        GD.Print("Clicked");
    }
}
