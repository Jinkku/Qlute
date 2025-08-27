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

	/// <summary>
	/// This is to check if it's closed because the function don't have that function yet o-o
	/// </summary>
	private void StartTopPanelOpen()
	{
		if (SettingsOperator.TopPanelPosition < 50)
		{
			SettingsOperator.toppaneltoggle();
		}
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
				var SideBarL = _skineditorScene.GetNode<PanelContainer>("Creativity");
				var SideBarR = _skineditorScene.GetNode<PanelContainer>("Extras");
				var ToolBar = _skineditorScene.GetNode<PanelContainer>("ToolBar");
				var ControlPanel = _skineditorScene.GetNode<PanelContainer>("ControlPanel");
				GetTree().Root.AddChild(_skineditorScene);
				_skineditorScene.Modulate = new Color(1, 1, 1, 0);
				SkinEditorAni.TweenProperty(_CurrentScene, "size", new Vector2(GetViewport().GetVisibleRect().Size.X - SideBarL.Size.X - SideBarR.Size.X, GetViewport().GetVisibleRect().Size.Y - ToolBar.Size.Y - ToolBar.Position.Y - ControlPanel.Size.Y), 0.5f)
					.SetTrans(Tween.TransitionType.Cubic)
					.SetEase(Tween.EaseType.Out);
				SkinEditorAni.TweenProperty(_skineditorScene, "modulate", new Color(1, 1, 1, 1), 0.5f)
					.SetTrans(Tween.TransitionType.Cubic)
					.SetEase(Tween.EaseType.Out);
				SkinEditorAni.TweenProperty(_CurrentScene, "position", new Vector2(SideBarL.Size.X, ToolBar.Size.Y + ToolBar.Position.Y), 0.5f)
					.SetTrans(Tween.TransitionType.Cubic)
					.SetEase(Tween.EaseType.Out);
			}
			else
			{
				SkinEditorAni.TweenProperty(_CurrentScene, "size", GetViewport().GetVisibleRect().Size, 0.5f)
					.SetTrans(Tween.TransitionType.Cubic)
					.SetEase(Tween.EaseType.Out);
				SkinEditorAni.TweenProperty(_skineditorScene, "modulate", new Color(1, 1, 1, 0.5f), 0.5f)
					.SetTrans(Tween.TransitionType.Cubic)
					.SetEase(Tween.EaseType.Out);
				SkinEditorAni.TweenProperty(_CurrentScene, "position", new Vector2(0, 0), 0.5f)
					.SetTrans(Tween.TransitionType.Cubic)
					.SetEase(Tween.EaseType.Out);
				SkinEditorAni.TweenCallback(Callable.From(() => StartTopPanelOpen()));
				SkinEditorAni.TweenCallback(Callable.From(() => _skineditorScene.QueueFree()));
				
			}
			SkinEditorAni.Play();

		}

		if (Input.IsActionJustPressed("screenshot"))
		{
			var filename = "/screenshot_" + ((int)Directory.GetFiles(SettingsOperator.screenshotdir).Count() + 1) + ".jpg";
			var image = GetViewport().GetTexture().GetImage();
			image.SaveJpg(SettingsOperator.screenshotdir + filename);
			Notify.Post($"saved as screenshot_{(int)Directory.GetFiles(SettingsOperator.screenshotdir).Count() + 1}",SettingsOperator.screenshotdir);
			GD.Print(filename);
		}
		else if (Input.IsActionJustPressed("Skin Editor"))
		{
			_SkinStart = !_SkinStart;
		}
	}
}
