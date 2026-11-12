using Godot;
using System;

public partial class SettingsSkinning : PanelContainer
{
	private LineEdit SkinName { get; set; }
	private ColorPickerButton NoteLane1 { get; set; }
	private ColorPickerButton NoteLane2 { get; set; }
	private ColorPickerButton NoteLane3 { get; set; }
	private ColorPickerButton NoteLane4 { get; set; }

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		SkinName = GetNode<LineEdit>("Panel/Scrolls/SettingsRows/Skin Name/Name");
		NoteLane1 = GetNode<ColorPickerButton>("Panel/Scrolls/SettingsRows/NoteLaneColour1/Color");
		NoteLane2 = GetNode<ColorPickerButton>("Panel/Scrolls/SettingsRows/NoteLaneColour2/Color");
		NoteLane3 = GetNode<ColorPickerButton>("Panel/Scrolls/SettingsRows/NoteLaneColour3/Color");
		NoteLane4 = GetNode<ColorPickerButton>("Panel/Scrolls/SettingsRows/NoteLaneColour4/Color");
		SkinName.Text = Skin.Element.Name;
		NoteLane1.Color = Skin.Element.LaneNotes[0];
		NoteLane2.Color = Skin.Element.LaneNotes[1];
		NoteLane3.Color = Skin.Element.LaneNotes[2];
		NoteLane4.Color = Skin.Element.LaneNotes[3];
	}
	private void NoteLane1Color(Color Color)
	{
		Skin.Element.LaneNotes[0] = Color;
		Changed();
	}
	private void NoteLane2Color(Color Color)
	{
		Skin.Element.LaneNotes[1] = Color;
		Changed();
	}
	private void NoteLane3Color(Color Color)
	{
		Skin.Element.LaneNotes[2] = Color;
		Changed();
	}
	private void NoteLane4Color(Color Color)
	{
		Skin.Element.LaneNotes[3] = Color;
		Changed();
	}
	private void NameChange(string text)
	{
		Skin.Element.Name = text;
		// Renaming the default skin alone shouldn't fork it on every keystroke; it's picked up by the next save.
		if (!(Skin.Element.SkinPath?.StartsWith("res://") ?? true)) Changed();
	}
	/// <summary>Lets the skin editor auto-save (forks the default skin into a new one on its first change).</summary>
	private void Changed() => GetParentOrNull<SkinEditor>()?.QueueAutoSave();
}
