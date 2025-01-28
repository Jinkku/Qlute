using Godot;
using Godot.Collections;
using System;

public partial class Settings : Button
{
	//public static Dictionary Configuration { get; set; } = new Dictionary<string, object>;
	//public static <string, object>GetSetting { get; set; }
	
	public static SettingsOperator SettingsOperator { get; set; }
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
	private void _on_pressed(){
		Control AccountPanel = GetNode<Control>("%AccountPanel");
		Panel SettingsPanel = GetNode<Panel>("%SettingsPanel");
		AnimationPlayer AniPlayer = GetNode<AnimationPlayer>("%AccountPanelAnimation");
		if (SettingsPanel.Visible) {
			SettingsPanel.Visible = false;
		} else {
			SettingsPanel.Visible = true;
		}
		if (AccountPanel.Position.Y >=-250) {
			AniPlayer.Play("Drop out");
		}

	}
	public void _on_AccountButton_pressed(){
		Control AccountProfileCard = GetNode<Control>("%AccountProfileCard");
		Control AccountPanel = GetNode<Control>("%AccountPanel");
		AnimationPlayer AniPlayer = GetNode<AnimationPlayer>("%AccountPanelAnimation");
		Panel SettingsPanel = GetNode<Panel>("%SettingsPanel");
		Control Card;
		var loggedin = (bool)SettingsOperator.Sessioncfg["loggedin"];
		if (loggedin == true) {
			Card = AccountProfileCard;
		} else {
			Card = AccountPanel;
		}
		if ((bool)SettingsOperator.Sessioncfg["showaccountpro"] == false) {
			AniPlayer.Play("Drop in" + (loggedin == true ? "_Profile" : ""));
		} else {
			AniPlayer.Play("Drop out" + (loggedin == true ? "_Profile" : ""));
		}
		SettingsOperator.Sessioncfg["showaccountpro"] = !(bool)SettingsOperator.Sessioncfg["showaccountpro"];
		SettingsPanel.Visible = false;

	}
}
//