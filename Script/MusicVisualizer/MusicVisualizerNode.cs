using Godot;
using System;

public partial class MusicVisualizerNode : Control
{
	public static Vector3 Locations = new Vector3(0f, 0f, 0f);
	private MusicVisualizer BassNode { get; set; }
	private MusicVisualizer LMidNode { get; set; }
	private MusicVisualizer MidNode { get; set; }
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		BassNode = GetNode<MusicVisualizer>("MusicVisualizerBass");
		LMidNode = GetNode<MusicVisualizer>("MusicVisualizerLMid");
		MidNode = GetNode<MusicVisualizer>("MusicVisualizerMid");
		if (Locations != new Vector3(0, 0, 0))
		{
			BassNode.Rotation = Locations.X;
			LMidNode.Rotation = Locations.Y;
			MidNode.Rotation = Locations.Z;
		}
	}

	public override void _ExitTree()
	{
		Locations.X = BassNode.Rotation;
		Locations.Y = LMidNode.Rotation;
		Locations.Z = MidNode.Rotation;
	}
}
