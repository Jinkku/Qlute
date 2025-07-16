using Godot;
using System;

public partial class HtCustom : VBoxContainer
{
	private Label HTLabel;
	public static float Speed;
	private HSlider HTMS;
	private bool oldvis;
	public override void _Ready()
	{
		HTLabel = GetNode<Label>("HTM");
		HTMS = GetNode<HSlider>("HTMS");
	}
	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double _delta)
	{
		Visible = ModsOperator.Mods["ht"];
		if (oldvis != Visible)
		{
			oldvis = Visible;
			Speed = AudioPlayer.Instance.PitchScale;
			HTMS.Value = Speed;
		}
		if (Visible)
		{
			HTLabel.Text = $"HT Speed {Speed}x";
			AudioPlayer.Instance.PitchScale = Speed;
		}
	}
	private void _valuechanged(float value)
	{
		Speed = value;
	}
}
