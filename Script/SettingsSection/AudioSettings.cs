using Godot;
using System;

public partial class AudioSettings : PanelContainer
{
	private SettingsOperator SettingsOperator { get; set; }
	public ButtonFade OffsetButton { get; set; }
	public HSlider OffsetSlider { get; set; }
	public Label OffsetTicker { get; set; }
	public Label AoaTicker { get; set; }
	public OptionButton DeviceSelector { get; set; }
	private float MS { get; set; }
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		OffsetButton = GetNode<ButtonFade>("Rows/AudioOffsetAuto");
		OffsetSlider = GetNode<HSlider>("Rows/AudioOffset");
		OffsetTicker = GetNode<Label>("Rows/AudioTicker");
		AoaTicker = GetNode<Label>("Rows/AoaTicker");
		DeviceSelector = GetNode<OptionButton>("Rows/AudioSelector");
		DeviceSelector.Visible = true; // hidden placeholder in Audio.tscn until this was wired up
		GetNode<Label>("Rows/AudioNotice").Visible = true;
		FillDevices();
		DeviceSelector.ItemSelected += OnDeviceSelected;
		DeviceSelector.GetPopup().AboutToPopup += FillDevices; // headphones plugged in since: list them too
		var offset = Num.Float(SettingsOperator.GetSetting("audiooffset")?.ToString(), 0, commaFallback: true);
		if (offset != 0)
		{
			offset = SettingsOperator.GetSetting("audiooffset") != null ? offset : 0;
		}
		else
		{
			offset = 0;
		}
		OffsetSlider.Value = 200 - offset;
		OffsetTicker.Text = "Audio offset - " + (OffsetSlider.Value - 200).ToString("N0") + "ms";
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		MS = SettingsOperator.Getms();
		if (float.IsNaN(MS))
		{
			MS = -1;
			AoaTicker.Text = "Please play a map before you can set your offset!!";
			OffsetButton.StringText = "Play first!";
			OffsetButton.Disabled = true;
		}
		else
		{
			AoaTicker.Text = "your offset from last played song is (" + MS.ToString("N0") + "ms)";
			OffsetButton.StringText = "Set Offset";
			OffsetButton.Disabled = false;
		}
	}
	// ------------------------------------------------------------------ output device

	/// <summary>Lists the sound outputs Godot can see ("Default" follows the system's default device).</summary>
	private void FillDevices()
	{
		DeviceSelector.Clear();
		string current = AudioServer.OutputDevice;
		var devices = AudioServer.GetOutputDeviceList();
		for (int i = 0; i < devices.Length; i++)
		{
			DeviceSelector.AddItem(devices[i] == "Default" ? "Default (system)" : devices[i], i);
			DeviceSelector.SetItemMetadata(i, devices[i]);
			if (devices[i] == current) DeviceSelector.Selected = i;
		}
		DeviceSelector.TooltipText = "Where the game's sound plays. \"Default\" follows your system's sound settings.";
	}

	private void OnDeviceSelected(long index)
	{
		string device = DeviceSelector.GetItemMetadata((int)index).AsString();
		AudioPlayer.SetOutputDevice(device);
		Notify.Post($"Sound now plays on {(device == "Default" ? "your system's default device" : device)}");
	}

	private void _on_audio_offset_value_changed(float value)
	{
		SettingsOperator.SetSetting("audiooffset", 200 - value);
		OffsetTicker.Text = "Audio offset - " + (value - 200).ToString("N0") + "ms";
	}
	private void _aoautoset()
	{
		SettingsOperator.SetSetting("audiooffset", (int)MS);
		OffsetSlider.Value = 200 + MS;
		OffsetTicker.Text = "Audio offset - " + (OffsetSlider.Value - 200).ToString("N0") + "ms";
	}
	private void _aow()
	{
		GetNode<SceneTransition>("/root/Transition").Switch("res://Panels/Screens/AudioOffset.tscn");
	}
}
