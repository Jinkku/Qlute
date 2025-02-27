using Godot;
using System;

public partial class NotificationPanel : ColorRect
{
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		foreach (var NotiInfo in NotificationListener.NotificationList){
    	    var NotiCard = GD.Load<PackedScene>("res://Panels/Overlays/NotificationApplet.tscn").Instantiate().GetNode<Button>(".");
			GetNode<VBoxContainer>("MarginContainer/ScrollContainer/VBoxContainer").AddChild(NotiCard);
			NotiCard.Text = NotiInfo.Title;
		}

	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		Size = new Vector2(Size.X,GetViewportRect().Size.Y-(GetNode<ColorRect>("..").Position.Y+50));
	}
}
