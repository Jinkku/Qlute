using Godot;
using System;
using System.Runtime.CompilerServices;
public partial class AudioPlayer : AudioStreamPlayer
{
	// Called when the node enters the scene tree for the first time.
	public static AudioStreamPlayer Instance;
	
	public override void _Ready()
	{
		Instance = this;
		Instance.Bus = "Master";
	}
	public static AudioStreamMP3 LoadMP3(string path)
	{
		using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
		var sound = new AudioStreamMP3();
		sound.Data = file.GetBuffer((long)file.GetLength());
		return sound;
	}

	public static AudioStreamOggVorbis LoadOGG(string path)
	{
		return null;
	}


	public static AudioStreamWav LoadWAV(string path)
	{
		using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
		var sound = new AudioStreamWav();
		sound.Data = file.GetBuffer((long)file.GetLength());
		return sound;
	}
	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}
}
