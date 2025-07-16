using Godot;
using System;

public partial class DtCustom : VBoxContainer
{
	private Label DTLabel;
	private HSlider DTMS;
	public static float Speed;
	private bool oldvis;
	public override void _Ready()
	{
		DTMS = GetNode<HSlider>("DTMS");
		DTLabel = GetNode<Label>("DTM");
	}
	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double _delta)
	{
		Visible = ModsOperator.Mods["dt"];
		if (oldvis != Visible)
		{
			oldvis = Visible;
			Speed = AudioPlayer.Instance.PitchScale;
			DTMS.Value = Speed;
		}
		if (Visible)
		{
			DTLabel.Text = $"DT Speed {Speed}x";
			AudioPlayer.Instance.PitchScale = Speed;
		}
	}
	private void _valuechanged(float value)
	{
		Speed = value;
	}
}
