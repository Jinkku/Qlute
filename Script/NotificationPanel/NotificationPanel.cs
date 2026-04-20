using Godot;
using System;

public partial class NotificationPanel : ColorRect
{
	private Button ClearAll { get; set; }
	private VBoxContainer NotificationCards { get; set; }
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		NotificationCards = GetNode<VBoxContainer>("MarginContainer/ScrollContainer/VBoxContainer");
		for (int i = NotificationListener.NotificationList.Count - 1; i >= 0; i--)
		{
			var NotiInfo = NotificationListener.NotificationList[i];
			if (NotiInfo.ShowProgress == false)
			{
				var NotiCard = GD.Load<PackedScene>("res://Panels/Overlays/Notification.tscn").Instantiate().GetNode<Button>(".");
				NotiCard.SetMeta("is_popup", false);
				NotiCard.SetMeta("listid", Math.Max(0, (int)NotiInfo.id));
				NotificationCards.AddChild(NotiCard);
				NotiCard.Text = NotiInfo.Title;
			}
		}
		ClearAll = GetNode<Button>("MarginContainer/ScrollContainer/VBoxContainer/ClearAll Button");
		ClearAll.Disabled = true;
	}

	private void _ClearAll()
	{
		var tick = 0;
		NotificationListener.NotificationList.Clear();
		NotificationListener.NotificationCards.Clear();
		foreach (var Node in NotificationCards.GetChildren())
		{
			if (tick > 1)
			{
				Node.QueueFree();
			}
			tick++;
		}
	}
	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		ClearAll.Disabled = NotificationListener.NotificationList.Count < 1;
		Size = new Vector2(Size.X, GetViewportRect().Size.Y - (GetNode<ColorRect>("..").Position.Y + 50));
	}
}
