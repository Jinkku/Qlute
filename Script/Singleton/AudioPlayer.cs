using Godot;
using System;
using System.IO;
using System.Threading.Tasks;

public enum AudioFormat { MP3, WAV, OGG }
public partial class AudioPlayer : AudioStreamPlayer
{
    public static AudioPlayer Instance;
    public static AudioStreamPlayer BrowsePreview;
    private AudioEffectEQ _eqEffect;
    public static int MasterVol { get; set; } = 80;
    public static int SampleVol { get; set; } = 70;
    private bool _isPlaying = false;
    public static bool _isogg = false;
    public static bool _isLoading = false;
    public static string checksum { get; set; }
    private float _seekPosition = 0.0f;
    public static int PreviewID { get; set; } = 0;

    public override void _Ready()
    {
        int busIdx = AudioServer.GetBusIndex("Music");
        _eqEffect = AudioServer.GetBusEffect(busIdx, 1) as AudioEffectEQ;
        Instance = this;
        BrowsePreview = new AudioStreamPlayer();
        BrowsePreview.Finished += OnAudioFinished;
        BrowsePreview.Name = $"Preview";
        AddChild(BrowsePreview);
        Bus = "Music";
        MasterVol = int.TryParse(SettingsOperator.GetSetting("master").ToString(), out int mtr) ? mtr : 80;
        SampleVol = int.TryParse(SettingsOperator.GetSetting("sample").ToString(), out int smp) ? smp : 70;
        VolumeDb = ToDB(MasterVol);
        VolumeKnob.SavedValue = AudioPlayer.MasterVol;
    }

    public static float ToDB(float value) => Mathf.LinearToDb(value / 100.0f) - 10f;
    public Tween TweenEQ { get; set; }

    public void MuffledEQ()
    {
        TweenEQ?.Kill();
        TweenEQ = CreateTween().BindNode(this);
        TweenEQ.SetParallel(true);
        TweenEQ.TweenMethod(
            Callable.From<float>(val => _eqEffect.SetBandGainDb(3, val)),
            0f, -60f, 0.25f).SetTrans(Tween.TransitionType.Linear).SetEase(Tween.EaseType.Out);

        TweenEQ.TweenMethod(
            Callable.From<float>(val => _eqEffect.SetBandGainDb(4, val)),
            0f, -60f, 0.25f).SetTrans(Tween.TransitionType.Linear).SetEase(Tween.EaseType.Out);

        TweenEQ.TweenMethod(
            Callable.From<float>(val => _eqEffect.SetBandGainDb(5, val)),
            0f, -60f, 0.25f).SetTrans(Tween.TransitionType.Linear).SetEase(Tween.EaseType.Out);
        TweenEQ.Play();
    }
    public void RevertEQ()
    {
        TweenEQ?.Kill();
        TweenEQ = CreateTween().BindNode(this);
        TweenEQ.SetParallel(true);
        TweenEQ.TweenMethod(
            Callable.From<float>(val => _eqEffect.SetBandGainDb(3, val)),
            -60f, 0f, 0.25f).SetTrans(Tween.TransitionType.Linear).SetEase(Tween.EaseType.Out);

        TweenEQ.TweenMethod(
            Callable.From<float>(val => _eqEffect.SetBandGainDb(4, val)),
            -60f, 0f, 0.25f).SetTrans(Tween.TransitionType.Linear).SetEase(Tween.EaseType.Out);

        TweenEQ.TweenMethod(
            Callable.From<float>(val => _eqEffect.SetBandGainDb(5, val)),
            -60f, 0f, 0.25f).SetTrans(Tween.TransitionType.Linear).SetEase(Tween.EaseType.Out);
        TweenEQ.Play();
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
        Sample.VolumeDb = ToDB(SampleVol);
    }

    public static AudioStream AutoDetectFormat(string audioPath)
    {
        AudioFormat? format = AudioPlayer.GetAudioFormat(audioPath);

        AudioStream? fileStream = format switch
        {
            AudioFormat.WAV => AudioPlayer.LoadWAV(audioPath),
            AudioFormat.OGG => AudioPlayer.LoadOGG(audioPath),
            AudioFormat.MP3 => AudioPlayer.LoadMP3(audioPath),
            _               => null
        };

        if (fileStream == null)
        {
            GD.PrintErr($"[AutoDetect] Unrecognised format: {format}");
            return null;
        }

        return fileStream;
    }

    public static bool isMasterMuted() => (MasterVol == 0);
    public static async void LoadMusic(string audioPath, float seek = 0)
    {
        if (!System.IO.File.Exists(audioPath))
        {
            AudioPlayer.checksum = null;
            Instance.Stream = null;
            Instance.Stop();
            GD.PrintErr("Audio file not found: " + audioPath);
            return;
        }

        if (_isLoading) return;
        _isLoading = true;

        try
        {
            // Compute SHA-256 and read audio bytes on a background thread
            string chk = await Task.Run(() => ChecksumUtil.GetSha256(audioPath));

            if (AudioPlayer.checksum != chk)
            {
                AudioPlayer.checksum = chk;
                
                // Read and decode stream asynchronously
                AudioStream filestream = await Task.Run(() => AutoDetectFormat(audioPath));

                if (filestream != null)
                {
                    Instance.Stream = filestream;
                    Instance.Play(seek);
                    SettingsOperator.Gameplaycfg.TimeTotal = (float)(Instance.Stream?.GetLength() ?? 0);
                }
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"Failed to load audio asynchronously: {ex.Message}");
        }
        finally
        {
            _isLoading = false;
        }
    }
	public void AudioLoop(){
		if (SettingsOperator.Gameplaycfg.TimeTotal - GetPlaybackPosition() < 0.1 && SettingsOperator.loopaudio)
		{
			AudioPlayer.Instance.Play();
		}
	}
    public static AudioStreamMP3 LoadMP3(string path)
    {
        _isogg = false;
        byte[] data = System.IO.File.ReadAllBytes(path);
        
        var sound = new AudioStreamMP3
        {
            Data = data
        };
        return sound;
    }

    public static AudioStreamOggVorbis LoadOGG(string path)
    {
        _isogg = true;
        return AudioStreamOggVorbis.LoadFromFile(path);
    }

    public static AudioStreamWav LoadWAV(string path)
    {
        _isogg = false;
        byte[] data = System.IO.File.ReadAllBytes(path);

        var sound = new AudioStreamWav
        {
            Data = data,
            Format = AudioStreamWav.FormatEnum.Format16Bits, // Prevents real-time conversion stalls
            MixRate = 48000,                                 // Matches default driver rate
            Stereo = true
        };
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
