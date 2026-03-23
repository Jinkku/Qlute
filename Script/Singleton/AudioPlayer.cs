using Godot;
using System;
using System.IO;

public enum AudioFormat { MP3, WAV, OGG }
public partial class AudioPlayer : AudioStreamPlayer
{
    public static AudioStreamPlayer Instance;
    public static AudioStreamPlayer BrowsePreview;
    private bool _isPlaying = false;
    public static bool _isogg = false;
    public static string checksum { get; set; }
    private float _seekPosition = 0.0f;
    public static int PreviewID { get; set; } = 0;

    public override void _Ready()
    {
        Instance = this;
        BrowsePreview = new AudioStreamPlayer();
        BrowsePreview.Finished += OnAudioFinished;
        BrowsePreview.Name = $"Preview";
        AddChild(BrowsePreview);
        Instance.Bus = "Music";
    }

    private void OnAudioFinished()
    {
        if (Stream != null)
        {
            GD.Print("[Qlute] Should continue playing...");
            StreamPaused = false;
        }
    }
    public override void _Process(double delta)
    {
        if (SettingsOperator.loopaudio) AudioLoop();
        BrowsePreview.VolumeDb = VolumeDb;
        SettingsOperator.Gameplaycfg.Time = GetPlaybackPosition();
        SettingsOperator.Gameplaycfg.TimeTotal = (float)(Stream?.GetLength() ?? 0);
    }

    public static AudioStreamMP3 LoadMP3(string path)
    {
        using var file = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read);
        var sound = new AudioStreamMP3();
        _isogg = false;
        sound.Data = file.GetBuffer((long)file.GetLength());
        GD.Print($"Loaded MP3"); 
        return sound;
    }
	public void AudioLoop(){
		if (SettingsOperator.Gameplaycfg.TimeTotal - GetPlaybackPosition() < 0.1 && SettingsOperator.loopaudio)
		{
			AudioPlayer.Instance.Play();
		}
	}
    public static AudioStreamOggVorbis LoadOGG(string path) 
    { 
         GD.Print($"Loaded OGG"); 
         return AudioStreamOggVorbis.LoadFromFile(path);
    }
    public static AudioStreamWav LoadWAV(string path)
    {
        _isogg = false;
        using var file = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read);
        var sound = new AudioStreamWav();
        sound.Data = file.GetBuffer((long)file.GetLength());
        GD.Print($"Loaded WAV");
        return sound;
    }

    public static AudioFormat? GetAudioFormat(string filePath)
    {
        try
        {
            byte[] header = new byte[4];

            using (FileStream fs = new FileStream(filePath, FileMode.Open, System.IO.FileAccess.Read))
            {
                if (fs.Read(header, 0, 4) < 4)
                    return null;
            }

            // WAV: "RIFF"
            if (header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46)
                return AudioFormat.WAV;

            // OGG: "OggS"
            if (header[0] == 0x4F && header[1] == 0x67 && header[2] == 0x67 && header[3] == 0x53)
                return AudioFormat.OGG;

            // MP3: ID3 tag
            if (header[0] == 0x49 && header[1] == 0x44 && header[2] == 0x33)
                return AudioFormat.MP3;

            // MP3: raw MPEG sync bytes
            if (header[0] == 0xFF && (header[1] & 0xE0) == 0xE0)
                return AudioFormat.MP3;

            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
