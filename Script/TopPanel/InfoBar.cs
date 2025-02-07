using Godot;
using System;

public partial class InfoBar : ColorRect
{
	
	public static SettingsOperator SettingsOperator { get; set; }
	public static AnimationPlayer Loadinganimation {get ; set; }
	public static Sprite2D Loadingicon {get ; set; }
	public override void _Ready()
	{
		Loadinganimation = GetNode<AnimationPlayer>("AccountButton/Loadingicon/Loadinganimation");
		Loadingicon = GetNode<Sprite2D>("AccountButton/Loadingicon");
		Loadinganimation.Play("loading");
		SettingsOperator = GetNode<SettingsOperator>("/root/SettingsOperator");
		InfoBar TopPanel = GetNode<InfoBar>(".");
		if ((bool)SettingsOperator.Sessioncfg["TopPanelSlidein"] == true) {
			TopPanel.Position = new Vector2(0, 0);
		}else{
		AnimationPlayer Ana = GetNode<AnimationPlayer>("%Wabamp");
		Ana.Play("Bootup");
		SettingsOperator.Sessioncfg["TopPanelSlidein"] = true;}
	}
	public override void _Process(double _delta){
		
	}
}
