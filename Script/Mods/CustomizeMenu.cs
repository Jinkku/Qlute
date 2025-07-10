using Godot;
using System;

public partial class CustomizeMenu : PanelContainer
{
	private Tween _customizeTween;
	private PanelContainer _modmenuPanel;
	private int _customizePanelHeight = 0;
	private int _modmenuPanelHeight = 0;
	private bool _isCustomizing = false;
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		_modmenuPanel = GetNode<PanelContainer>("../Mods");
		_customizePanelHeight = (int)Position.Y;
		_modmenuPanelHeight = (int)_modmenuPanel.Position.Y;
	}
	private void _customize()
	{
		_isCustomizing = !_isCustomizing;
		if (_customizeTween != null)
		{
			_customizeTween.Kill();
		}
		_customizeTween = CreateTween();
		_customizeTween.SetParallel(true);
		if (_isCustomizing)
		{
			// Move the mod menu panel down
			_customizeTween.TweenProperty(_modmenuPanel, "size", new Vector2(_modmenuPanel.Size.X - 400, _modmenuPanel.Size.Y), 0.5f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
			_customizeTween.TweenProperty(this, "position", new Vector2(Position.X - 400, Position.Y), 0.5f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		}
		else
		{
			// Move the mod menu panel back to its original position
			_customizeTween.TweenProperty(_modmenuPanel, "size", new Vector2(_modmenuPanel.Size.X + 400, _modmenuPanel.Size.Y), 0.5f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
			_customizeTween.TweenProperty(this, "position", new Vector2(Position.X + 400, Position.Y), 0.5f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		}
		_customizeTween.Play();
	}
}
