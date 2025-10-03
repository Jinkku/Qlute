using Godot;
using System;
using System.IO;
using System.Linq;


public partial class Global : Node
{
	// Called when the node enters the scene tree for the first time.
	private SettingsOperator SettingsOperator { get; set; }
	public override void _Ready()
	{
		SettingsOperator = GetNode<SettingsOperator>("/root/SettingsOperator");
	}
	private Node _CurrentScene { get; set; }
	public bool _skineditorEnabled = false;
	public static bool _SkinStart = false;
	private Control _skineditorScene;
	private Tween SkinEditorAni { get; set; }

	public override void _Process(double delta)
	{
		_CurrentScene = GetTree().CurrentScene;
		if (_SkinStart)
		{
			_skineditorEnabled = !_skineditorEnabled;
			_SkinStart = false;
			SkinEditorAni?.Kill();
			SkinEditorAni = GetTree().CreateTween();
			SkinEditorAni.SetParallel(true);
			if (_skineditorEnabled)
			{
				if (IsInstanceValid(_skineditorScene)) _skineditorScene?.QueueFree();
				_skineditorScene = GD.Load<PackedScene>("res://Panels/Screens/SkinEditor.tscn").Instantiate().GetNode<Control>(".");
				GetTree().Root.AddChild(_skineditorScene);
				var SideBarL = _skineditorScene.GetNode<PanelContainer>("Creativity");
				var SideBarR = _skineditorScene.GetNode<PanelContainer>("Extras");
				var ToolBar = _skineditorScene.GetNode<PanelContainer>("ToolBar");
				var ControlPanel = _skineditorScene.GetNode<PanelContainer>("ControlPanel");
				var Error = _skineditorScene.GetNode<PanelContainer>("Missing");
				var aftercontrol = ControlPanel.Position;
				ControlPanel.Position = new Vector2(ControlPanel.Position.X, ControlPanel.Position.Y + ControlPanel.Size.Y);
				ToolBar.Position = new Vector2(0, ToolBar.Position.Y - ToolBar.Size.Y);
				SideBarL.Position = new Vector2(-SideBarL.Size.X, 0);
				SideBarL.Size = new Vector2(SideBarL.Size.X, GetViewport().GetVisibleRect().Size.Y);
				SideBarR.Position = new Vector2(SideBarR.Position.X + SideBarR.Size.X, 0);
				SideBarR.Size = new Vector2(SideBarR.Size.X, GetViewport().GetVisibleRect().Size.Y);
				Error.Position = new Vector2(Error.Position.X, Error.Position.Y - Error.Size.Y);
				SkinEditorAni.TweenProperty(SideBarL, "position", new Vector2(0, ToolBar.Size.Y), 0.5f)
					.SetTrans(Tween.TransitionType.Cubic)
					.SetEase(Tween.EaseType.Out);
				SkinEditorAni.TweenProperty(SideBarL, "size:y", SideBarL.Size.Y - ControlPanel.Size.Y - ToolBar.Size.Y, 0.5f)
					.SetTrans(Tween.TransitionType.Cubic)
					.SetEase(Tween.EaseType.Out);
				SkinEditorAni.TweenProperty(SideBarR, "position", new Vector2(SideBarR.Position.X - SideBarR.Size.X, ToolBar.Size.Y), 0.5f)
					.SetTrans(Tween.TransitionType.Cubic)
					.SetEase(Tween.EaseType.Out);
				SkinEditorAni.TweenProperty(SideBarR, "size:y", SideBarR.Size.Y - ControlPanel.Size.Y - ToolBar.Size.Y, 0.5f)
					.SetTrans(Tween.TransitionType.Cubic)
					.SetEase(Tween.EaseType.Out);
				SkinEditorAni.TweenProperty(ToolBar, "position:y", 0, 0.5f)
					.SetTrans(Tween.TransitionType.Cubic)
					.SetEase(Tween.EaseType.Out);
				SkinEditorAni.TweenProperty(ControlPanel, "position:y", aftercontrol.Y, 0.5f)
					.SetTrans(Tween.TransitionType.Cubic)
					.SetEase(Tween.EaseType.Out);
				SkinEditorAni.TweenProperty(_CurrentScene, "size", new Vector2(GetViewport().GetVisibleRect().Size.X - SideBarL.Size.X - SideBarR.Size.X, GetViewport().GetVisibleRect().Size.Y - ToolBar.Size.Y - ControlPanel.Size.Y), 0.5f)
					.SetTrans(Tween.TransitionType.Cubic)
					.SetEase(Tween.EaseType.Out);
				SkinEditorAni.TweenProperty(_CurrentScene, "position", new Vector2(SideBarL.Size.X, ToolBar.Size.Y), 0.5f)
					.SetTrans(Tween.TransitionType.Cubic)
					.SetEase(Tween.EaseType.Out);
				SkinEditorAni.TweenProperty(Error, "position:y", Error.Position.Y + Error.Size.Y, 0.5f)
					.SetTrans(Tween.TransitionType.Cubic)
					.SetEase(Tween.EaseType.Out);
			}
			else
			{
				var SideBarL = _skineditorScene.GetNode<PanelContainer>("Creativity");
				var SideBarR = _skineditorScene.GetNode<PanelContainer>("Extras");
				var ToolBar = _skineditorScene.GetNode<PanelContainer>("ToolBar");
				var ControlPanel = _skineditorScene.GetNode<PanelContainer>("ControlPanel");
				var Error = _skineditorScene.GetNode<PanelContainer>("Missing");
				SkinEditorAni.TweenProperty(SideBarL, "position", new Vector2(SideBarL.Position.X - SideBarL.Size.X, 0), 0.5f)
					.SetTrans(Tween.TransitionType.Cubic)
					.SetEase(Tween.EaseType.Out);
				SkinEditorAni.TweenProperty(SideBarL, "size:y", GetViewport().GetVisibleRect().Size.Y, 0.5f)
					.SetTrans(Tween.TransitionType.Cubic)
					.SetEase(Tween.EaseType.Out);
				SkinEditorAni.TweenProperty(SideBarR, "position", new Vector2(SideBarR.Position.X + SideBarR.Size.X, 0), 0.5f)
					.SetTrans(Tween.TransitionType.Cubic)
					.SetEase(Tween.EaseType.Out);
				SkinEditorAni.TweenProperty(SideBarR, "size:y", GetViewport().GetVisibleRect().Size.Y, 0.5f)
					.SetTrans(Tween.TransitionType.Cubic)
					.SetEase(Tween.EaseType.Out);
				SkinEditorAni.TweenProperty(ToolBar, "position:y", ToolBar.Position.Y - ToolBar.Size.Y, 0.5f)
					.SetTrans(Tween.TransitionType.Cubic)
					.SetEase(Tween.EaseType.Out);
				SkinEditorAni.TweenProperty(ControlPanel, "position:y", ControlPanel.Position.Y + ControlPanel.Size.Y, 0.5f)
					.SetTrans(Tween.TransitionType.Cubic)
					.SetEase(Tween.EaseType.Out);
				SkinEditorAni.TweenProperty(Error, "position:y", Error.Position.Y + Error.Size.Y + 50, 0.5f)
					.SetTrans(Tween.TransitionType.Cubic)
					.SetEase(Tween.EaseType.Out);
				SkinEditorAni.TweenProperty(_CurrentScene, "size", GetViewport().GetVisibleRect().Size, 0.5f)
					.SetTrans(Tween.TransitionType.Cubic)
					.SetEase(Tween.EaseType.Out);
				SkinEditorAni.TweenProperty(_CurrentScene, "position", new Vector2(0, 0), 0.5f)
					.SetTrans(Tween.TransitionType.Cubic)
					.SetEase(Tween.EaseType.Out);
				SkinEditorAni.TweenCallback(Callable.From(() => SettingsOperator.toppaneltoggle(true)));
				SkinEditorAni.Chain().TweenCallback(Callable.From(() => _skineditorScene.QueueFree()));
			}
			SkinEditorAni.Play();
		}

		if (Input.IsActionJustPressed("screenshot"))
		{
			var filename = "/screenshot_" + ((int)Directory.GetFiles(SettingsOperator.screenshotdir).Count() + 1) + ".jpg";
			var image = GetViewport().GetTexture().GetImage();
			image.SaveJpg(SettingsOperator.screenshotdir + filename);
			Notify.Post($"saved as screenshot_{(int)Directory.GetFiles(SettingsOperator.screenshotdir).Count() + 1}", SettingsOperator.screenshotdir);
			GD.Print(filename);
		}
		else if (Input.IsActionJustPressed("Skin Editor"))
		{
			_SkinStart = !_SkinStart;
		}
	}
}
