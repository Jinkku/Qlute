using Godot;
using System;
using System.IO;
using System.Linq;


public partial class Global : Node
{
	// Called when the node enters the scene tree for the first time.
	public static SettingsOperator SettingsOperator { get; set; }
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
			if (_skineditorEnabled)
			{
				if (SkinEditorAni != null)
				{
					SkinEditorAni.Kill();
				}
				_skineditorScene = GD.Load<PackedScene>("res://Panels/Screens/SkinEditor.tscn").Instantiate().GetNode<Control>(".");
				GetTree().Root.AddChild(_skineditorScene);
				_skineditorScene.Modulate = new Color(1, 1, 1, 0);
				SkinEditorAni = GetTree().CreateTween();
				SkinEditorAni.SetParallel(true);
				SkinEditorAni.TweenProperty(_CurrentScene, "size", new Vector2(GetViewport().GetVisibleRect().Size.X - _skineditorScene.GetNode<PanelContainer>("Creativity").Size.X, GetViewport().GetVisibleRect().Size.Y - _skineditorScene.GetNode<PanelContainer>("ToolBar").Size.Y - _skineditorScene.GetNode<PanelContainer>("ToolBar").Position.Y - _skineditorScene.GetNode<PanelContainer>("ControlPanel").Size.Y), 0.5f)
					.SetTrans(Tween.TransitionType.Cubic)
					.SetEase(Tween.EaseType.Out);
				SkinEditorAni.TweenProperty(_skineditorScene, "modulate", new Color(1, 1, 1, 1), 0.5f)
					.SetTrans(Tween.TransitionType.Cubic)
					.SetEase(Tween.EaseType.Out);
				SkinEditorAni.TweenProperty(_CurrentScene, "position", new Vector2(_skineditorScene.GetNode<PanelContainer>("Creativity").Size.X, _skineditorScene.GetNode<PanelContainer>("ToolBar").Size.Y + _skineditorScene.GetNode<PanelContainer>("ToolBar").Position.Y), 0.5f)
					.SetTrans(Tween.TransitionType.Cubic)
					.SetEase(Tween.EaseType.Out);
				SkinEditorAni.Play();
			}
			else
			{
				if (SkinEditorAni != null)
				{
					SkinEditorAni.Kill();
				}
				SkinEditorAni = GetTree().CreateTween();
				SkinEditorAni.SetParallel(true);
				SkinEditorAni.TweenProperty(_CurrentScene, "size", GetViewport().GetVisibleRect().Size, 0.5f)
					.SetTrans(Tween.TransitionType.Cubic)
					.SetEase(Tween.EaseType.Out);
				SkinEditorAni.TweenProperty(_skineditorScene, "modulate", new Color(1, 1, 1, 0), 0.5f)
					.SetTrans(Tween.TransitionType.Cubic)
					.SetEase(Tween.EaseType.Out);
				SkinEditorAni.TweenProperty(_CurrentScene, "position", new Vector2(0, 0), 0.5f)
					.SetTrans(Tween.TransitionType.Cubic)
					.SetEase(Tween.EaseType.Out);
				SkinEditorAni.Play();
				SkinEditorAni.TweenCallback(Callable.From(() => _skineditorScene.QueueFree()));
			}
		}

		if (Input.IsActionJustPressed("screenshot"))
		{
			var filename = "/screenshot_" + ((int)Directory.GetFiles(SettingsOperator.screenshotdir).Count() + 1) + ".jpg";
			var image = GetViewport().GetTexture().GetImage();
			image.SaveJpg(SettingsOperator.screenshotdir + filename);
			GD.Print(filename);
		}
		else if (Input.IsActionJustPressed("Skin Editor"))
		{
			_SkinStart = !_SkinStart;
		}
	}
}
