using Godot;
using System;

public partial class NotificationPanelApplet : Button
{
	private void _pressed(){
        QueueFree();
    }
}
