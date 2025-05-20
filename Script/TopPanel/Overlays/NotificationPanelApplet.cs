using Godot;
using System;

public partial class NotificationPanelApplet : Button
{
	private void _pressed(){
        GD.Print(GetMeta("id"));
		NotificationListener.NotificationList.RemoveAt((int)GetMeta("id"));

        QueueFree();
    }
}
