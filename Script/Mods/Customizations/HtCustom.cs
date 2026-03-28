using Godot;
using System;

public partial class HtCustom : VBoxContainer
{
	private Label HTLabel;
	private HSlider HTMS;
	public static float HTSpeed = 0.5f;
	public static float Speed = HTSpeed;
	private bool oldvis = false;
	public override void _Ready()
	{
		HTMS = GetNode<HSlider>("HTMS");
		HTLabel = GetNode<Label>("HTM");
		HTMS.Value = Speed;
	}
	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double _delta)
	{
		Visible = ModsOperator.Mods["ht"];
		if (oldvis != Visible)
		{
			oldvis = Visible;
			HTMS.Value = Speed;
		}
		if (Visible)
		{
			HTLabel.Text = $"HT Speed {Speed}x";
		}
	}
	private void _valuechanged(float value)
	{
		if (oldvis)
		{
			Speed = value;
			AudioPlayer.Instance.PitchScale = Speed;
		}
	}
}
