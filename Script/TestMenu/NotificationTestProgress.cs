using Godot;
using System;

public partial class NotificationTestProgress : Control
{
	private HSlider Slider { get; set; }
	private void _Notify()
	{
		Notify.Post("AGHHHHH");
	}
	private void _Start()
	{
		Notify.Post("Importing foods....", ProgressGetter: () => (int)Slider.Value, Max: () => 100);
	}
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		Slider = GetNode<HSlider>("HSlider");
	}

}
