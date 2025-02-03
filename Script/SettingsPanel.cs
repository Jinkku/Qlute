using Godot;
using System;

public partial class SettingsPanel : Control
{
	// Called when the node enters the scene tree for the first time.
	public string rescfg {get; set;}
	public static SettingsOperator SettingsOperator { get; set; }
	public override void _Ready(){
		SettingsOperator = GetNode<SettingsOperator>("/root/SettingsOperator");
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}
	private void _changed_resolution(int index)
	{
		GD.Print(string.Format("Resolution changed to {0}", index));
		if (index == 0){
			DisplayServer.WindowSetMode(DisplayServer.WindowMode.ExclusiveFullscreen);
		} else if (index == 1){
			DisplayServer.WindowSetMode(DisplayServer.WindowMode.Fullscreen);
		} else if (index == 2){
			DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
		}
	}
}
