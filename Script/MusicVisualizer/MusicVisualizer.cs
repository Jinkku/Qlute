using Godot;
using System;

public partial class MusicVisualizer : PanelContainer
{
	private AudioEffectSpectrumAnalyzerInstance _spectrum;
	private int _busIndex;
	[Export] public float SmoothSpeed = 15f;
	[Export] private float _rotationMultiplier = 150.0f;

	// Bass frequency range
	[Export] public Vector2 Range = new Vector2(0f, 250f);
	private float _currentRotationSpeed = 0.0f;
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		// Get master bus index
		_busIndex = AudioServer.GetBusIndex("Music");
		PivotOffset = Size / 2;
		// Get the analyzer effect instance from the master bus
		_spectrum = (AudioEffectSpectrumAnalyzerInstance)AudioServer.GetBusEffectInstance(_busIndex, 0);
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		if (_spectrum == null) return;
        
		// Get magnitude for just the bass range
		Vector2 magnitude = _spectrum.GetMagnitudeForFrequencyRange(Range.X, Range.Y);
		float bassEnergy = (magnitude.X + magnitude.Y) * 0.5f;
		float targetSpeed = Math.Max(bassEnergy * _rotationMultiplier, 0.1f);
		// Smooth out the transition so rotation doesn't jitter abruptly
		_currentRotationSpeed = Mathf.Lerp(_currentRotationSpeed, targetSpeed, (float)(SmoothSpeed * delta));

		// Apply rotation reference
		Rotation += _currentRotationSpeed * (float)delta;
	}
}
