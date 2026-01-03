using Godot;
using System;
using System.Collections.Generic;

public partial class PauseMenu : Control
{
	private SettingsOperator SettingsOperator { get; set; }
	private Tween PauseTween { get; set; }
	private int MaxChild {get;set;}
	private int Childint { get; set; }
    public override void _ExitTree()
    {
        Cursor.CursorVisible = false;
    }
	private void Countdownshow()
	{
		var countdown = GD.Load<PackedScene>("res://Panels/Screens/Countdown.tscn").Instantiate();
		GetTree().Root.AddChild(countdown);
	}
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		Cursor.CursorVisible = true;
		SettingsOperator = GetNode<SettingsOperator>("/root/SettingsOperator");
		GetTree().Paused = true;
		Modulate = new Color(0f, 0f, 0f, 0f);
		PauseTween?.Kill();
		PauseTween = CreateTween();
		PauseTween.TweenProperty(this, "modulate", new Color(1f, 1f, 1f, 1f), 0.4f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		PauseTween.Play();
		MaxChild = GetNode<VBoxContainer>("PanelContainer/VBoxContainer").GetChildCount();
		Childint = 0;
		if (Gameplay.Dead)
		{
			GetNode<Button>("PanelContainer/VBoxContainer/Continue").Visible = false;
			GetNode<Label>("PauseLabel/Text").Visible = true;
			GetNode<Label>("PauseLabel").Text = "Game Over";
		}
	}

	private void FadeOut() // Using this later, it's supposed to be a fade out animation for when you press the pause button but this is glitchy right now.
	{
		QueueFree();
		PauseTween?.Kill();
		PauseTween = CreateTween();
		PauseTween.TweenProperty(this, "modulate", new Color(1f,1f,1f,0f), 0.4f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		PauseTween.Connect("finished", new Callable(this, nameof(QueueFree)));
	}
	// Called every frame. 'delta' is the elapsed time since the previous frame.
	private void _continue()
	{
		Countdownshow();
		FadeOut();
	}
	private void _resetreplay()
	{
		SettingsOperator.SpectatorMode = false;
	}
	private void _retry()
	{
		GetTree().Paused = false;
		FadeOut();
		BeatmapBackground.FlashEnable = true;
		SettingsOperator.toppaneltoggle(true);
		GetNode<SceneTransition>("/root/Transition").Switch("res://Panels/Screens/SongLoadingScreen.tscn");
	}
	private Control FocusOwner { get; set; }
	public override void _Process(double delta)
	{
		if (Input.IsActionJustPressed("Save Replay"))
		{
			Replay.SaveReplay();
		}
		else if (Input.IsActionJustPressed("View Replay"))
		{
			Replay.SaveReplay();
			Replay.ReloadReplay(Replay.FilePath);
			_retry();
		}




		if ((Input.IsActionJustPressed("ui_up") || Input.IsActionJustPressed("ui_down")) && GetViewport().GuiGetFocusOwner() == null)
		{
			foreach (Button Node in GetNode<VBoxContainer>("PanelContainer/VBoxContainer").GetChildren())
			{
				if (Node.Visible)
				{
					Node.GrabFocus();
					break;
				}
			}
		}

		if (Input.IsActionJustPressed("pausemenu") && !Gameplay.Dead)
		{
			_continue();
		}
		else if (Input.IsActionJustPressed("pausemenu") && Gameplay.Dead)
		{
			Modulate = new Color(1f, 0.5f, 0.5f, 1f);
			PauseTween?.Kill();
			PauseTween = CreateTween();
			PauseTween.TweenProperty(this, "modulate", new Color(1f, 1f, 1f, 1f), 0.4f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
			PauseTween.Play();
		}
		else if (Input.IsActionJustPressed("retry"))
		{
			_retry();
		}
	}
	private void _exit(){
			GetTree().Paused = false;
			FadeOut();
			BeatmapBackground.FlashEnable = true;
			SettingsOperator.toppaneltoggle(true);
			GetNode<SceneTransition>("/root/Transition").Switch("res://Panels/Screens/song_select.tscn");
	}
}
