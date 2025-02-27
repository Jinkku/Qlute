using Godot;
using System;
using System.IO;
using NVorbis;

public partial class AudioPlayer : AudioStreamPlayer
{
    public static AudioStreamPlayer Instance;
    private static VorbisReader _vorbisReader;
    private Stream _audioStream;
    private bool _isPlaying = false;
    public static bool _isogg = false;
    private float _seekPosition = 0.0f;

    public override void _Ready()
    {
        Instance = this;
        Instance.Bus = "Master";
    }
    public override void _Process(double delta)
    {
        if (SettingsOperator.loopaudio){AudioLoop();}
    }

    public static AudioStreamMP3 LoadMP3(string path)
    {
        using var file = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read);
        var sound = new AudioStreamMP3();
        _isogg = false;
        sound.Data = file.GetBuffer((long)file.GetLength());
        return sound;
    }
	public void AudioLoop(){
		if (SettingsOperator.Gameplaycfg["timetotal"]-(GetPlaybackPosition()/0.001) < -1000 && SettingsOperator.loopaudio)
		{
			AudioPlayer.Instance.Play();
		}
	}
	 public static AudioStreamGenerator LoadOGG(string path)
	 {
	//     // Open the Ogg file using System.IO.File
	//     using (var _audioStream = File.OpenRead(path))
	//     {
	//         // Create a VorbisReader to decode the Ogg file
	//         _vorbisReader = new VorbisReader(_audioStream);

	//         // Create the AudioStreamGenerator
	//         var audioStream = new AudioStreamGenerator();
	//         audioStream.MixRate = _vorbisReader.SampleRate; // Set sample rate from Vorbis file

	//         // Prepare the buffer for PCM data (stereo, two channels)
	//         var frameCount = _vorbisReader.TotalSamples * _vorbisReader.Channels;
	//         var frames = new Vector2[frameCount / 2];

	//         // Read and convert the PCM data (from float to short range)
	//         var data = new float[frameCount];
	//         _vorbisReader.ReadSamples(data, 0, data.Length);

	//         // Process the PCM data and populate the frames array
	//         for (int i = 0, j = 0; i < data.Length; i += 2, j++)
	//         {
	//             // Convert the decoded float PCM data to Vector2 (stereo channels)
	//             frames[j] = new Vector2(data[i] * short.MaxValue, data[i + 1] * short.MaxValue);
	//         }

	//         // Create the playback instance for the AudioStreamGenerator
	//         var playback = audioStream.Playback;
	//         playback.PushBuffer(frames);

	//         // Return the AudioStreamGenerator
	//         return audioStream;}
			return null;
	 }

    public static AudioStreamWav LoadWAV(string path)
    {
        _isogg = false;
        using var file = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read);
        var sound = new AudioStreamWav();
        sound.Data = file.GetBuffer((long)file.GetLength());
        return sound;
    }
}
