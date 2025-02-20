using Godot;
using System;

public partial class SettingsPanel : Control
{
	// Called when the node enters the scene tree for the first time.
	public static SettingsOperator SettingsOperator { get; set; }
	public static OptionButton Windowmode { get; set; }
	public static HSlider BackgroundDim { get; set; }
	public override void _Ready(){
		SettingsOperator = GetNode<SettingsOperator>("/root/SettingsOperator");
	}
}
