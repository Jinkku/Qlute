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
		if ((bool)SettingsOperator.Sessioncfg["toppanelhide"] != true){
		AnimationPlayer Ana = GetNode<AnimationPlayer>("%Wabamp");
		Ana.Play("Bootup");}
	}
	private void _Slidepanelstart(string ani){
		SettingsOperator.Sessioncfg["TopPanelSlideip"] = true;
	}
	private void _Slidepanelfinished(string ani){
		SettingsOperator.Sessioncfg["TopPanelSlideip"] = false;
	}
	public void _hovered(){
		TextureRect Ana = GetNode<TextureRect>("Shadow");
		Ana.Visible = true;
	}
	public void _unhover(){
		TextureRect Ana = GetNode<TextureRect>("Shadow");
		Ana.Visible = false;
	}
	private void _on_browse_pressed(){
		GetTree().ChangeSceneToFile("res://Panels/Screens/Browse.tscn");
	}
	public override void _Process(double _delta){
		Loadingicon.Visible = (bool)SettingsOperator.Sessioncfg["loggingin"];
		
	}
}

