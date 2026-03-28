using Godot;
using System;

public partial class DtCustom : VBoxContainer
{
	private Label DTLabel;
	private HSlider DTMS;
	public static float DTSpeed = 1.25f;
	public static float Speed = DTSpeed;
	private bool oldvis = false;
	public override void _Ready()
	{
		DTMS = GetNode<HSlider>("DTMS");
		DTLabel = GetNode<Label>("DTM");
		DTMS.Value = Speed;
	}
	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double _delta)
	{
		Visible = ModsOperator.Mods["dt"];
		if (oldvis != Visible)
		{
			oldvis = Visible;
			DTMS.Value = Speed;
		}
		if (Visible)
		{
			DTLabel.Text = $"DT Speed {Speed}x";
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
