using Godot;
using System;

public partial class InfoBar : ColorRect
{
	
	public static SettingsOperator SettingsOperator { get; set; }
	public override void _Ready()
	{
		SettingsOperator = GetNode<SettingsOperator>("/root/SettingsOperator");
		InfoBar TopPanel = GetNode<InfoBar>(".");
		if ((bool)SettingsOperator.Sessioncfg["TopPanelSlidein"] == true) {
			TopPanel.Position = new Vector2(0, 0);
		}else{
		AnimationPlayer Ana = GetNode<AnimationPlayer>("%Wabamp");
		Ana.Play("Bootup");
		SettingsOperator.Sessioncfg["TopPanelSlidein"] = true;}
	}
}
