using Godot;
using System;
using System.Collections.Generic;
public partial class Sample : Node
{
  public static List<AudioStreamPlayer> Streams = new List<AudioStreamPlayer>();
  public static float VolumeDb { get; set; } = 0;
  public static void PlaySample(string path, float audiopitch = 1)
  {
    var SampleNode = new AudioStreamPlayer();
    SampleNode.Bus = "Effects";
    
    SampleNode.PitchScale = audiopitch;
    
    var audioStream = ResourceLoader.Load<AudioStream>(path);
    if (audioStream == null)
    {
      GD.PrintErr("Failed to load audio stream from path: " + path);
      return;
    }
    SampleNode.Stream = audioStream;
    Streams.Add(SampleNode);
    GD.Print("aga");
  }

  public override void _Process(double delta)
  {
    foreach (var stream in Streams)
    {
      GD.Print("aka");
      AddChild(stream);
      stream.VolumeDb = VolumeDb;
      stream.Play();
      stream.Finished += () => stream.QueueFree();
      Streams.Remove(stream);
    }
  }
}
