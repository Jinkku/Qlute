using Godot;
using System;

public partial class TopPanel : ColorRect
{
	public SettingsOperator SettingsOperator { get; set; }
	public AnimationPlayer Loadinganimation {get ; set; }
	public Sprite2D Loadingicon {get ; set; }
	private TextureRect Shadow { get; set; }
	private Tween ShadowT { get; set; }
	private Label Tooltip { get; set; }
	private Tween TooltipT { get; set; }
	public override void _Ready()
	{
		Shadow = GetNode<TextureRect>("Shadow");
		Loadinganimation = GetNode<AnimationPlayer>("AccountButton/Loadingicon/Loadinganimation");
		Loadingicon = GetNode<Sprite2D>("AccountButton/Loadingicon");
		Loadinganimation.Play("loading");
		SettingsOperator = GetNode<SettingsOperator>("/root/SettingsOperator");
		SettingsOperator.toppaneltoggle(false,true);
	}
	private void _Slidepanelstart(string ani){
		SettingsOperator.SessionConfig.TopPanelSlideip = true;
	}
	private void _Slidepanelfinished(string ani){
		SettingsOperator.SessionConfig.TopPanelSlideip = false;
	}

	private void ShadowAni(int opt)
	{
		ShadowT?.Kill();
		ShadowT = CreateTween();
		ShadowT.TweenProperty(Shadow, "modulate:a", opt, 0.2f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		ShadowT.Play();
	}

	private void _hidetooltip()
	{
	}

	private Control ChangeLog { get; set; }
	private void _ChangeLog()
	{
		if (ChangeLog != null)
		{
			GetTree().CurrentScene.GetNode<ColorRect>("Blank-Chan").QueueFree();
			ChangeLog.QueueFree();
			ChangeLog = null;
		}
		else
		{
			ChangeLog = GD.Load<PackedScene>("res://Panels/Overlays/Changelog.tscn").Instantiate().GetNode<Control>(".");
			GetTree().CurrentScene.AddChild(ChangeLog);
		}
	}
	public void toggledashboard(){}
	public void _hovered()
	{
		ShadowAni(1);
	}
	public void _unhover(){
		ShadowAni(0);
	}
	
	public PanelContainer ChatBox {get;set;}

	private void _ChatRoom()
	{
		if (!SettingsOperator.SessionConfig.ChatBoxVisible)
		{
			ChatBox?.QueueFree();
			ChatBox = GD.Load<PackedScene>("res://Panels/Overlays/ChatOverlay.tscn").Instantiate().GetNode<PanelContainer>(".");
			AddChild(ChatBox);
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
			_tween.TweenProperty(ChatBox, "modulate", new Color(1f, 1f, 1f, 0f), 0.3f)
				.SetTrans(Tween.TransitionType.Cubic)
				.SetEase(Tween.EaseType.Out);
			_tween.Play();
		}
		SettingsOperator.SessionConfig.ChatBoxVisible = !SettingsOperator.SessionConfig.ChatBoxVisible;
	}

	private void _on_browse_pressed()
	{
		GetNode<SceneTransition>("/root/Transition").Switch("res://Panels/Screens/Browse.tscn");
	}
	
	private Control VolumePanel { get; set; }
	private CanvasLayer VolumeLayer { get; set; }
	public override void _Process(double _delta)
	{
		Loadingicon.Visible = (bool)SettingsOperator.SessionConfig.Loggingin;

		if (Input.IsActionJustPressed("Special") && !Global.VolumeControlActive)
		{
			Global.VolumeControlActive = true;
			VolumePanel?.QueueFree();
			VolumeLayer = GD.Load<PackedScene>("res://Panels/Overlays/VolumeControl.tscn").Instantiate().GetNode<CanvasLayer>(".");
			VolumePanel = VolumeLayer.GetNode<Control>("VolumeControl");
			GetTree().Root.AddChild(VolumeLayer);
			GetTree().CurrentScene.GetNode(".").SetProcessInput(false);
			var _tween = VolumePanel.CreateTween();
			VolumePanel.Modulate = new Color(1f, 1f, 1f, 0f);
			VolumePanel.Position = new Vector2(-VolumePanel.Size.X, VolumePanel.Position.Y);
			_tween.SetParallel(true);
			_tween.TweenProperty(VolumePanel, "position", new Vector2(0, VolumePanel.Position.Y), 0.3f)
				.SetTrans(Tween.TransitionType.Cubic)
				.SetEase(Tween.EaseType.Out);
			_tween.TweenProperty(VolumePanel, "modulate", new Color(1f, 1f, 1f, 1f), 0.3f)
				.SetTrans(Tween.TransitionType.Cubic)
				.SetEase(Tween.EaseType.Out);
			_tween.Play();
		}

		foreach (var NotiInfo in NotificationListener.NotificationList)
		{
			if (!NotiInfo.Finished)
			{
				Sample.PlaySample("res://SelectableSkins/Slia/Sounds/notification.wav");
				var NotiCard = GD.Load<PackedScene>("res://Panels/Overlays/Notification.tscn").Instantiate().GetNode<NotificationApplet>(".");
				NotificationListener.NotificationCards.Add(NotiCard);
				NotiCard.Position = new Vector2(GetViewportRect().Size.X, 60 + ((NotiCard.Size.Y + 10) * NotificationListener.Count));
				NotiCard.Text = NotiInfo.Title;
				NotiCard.SetMeta("id", NotificationListener.Count);
				NotiCard.SetMeta("listid", NotificationListener.NotificationList.Count - 1);
				NotiCard.SetMeta("time", NotiInfo.Time);
				AddChild(NotiCard);
				NotiCard.Modulate = new Color(1f, 1f, 1f, 0f);
				var tween = NotiCard.CreateTween();
				tween.SetParallel(true);
				tween.TweenProperty(NotiCard, "position:x", GetViewportRect().Size.X - NotiCard.Size.X - 10, 0.4f)
					.SetTrans(Tween.TransitionType.Cubic)
					.SetEase(Tween.EaseType.Out);
				tween.TweenProperty(NotiCard, "modulate", new Color(1f, 1f, 1f, 1f), 0.4f)
					.SetTrans(Tween.TransitionType.Cubic)
					.SetEase(Tween.EaseType.Out);
				tween.Play();
				NotiInfo.Finished = true;
				NotificationListener.Count++;
			}
		}
	}
}
