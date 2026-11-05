using Godot;
using System;

public partial class ModeButton : Button
{
	[Export]
	public bool NewGameMode = false;
	/// <summary>
	/// Gamemode ID this button selects (0 = Mania, 1 = Taiko, 2 = Dash!, 3 = Tap!)
	/// </summary>
	[Export]
	public int ModeID = 0;
	private Label NewGameModeNode { get; set; } 
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		NewGameModeNode = GetNode<Label>("NewLabel");
		NewGameModeNode.Visible = NewGameMode;
		ToggleMode = true;
		Pressed += OnPressed;
	}

	private void OnPressed()
	{
		SettingsOperator.SetSetting("gamemode", ModeID);
		SettingsOperator.SessionConfig.GameMode = ModeID;
		Notify.Post($"Gamemode set to {Text}");
		var scene = GetTree().CurrentScene;
		if (scene != null && scene.HasMethod("_Mode_show")) scene.Call("_Mode_show"); // close the mode panel
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		SetPressedNoSignal(SettingsOperator.GetSelectedGameMode() == ModeID);
	}
}
