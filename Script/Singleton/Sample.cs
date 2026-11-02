using Godot;
using System;
using System.Collections.Generic;
public partial class Sample : Node
{
  public static float VolumeDb { get; set; } = 0;
  private static Sample Instance;

  // One persistent player per sound, using polyphony instead of creating/freeing a node for every hit.
  private static readonly Dictionary<(string, float), AudioStreamPlayer> Players = new();
  private static readonly List<(string, float)> Pending = new();
  private const int Polyphony = 16;

  public override void _Ready()
  {
    ProcessMode = ProcessModeEnum.Always;
    Instance = this;
    foreach (var (path, pitch) in Pending) Play(path, pitch);
    Pending.Clear();
  }

  public static void PlaySample(string path, float audiopitch = 1)
  {
    if (Instance == null || !Instance.IsInsideTree())
    {
      Pending.Add((path, audiopitch));
      return;
    }
    Play(path, audiopitch);
  }

  private static void Play(string path, float audiopitch)
  {
    if (!Players.TryGetValue((path, audiopitch), out var player) || !IsInstanceValid(player))
    {
      var audioStream = ResourceLoader.Load<AudioStream>(path);
      if (audioStream == null)
      {
        GD.PrintErr("Failed to load audio stream from path: " + path);
        return;
      }
      player = new AudioStreamPlayer
      {
        Bus = "Effects",
        Stream = audioStream,
        PitchScale = audiopitch,
        MaxPolyphony = Polyphony,
        ProcessMode = ProcessModeEnum.Always,
      };
      Instance.AddChild(player);
      Players[(path, audiopitch)] = player;
    }

    float db = VolumeDb;
    if (float.IsNaN(db)) db = 0;
    player.VolumeDb = db;
    player.Play();
  }
}
