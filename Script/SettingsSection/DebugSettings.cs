using Godot;
using System;

public partial class DebugSettings : PanelContainer
{
	private CheckButton DevHide { get; set; }
	private CheckButton DiscordRPC { get; set; }
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		DiscordRPC = GetNode<CheckButton>("Rows/DiscordRPC");
		DevHide = GetNode<CheckButton>("Rows/HideDevDisclaimer");
		DevHide.ButtonPressed = Check.CheckBoolValue(SettingsOperator.GetSetting("hidedevintro").ToString());
		DiscordRPC.ButtonPressed = Check.CheckBoolValue(SettingsOperator.GetSetting("discord-rpc").ToString());
		GetNode<Label>("Rows/GodotEngineVersion").Text = $"Godot Version {Engine.GetVersionInfo()["major"]}.{Engine.GetVersionInfo()["minor"]}";
	}

	private void _devsel()
	{
		SettingsOperator.SetSetting("hidedevintro", DevHide.ButtonPressed);
	}
	private void _discordrpc()
	{
		SettingsOperator.SetSetting("discord-rpc", DiscordRPC.ButtonPressed);
	}
}
