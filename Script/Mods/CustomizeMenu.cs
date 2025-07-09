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

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
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
			Modulate = new Color(1f, 1f, 1f, 0f);
			_customizeTween.TweenProperty(_modmenuPanel, "position", new Vector2(_modmenuPanel.Position.X, _modmenuPanel.Position.Y - Size.Y - 15), 0.5f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
			//_customizeTween.TweenProperty(_modmenuPanel, "modulate", new Color(1f, 1f, 1f, 0.5f), 0.5f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
			_customizeTween.TweenProperty(this, "modulate", new Color(1f, 1f, 1f, 1f), 0.3f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
			// Move the customize panel up
			_customizeTween.TweenProperty(this, "position", new Vector2(Position.X, _customizePanelHeight + Size.Y + 15), 0.5f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
			GetTree().CurrentScene.GetNode<Button>("Title/Info/Customize").Text = "Close Customize"; // Hide Customize
		}
		else
		{
			// Move the mod menu panel back to its original position
			_customizeTween.TweenProperty(_modmenuPanel, "position", new Vector2(_modmenuPanel.Position.X, _modmenuPanelHeight), 0.5f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
			//_customizeTween.TweenProperty(_modmenuPanel, "modulate", new Color(1f, 1f, 1f, 1f), 0.5f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
			_customizeTween.TweenProperty(this, "modulate", new Color(1f, 1f, 1f, 0f), 0.3f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
			// Move the customize panel back to its original position
			_customizeTween.TweenProperty(this, "position", new Vector2(Position.X, _customizePanelHeight), 0.5f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
			GetTree().CurrentScene.GetNode<Button>("Title/Info/Customize").Text = "Customize"; // Open Customize
		}
		_customizeTween.Play();
	}
}
