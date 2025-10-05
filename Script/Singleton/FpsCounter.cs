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
	private double Laten { get; set; }
	private int Framerate { get; set; }
	List<double> LatencyL { get; set; }
	public double deltam {get; set;}
	private Color Bad = new Color("#ff122a");
	private Color Good = new Color("#12ff26ff");
	private SettingsOperator SettingsOperator { get; set; }
	public PanelContainer RamUsage { get; set; }

	public override void _Ready()
	{
		SettingsOperator = GetNode<SettingsOperator>("/root/SettingsOperator");
		FPS = GetNode<Label>("./FPS");
		Latency = GetNode<Label>("./Latency");
		LatencyL = new List<double>();
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public void _refresh() {
		var lat = deltam / 0.001;
		if (lat < 0.001) lat = 0.001; // Prevent division by zero or too low values
		if (LatencyL.Count > 100)
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
		Framerate = (int)(1 / (lat * 0.001));
		Laten = lat;


		float MaxFPS = Engine.MaxFps;
		if (MaxFPS == 0)
		{
			MaxFPS = 1000;
		}

        double t = Mathf.Clamp((Framerate - 0.01) / (MaxFPS - 0.01), 0f, 1f);

        // Interpolate between red and green
        Color c = Bad.Lerp(Good, (float)t);

		// Change color depending on fps
		FPS.Modulate = c;
		Latency.Modulate = c;


		FPS.Text = $"{Framerate}fps"; // Enters FPS to FPS Label.
		Latency.Text = $"{Laten.ToString("0.00")}ms"; // Enters Latency to Latency Label.
	}
	private void _on_mouse_entered(){
		//RamUsage = GD.Load<PackedScene>("res://Panels/Overlays/ExtendedDetails.tscn").Instantiate().GetNode<PanelContainer>(".");
		//GetTree().CurrentScene.AddChild(RamUsage);
	}private void _on_mouse_exited(){
		//RamUsage.QueueFree();
	}
	public override void _Process(double delta)
	{
		deltam = delta;
		Visible = Check.CheckBoolValue(SettingsOperator.GetSetting("showfps").ToString());
		_refresh();
	}
}
