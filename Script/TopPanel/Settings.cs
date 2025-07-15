using Godot;
using Godot.Collections;
using System;

public partial class Settings : Button
{
	//public static Dictionary Configuration { get; set; } = new Dictionary<string, object>;
	//public static <string, object>GetSetting { get; set; }
	
	public static SettingsOperator SettingsOperator { get; set; }
	private Tween acctween;
	public Control Card;
	public Control SettingsPanel {get;set;}
	public Control NotificationPanel {get;set;}
	public ColorRect TopPanel {get;set;}
	public Settings Instance {get;set;}
	public Label Ranking {get;set;}
	public Label PlayerName {get;set;}
	public override void _Ready(){
		Instance = this;
		SettingsOperator = GetNode<SettingsOperator>("/root/SettingsOperator");
		TopPanel = GetTree().Root.GetNode<ColorRect>("/root/TopPanelOnTop/TopPanel/InfoBar");
        PlayerName = GetNode<Label>("%UPlayerName");
		Ranking = GetNode<Label>("%Ranking");

	}
	public override void _Process(double _delta){
		if (Input.IsActionJustPressed("Settings")){
			togglesettingspanel();
		}
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
			Ranking.Visible = false;
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
		if (chknotifv()){
			togglenotificationpanel();
		}
		if (!(bool)SettingsOperator.Sessioncfg["settingspanelv"])
		{
			if (IsInstanceValid(SettingsPanel))
			{
				SettingsPanel.QueueFree();
			}
			SettingsPanel = GD.Load<PackedScene>("res://Panels/Overlays/Settings.tscn").Instantiate().GetNode<Control>(".");
			TopPanel.AddChild(SettingsPanel);
			GetTree().CurrentScene.GetNode(".").SetProcessInput(false);
			var _tween = SettingsPanel.CreateTween();
			SettingsPanel.Position = new Vector2(-SettingsPanel.Size.X, 50);
			SettingsPanel.Size = new Vector2(SettingsPanel.Size.X, GetViewportRect().Size[1] - SettingsPanel.Position.Y);
			SettingsPanel.MouseFilter = Control.MouseFilterEnum.Ignore;
			_tween.SetParallel(true);
			_tween.TweenProperty(SettingsPanel, "position", new Vector2(0, 50), 0.3f)
				.SetTrans(Tween.TransitionType.Cubic)
				.SetEase(Tween.EaseType.Out);
			_tween.TweenProperty(GetTree().CurrentScene, "position", new Vector2(50, 0), 0.3f)
				.SetTrans(Tween.TransitionType.Cubic)
				.SetEase(Tween.EaseType.Out);
			_tween.TweenProperty(GetTree().CurrentScene, "modulate", new Color(0.5f, 0.5f, 0.5f, 1f), 0.3f)
				.SetTrans(Tween.TransitionType.Cubic)
				.SetEase(Tween.EaseType.Out);
			_tween.Play();
		}
		else if (IsInstanceValid(SettingsPanel))
		{
			var _tween = SettingsPanel.CreateTween();
			GetTree().CurrentScene.GetNode(".").SetProcessInput(true);
			_tween.SetParallel(true);
			_tween.TweenProperty(SettingsPanel, "position", new Vector2(-SettingsPanel.Size.X, 50), 0.3f)
				.SetTrans(Tween.TransitionType.Cubic)
				.SetEase(Tween.EaseType.Out);
			_tween.TweenProperty(GetTree().CurrentScene, "position", new Vector2(0, 0), 0.3f)
				.SetTrans(Tween.TransitionType.Cubic)
				.SetEase(Tween.EaseType.Out);
			_tween.TweenProperty(GetTree().CurrentScene, "modulate", new Color(1f, 1f, 1f, 1f), 0.3f)
				.SetTrans(Tween.TransitionType.Cubic)
				.SetEase(Tween.EaseType.Out);
			_tween.Play();
		}
		SettingsOperator.Sessioncfg["settingspanelv"] = !(bool)SettingsOperator.Sessioncfg["settingspanelv"];
	}
	private void queue_free(Control Prog){
			Prog.QueueFree();
			}
	private void togglenotificationpanel(){
		if (chksettingsv()){
			togglesettingspanel();
		}
		if (!(bool)SettingsOperator.Sessioncfg["notificationpanelv"])
		{
			if (IsInstanceValid(NotificationPanel))
			{
				NotificationPanel.QueueFree();
			}
			NotificationPanel = GD.Load<PackedScene>("res://Panels/Overlays/NotificationPanel.tscn").Instantiate().GetNode<ColorRect>(".");
			NotificationPanel.Position = new Vector2(0, 50);
			var tmp = NotificationPanel.Size;
			TopPanel.AddChild(NotificationPanel);
			NotificationPanel.Size = new Vector2(tmp[0], GetViewportRect().Size.Y - NotificationPanel.Position.Y);
			var _tween = NotificationPanel.CreateTween();
			NotificationPanel.MouseFilter = Control.MouseFilterEnum.Ignore;
			GetTree().CurrentScene.GetNode(".").SetProcessInput(false);
			_tween.SetParallel(true);
			_tween.TweenProperty(NotificationPanel, "position", new Vector2(GetViewportRect().Size.X - NotificationPanel.Size.X, 50), 0.3f)
					.SetTrans(Tween.TransitionType.Cubic)
					.SetEase(Tween.EaseType.Out);
			_tween.TweenProperty(GetTree().CurrentScene, "position", new Vector2(-50, 0), 0.3f)
				.SetTrans(Tween.TransitionType.Cubic)
				.SetEase(Tween.EaseType.Out);
			_tween.TweenProperty(GetTree().CurrentScene, "modulate", new Color(0.5f, 0.5f, 0.5f, 1f), 0.3f)
				.SetTrans(Tween.TransitionType.Cubic)
				.SetEase(Tween.EaseType.Out);
			_tween.Play();
		}
		else if (IsInstanceValid(NotificationPanel))
		{
			var _tween = NotificationPanel.CreateTween();
			GetTree().CurrentScene.GetNode(".").SetProcessInput(true);
			_tween.SetParallel(true);
			_tween.TweenProperty(NotificationPanel, "position", new Vector2(GetViewportRect().Size.X, 50), 0.3f)
				.SetTrans(Tween.TransitionType.Cubic)
				.SetEase(Tween.EaseType.Out);
			_tween.TweenProperty(GetTree().CurrentScene, "position", new Vector2(0, 0), 0.3f)
				.SetTrans(Tween.TransitionType.Cubic)
				.SetEase(Tween.EaseType.Out);
			_tween.TweenProperty(GetTree().CurrentScene, "modulate", new Color(1f, 1f, 1f, 1f), 0.3f)
				.SetTrans(Tween.TransitionType.Cubic)
				.SetEase(Tween.EaseType.Out);
			_tween.Play();
		}
		SettingsOperator.Sessioncfg["notificationpanelv"] = !(bool)SettingsOperator.Sessioncfg["notificationpanelv"];

	}
	private void toggleaccountpanel(){
		var loggedin = (bool)SettingsOperator.Sessioncfg["loggedin"];
		acctween?.Kill();
		acctween = CreateTween();
		if (!(bool)SettingsOperator.Sessioncfg["showaccountpro"])
		{
			Card = GD.Load<PackedScene>("res://Panels/Overlays/AccountPrompt.tscn").Instantiate().GetNode<Control>(".");
			Card.ZIndex = -1;
			TopPanel.AddChild(Card);
			Card.Position = new Vector2(0, -265);
			acctween.TweenProperty(Card, "position", new Vector2(0,SettingsOperator.TopPanelPosition), 0.2f).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
			acctween.Play();
		}
		else
		{
			acctween.TweenProperty(Card, "position", new Vector2(0,-265), 0.2f).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
			acctween.TweenCallback(Callable.From(Card.QueueFree));
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