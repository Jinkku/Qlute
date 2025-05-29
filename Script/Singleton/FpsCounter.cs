using Godot;
using System;
using System.Collections.Generic;
using System.Threading;

public partial class FpsCounter : PanelContainer
{
	// Called when the node enters the scene tree for the first time.
	public float wait = 0;
	Label Latency { get; set; }
	Label FPS { get; set; }
	List<double> LatencyL { get; set; }
	public double deltam {get; set;}
	public static SettingsOperator SettingsOperator { get; set; }
	public PanelContainer RamUsage { get; set; }
	public override void _Ready(){
		SettingsOperator = GetNode<SettingsOperator>("/root/SettingsOperator");
		FPS = GetNode<Label>("./FPS");
		Latency = GetNode<Label>("./Latency");
		LatencyL = new List<double>();
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public void _refresh(){
		var lat = deltam/0.001;
		if (LatencyL.Count > 1000)
		{
			LatencyL.RemoveAt(0);
		}
		else
		{
			LatencyL.Add(lat);
		}
		var sum = 0.0;
		foreach (var l in LatencyL)
		{
			sum += l;
		}
		lat = sum / LatencyL.Count;
		if (lat < 0.001) lat = 0.001; // Prevent division by zero or too low values
		FPS.Text = (1 / (lat * 0.001)).ToString("0")+ "fps\n ";
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
		Visible = Check.CheckBoolValue(SettingsOperator.GetSetting("showfps").ToString());
		_refresh();
	}
}
