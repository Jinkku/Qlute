using Godot;
using System;
using System.Linq;

public partial class InfoBar : ColorRect
{
	
	public SettingsOperator SettingsOperator { get; set; }
	public AnimationPlayer Loadinganimation {get ; set; }
	public ColorRect TopPanel {get;set;}
	public Sprite2D Loadingicon {get ; set; }
	public override void _Ready()
	{
		Loadinganimation = GetNode<AnimationPlayer>("AccountButton/Loadingicon/Loadinganimation");
		Loadingicon = GetNode<Sprite2D>("AccountButton/Loadingicon");
		Loadinganimation.Play("loading");
		SettingsOperator = GetNode<SettingsOperator>("/root/SettingsOperator");
		TopPanel = GetTree().Root.GetNode<ColorRect>("/root/TopPanelOnTop/TopPanel/InfoBar");
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
	
	public PanelContainer ChatBox {get;set;}

	private void _ChatRoom()
	{
		if (!(bool)SettingsOperator.Sessioncfg["chatboxv"])
		{

			if (IsInstanceValid(ChatBox))
			{
				ChatBox.QueueFree();
			}
			ChatBox = GD.Load<PackedScene>("res://Panels/Overlays/ChatOverlay.tscn").Instantiate().GetNode<PanelContainer>(".");
			TopPanel.AddChild(ChatBox);
			GetTree().CurrentScene.GetNode(".").SetProcessInput(false);
			var _tween = ChatBox.CreateTween();
			ChatBox.Position = new Vector2(0, GetViewportRect().Size.Y);
			ChatBox.Size = new Vector2(GetViewportRect().Size.X, ChatBox.Size.Y);
			ChatBox.MouseFilter = Control.MouseFilterEnum.Ignore;
			_tween.SetParallel(true);
			_tween.TweenProperty(ChatBox, "position", new Vector2(0, GetViewportRect().Size.Y - ChatBox.Size.Y), 0.3f)
				.SetTrans(Tween.TransitionType.Cubic)
				.SetEase(Tween.EaseType.Out);
			_tween.TweenProperty(GetTree().CurrentScene, "position", new Vector2(0, -50), 0.3f)
				.SetTrans(Tween.TransitionType.Cubic)
				.SetEase(Tween.EaseType.Out);
			_tween.TweenProperty(GetTree().CurrentScene, "modulate", new Color(0.5f, 0.5f, 0.5f, 1f), 0.3f)
				.SetTrans(Tween.TransitionType.Cubic)
				.SetEase(Tween.EaseType.Out);
			_tween.Play();
		}
		else if (IsInstanceValid(ChatBox))
		{
			var _tween = ChatBox.CreateTween();
			GetTree().CurrentScene.GetNode(".").SetProcessInput(true);
			_tween.SetParallel(true);
			_tween.TweenProperty(ChatBox, "position", new Vector2(0, GetViewportRect().Size.Y), 0.3f)
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
		SettingsOperator.Sessioncfg["chatboxv"] = !(bool)SettingsOperator.Sessioncfg["chatboxv"];
	}


	private void _on_browse_pressed()
	{
		GetNode<SceneTransition>("/root/Transition").Switch("res://Panels/Screens/Browse.tscn");
	}
	public override void _Process(double _delta){
		Loadingicon.Visible = (bool)SettingsOperator.Sessioncfg["loggingin"];
		foreach (var NotiInfo in NotificationListener.NotificationList){
			if (!NotiInfo.Finished){
				Sample.Instance.Stream = ResourceLoader.Load<AudioStreamWav>("res://Sounds/notification.wav");
				Sample.Instance.Play();
    	    var NotiCard = GD.Load<PackedScene>("res://Panels/Overlays/Notification.tscn").Instantiate().GetNode<Button>(".");
			NotificationListener.NotificationCards.Add(NotiCard);
	        AddChild(NotiCard);
			NotiCard.Position = new Vector2(GetViewportRect().Size.X,60 + ((NotiCard.Size.Y+10) * NotificationListener.Count));
			NotiCard.Text = NotiInfo.Title;
			NotiCard.SetMeta("id",NotificationListener.Count);
			NotiCard.SetMeta("time",NotiInfo.Time);
			var tween = NotiCard.CreateTween();
			tween.TweenProperty(NotiCard, "position", new Vector2(GetViewportRect().Size.X-NotiCard.Size.X-10,NotiCard.Position.Y), 0.2f)
				.SetTrans(Tween.TransitionType.Cubic)
				.SetEase(Tween.EaseType.Out);			
			tween.Play();
			NotiInfo.Finished = true;
			NotificationListener.Count++;
			}
			
		}
		
	}
}

