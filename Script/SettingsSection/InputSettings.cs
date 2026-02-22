using Godot;
using System;

public partial class InputSettings : PanelContainer
{
	private Label AI { get; set; }
	private bool Listening { get; set; }
	private int ListeningID { get; set; }
	private string OldKey {get; set;}
	private VBoxContainer Selection { get; set; }
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		Selection = GetNode<VBoxContainer>("Rows/Selection");
		AI = GetNode<Label>("Rows/AI");
		foreach (var key in Selection.GetChildren())
		{
			key.GetNode<ButtonFade>(key.Name.ToString()).StringText = SettingsOperator.GetSetting(key.Name).ToString();
		}
	}

	private void SetButton(int id)
	{
		var old = Selection.GetNode<ButtonFade>($"Key{ListeningID}/Key{ListeningID}");
		if (old.StringText == "...")
		{
			old.StringText = OldKey;
		}
		Listening = true;
		ListeningID = id;
		OldKey = SettingsOperator.GetSetting($"Key{ListeningID}").ToString();
		Selection.GetNode<ButtonFade>($"Key{ListeningID}/Key{ListeningID}").StringText = "...";
	}

	public override void _Input(InputEvent @event)
	{
		if (Listening && (@event is InputEventKey code))
		{
			Listening = false;
			Notify.Post($"Key {ListeningID} is set!");
			SettingsOperator.SetSetting($"Key{ListeningID}", code.Keycode.ToString());
			Selection.GetNode<ButtonFade>($"Key{ListeningID}/Key{ListeningID}").StringText = @event.AsText();
		}
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		if (Listening)
		{
			AI.Text = "Awaiting Input...";
		}
		else
		{
			AI.Text = "Click a key.";
		}
	}
}
