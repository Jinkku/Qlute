using Godot;
using System;

public partial class SongIndication : Label
{
	private Tween Ani { get; set; }
	private string oldText { get; set; }
	private string newText { get; set; }
	private void ReloadSongText()
	{
		var Title = SettingsOperator.Sessioncfg["beatmaptitle"]?.ToString() ?? "No song selected";
		var Artist = SettingsOperator.Sessioncfg["beatmapartist"]?.ToString() ?? "";
		if (Artist == "")
		{
			newText = Title;
		}
		else
		{
			newText = $"{Artist} - {Title}";
		}
		if (oldText != newText)
		{
			var posx = GetViewportRect().Size.X;
			oldText = newText;
			Modulate = new Color(1f, 1f, 1f, 0f);
			Ani?.Kill();
			Ani = CreateTween();
			Ani.SetParallel(true);
			Ani.TweenProperty(this, "modulate:a", 0f, 0.5).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
			Ani.TweenCallback(Callable.From(() => Text = newText)).SetDelay(0.5);
			Ani.TweenProperty(this, "modulate:a", 1f, 0.5).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic).SetDelay(0.5);
			Ani.Play();
			
		}
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		ReloadSongText();
	}
}
