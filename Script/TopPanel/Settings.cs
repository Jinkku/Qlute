using Godot;
using Godot.Collections;
using System;

public partial class Settings : Button
{
	//public static Dictionary Configuration { get; set; } = new Dictionary<string, object>;
	//public static <string, object>GetSetting { get; set; }
	
	public static SettingsOperator SettingsOperator { get; set; }
	public Control Card;
	public Control SettingsPanel {get;set;}
	public Control NotificationPanel {get;set;}
	private void ready(){
		SettingsOperator = GetNode<SettingsOperator>("/root/SettingsOperator");

        Label PlayerName = GetNode<Label>("%UPlayerName");
		Label Ranking = GetNode<Label>("%Ranking");
		string username = SettingsOperator.GetSetting("username")?.ToString();
		string ranking = SettingsOperator.Sessioncfg["ranknumber"]?.ToString();
		if (username != null){
			PlayerName.Text = username;
			if (ranking != null && ranking != "0"){
        	Ranking.Visible = true;}
			else{Ranking.Visible = false;}
			Ranking.Text = "#" + ranking;
		} else{
			PlayerName.Text = "Guest\nLog in here!";
		}

	}
	private bool chksettingsv(){
		return (bool)SettingsOperator.Sessioncfg["settingspanelv"];
	}
	private bool chknotifv(){
		return (bool)SettingsOperator.Sessioncfg["notificationpanelv"];
	}
	private bool chkaccountpos(){
		return (bool)SettingsOperator.Sessioncfg["showaccountpro"];
	}
	private void togglesettingspanel(){
		if (!(bool)SettingsOperator.Sessioncfg["settingspanelv"]){
		SettingsPanel = GD.Load<PackedScene>("res://Panels/Overlays/Settings.tscn").Instantiate().GetNode<Control>(".");
		GetTree().CurrentScene.AddChild(SettingsPanel);
		} else {
			SettingsPanel.QueueFree();
		}
		SettingsOperator.Sessioncfg["settingspanelv"] = !(bool)SettingsOperator.Sessioncfg["settingspanelv"];

	}
	private void togglenotificationpanel(){
		GD.Print("clicked");
		if (!(bool)SettingsOperator.Sessioncfg["notificationpanelv"]){
		NotificationPanel = GD.Load<PackedScene>("res://Panels/Overlays/NotificationPanel.tscn").Instantiate().GetNode<Control>(".");
		GetTree().CurrentScene.AddChild(NotificationPanel);
		} else {
			NotificationPanel.QueueFree();
		}
		SettingsOperator.Sessioncfg["notificationpanelv"] = !(bool)SettingsOperator.Sessioncfg["notificationpanelv"];

	}
	private void toggleaccountpanel(){
		Control AccountProfileCard = GetNode<Control>("%AccountProfileCard");
		Control AccountPanel = GetNode<Control>("%AccountPanel");
		AnimationPlayer AniPlayer = GetNode<AnimationPlayer>("%AccountPanelAnimation");
		var loggedin = (bool)SettingsOperator.Sessioncfg["loggedin"];
		if (loggedin == true) {
			Card = AccountProfileCard;
		} else {
			Card = AccountPanel;
		}
		if (((bool)SettingsOperator.Sessioncfg["showaccountpro"] == true)) AniPlayer.PlayBackwards("Drop in" + (loggedin == true ? "_Profile" : "")); else AniPlayer.Play("Drop in" + (loggedin == true ? "_Profile" : ""));
		SettingsOperator.Sessioncfg["showaccountpro"] = !(bool)SettingsOperator.Sessioncfg["showaccountpro"];}
	private void _settings_pressed(){
		if (chkaccountpos()) {
			toggleaccountpanel();
		}
		togglesettingspanel();

	}
	private void _on_notifications(){
		if (chkaccountpos()) {
			toggleaccountpanel();
		}
		togglenotificationpanel();

	}
	private void _on_AccountButton_pressed(){
		toggleaccountpanel();

	}
}
//