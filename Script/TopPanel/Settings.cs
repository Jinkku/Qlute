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
	public ColorRect TopPanel {get;set;}
	public Settings Instance {get;set;}
	private void ready(){
		Instance = this;
		SettingsOperator = GetNode<SettingsOperator>("/root/SettingsOperator");
		TopPanel = GetTree().Root.GetNode<ColorRect>("/root/TopPanelOnTop/TopPanel/InfoBar");
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
		var _tween = CreateTween();
		if (!(bool)SettingsOperator.Sessioncfg["settingspanelv"]){
			SettingsPanel = GD.Load<PackedScene>("res://Panels/Overlays/Settings.tscn").Instantiate().GetNode<Control>(".");
			if (TopPanel.HasNode("SettingsPanel")){
				TopPanel.GetNode<Control>("SettingsPanel").QueueFree();
			}
			TopPanel.AddChild(SettingsPanel);
			SettingsPanel.Position = new Vector2(-SettingsPanel.Size.X,50);
			SettingsPanel.Size = new Vector2(SettingsPanel.Size.X,GetViewportRect().Size[1]-SettingsPanel.Position.Y);
			_tween.TweenProperty(SettingsPanel, "position", new Vector2(0,50), 0.3f)
				.SetTrans(Tween.TransitionType.Cubic)
				.SetEase(Tween.EaseType.Out);
			_tween.Play();
		} else {
			if (TopPanel.HasNode("SettingsPanel")){
			_tween.TweenProperty(SettingsPanel, "position", new Vector2(-SettingsPanel.Size.X,50), 0.3f)
				.SetTrans(Tween.TransitionType.Cubic)
				.SetEase(Tween.EaseType.Out);
			_tween.TweenCallback(Callable.From(() => queue_free(SettingsPanel)));
			}
		}
		SettingsOperator.Sessioncfg["settingspanelv"] = !(bool)SettingsOperator.Sessioncfg["settingspanelv"];
	}
	private void queue_free(Control Prog){
			Prog.QueueFree();
			}
	private void togglenotificationpanel(){
		var _tween = CreateTween();
		if (!(bool)SettingsOperator.Sessioncfg["notificationpanelv"]){
		NotificationPanel = GD.Load<PackedScene>("res://Panels/Overlays/NotificationPanel.tscn").Instantiate().GetNode<ColorRect>(".");
		NotificationPanel.Position = new Vector2(0,50);
		var tmp = NotificationPanel.Size;
		TopPanel.AddChild(NotificationPanel);
		NotificationPanel.Size = tmp;
		_tween.TweenProperty(NotificationPanel, "position", new Vector2(NotificationPanel.Size.X-50,50), 0.3f)
				.SetTrans(Tween.TransitionType.Cubic)
				.SetEase(Tween.EaseType.Out);
			_tween.Play();
		GD.Print("asaa");
		} else {
			if (TopPanel.HasNode("NotificationPanel")){
			_tween.TweenProperty(NotificationPanel, "position", new Vector2(GetViewportRect().Size.X,50), 0.3f)
				.SetTrans(Tween.TransitionType.Cubic)
				.SetEase(Tween.EaseType.Out);
			_tween.TweenCallback(Callable.From(() => queue_free(NotificationPanel)));
			}
		}
		SettingsOperator.Sessioncfg["notificationpanelv"] = !(bool)SettingsOperator.Sessioncfg["notificationpanelv"];

	}
	private void toggleaccountpanel(){
	//	AnimationPlayer AniPlayer = GetNode<AnimationPlayer>("%AccountPanelAnimation");
		var loggedin = (bool)SettingsOperator.Sessioncfg["loggedin"];
		if (!(bool)SettingsOperator.Sessioncfg["showaccountpro"]){
		Card = GD.Load<PackedScene>("res://Panels/Overlays/AccountPrompt.tscn").Instantiate().GetNode<Control>(".");
		Card.Position = new Vector2(0,50);
		TopPanel.AddChild(Card);
		} else {
			Card.QueueFree();
		}
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