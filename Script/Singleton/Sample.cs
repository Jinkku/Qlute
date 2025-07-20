using Godot;
using System;

public partial class Sample : AudioStreamPlayer
{
  public static AudioStreamPlayer Instance;
  public override void _Ready()
  {
    Instance = this;
    Bus = "Effects";
  }
  public static void PlaySample(string path)
  {
    if (Instance == null)
    {
      GD.PrintErr("Sample instance is not initialized.");
      return;
    }
    
    var audioStream = ResourceLoader.Load<AudioStream>(path);
    if (audioStream == null)
    {
      GD.PrintErr("Failed to load audio stream from path: " + path);
      return;
    }
    if (Instance.Playing)
    {
      Instance.Stop();
    }
    Instance.Stream = audioStream;
    Instance.Play();
  }
}
