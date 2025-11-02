using Godot;
using System;

public partial class MusicBar : TextureProgressBar
{
    private AudioEffectSpectrumAnalyzerInstance _spectrum;
    private int _busIndex;
    [Export] public float SmoothSpeed = 15f;

    // Bass frequency range
    [Export] public Vector2 BassRange = new Vector2(20f, 250f);
    public override void _Ready()
    {
        // Get master bus index
        _busIndex = AudioServer.GetBusIndex("Music");
        PivotOffset = Size / 2;

        // Get the analyzer effect instance from the master bus
        _spectrum = (AudioEffectSpectrumAnalyzerInstance)AudioServer.GetBusEffectInstance(_busIndex, 0);
    }

    public override void _Process(double delta)
    {
        if (_spectrum == null) return;
        
        // Get magnitude for just the bass range
        float magnitude = _spectrum.GetMagnitudeForFrequencyRange(BassRange.X, BassRange.Y).Length();
        float db = Mathf.LinearToDb(magnitude);

        // Normalize -60dB → 0dB into 0–1 range
        float normalized = Mathf.Clamp((db + 60f) / 60f, 0f, 1f);

        // Smooth movement
        float targetValue = normalized * 100f;
        float currentValue = (float)Value;
        Value = Mathf.Lerp(currentValue, targetValue, SmoothSpeed * (float)delta);
    }
}