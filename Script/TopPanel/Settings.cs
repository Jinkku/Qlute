using Godot;
using Godot.Collections;
using System;

public partial class Settings : Button
{
	//public static Dictionary Configuration { get; set; } = new Dictionary<string, object>;
	//public static <string, object>GetSetting { get; set; }
	
	private SettingsOperator SettingsOperator { get; set; }
	public Control SettingsPanel {get;set;}
	public Control NotificationPanel {get;set;}
	public ColorRect TopPanel {get;set;}
	public Settings Instance {get;set;}
	public override void _Ready(){
		Instance = this;
		SettingsOperator = GetNode<SettingsOperator>("/root/SettingsOperator");
		TopPanel =  GetNode<ColorRect>("../../");
	}
	public override void _Process(double _delta){
		if (Input.IsActionJustPressed("Settings")){
			togglesettingspanel();
		}
	}
	private bool chksettingsv(){
		return SettingsOperator.SessionConfig.SettingsPanelVisible;
	}
	private bool chknotifv(){
		return SettingsOperator.SessionConfig.NotificationPanelVisible;
	}
	private bool chkaccountpos(){
		return SettingsOperator.SessionConfig.ShowAccountProfile;
	}
	public void togglesettingspanel(){
		if (chknotifv()){
			togglenotificationpanel();
		}
		if (!SettingsOperator.SessionConfig.SettingsPanelVisible)
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
		SettingsOperator.SessionConfig.SettingsPanelVisible = !SettingsOperator.SessionConfig.SettingsPanelVisible;
	}
	private void togglenotificationpanel(){
		if (chksettingsv()){
			togglesettingspanel();
		}
		if (!SettingsOperator.SessionConfig.NotificationPanelVisible)
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
		SettingsOperator.SessionConfig.NotificationPanelVisible = !SettingsOperator.SessionConfig.NotificationPanelVisible;
	}

	private void _settings_pressed(){
		if (chkaccountpos()) {
			GetNode<AccountButton>("../../AccountButton").toggleaccountpanel();
		}
		togglesettingspanel();
	}
	private void _on_notifications(){
		if (chkaccountpos()) {
			GetNode<AccountButton>("../../AccountButton").toggleaccountpanel();
		}
		togglenotificationpanel();
	}
}