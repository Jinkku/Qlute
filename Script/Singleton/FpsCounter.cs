using Godot;
using System;
using System.Threading;

public partial class FpsCounter : PanelContainer
{
	// Called when the node enters the scene tree for the first time.
	public float wait = 0;
	Label Latency { get; set; }
	Label FPS { get; set; }
	public double deltam {get; set;}
	public static SettingsOperator SettingsOperator { get; set; }
	public PanelContainer RamUsage { get; set; }
	public override void _Ready(){
		SettingsOperator = GetNode<SettingsOperator>("/root/SettingsOperator");
		FPS = GetNode<Label>("./FPS");
		Latency = GetNode<Label>("./Latency");
		_refresh();
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public void _refresh(){
		var lat = deltam/0.001;
		FPS.Text =(1/deltam).ToString("0")+ "fps\n ";
		Latency.Text = " \n" + lat.ToString("0.00")+ "ms";

	}
	private void _on_mouse_entered(){
		RamUsage = GD.Load<PackedScene>("res://Panels/Overlays/ramusage.tscn").Instantiate().GetNode<PanelContainer>(".");
		GetTree().CurrentScene.AddChild(RamUsage);

	}private void _on_mouse_exited(){
		RamUsage.QueueFree();
	}
	public override void _Process(double delta)
	{
		deltam = delta;
	}
}
