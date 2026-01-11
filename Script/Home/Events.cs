using Godot;
using System;

public partial class Events : TextureButton
{
	private int ID { get; set; }
	private Timer Delay { get; set; }

	private Color NormalModulate { get; set; }

	private EventLegend Event { get; set; }
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		NormalModulate = Modulate;
		Delay = GetNode<Timer>("Delay");
		NextEvent();
		PivotOffset = Size / 2;
	}

	private Texture2D newTexture;
	private Tween Tween;
	private Tween HoverTween;
	private Vector2 inScale = new Vector2(0.5f, 0.5f);
	private Vector2 OutScale = new Vector2(1.5f, 1.5f);
	private Vector2 NormalScale = new Vector2(1f, 1f);
	private float Speed { get; set; } = 0.3f;

	private void TransitionImage(Texture2D image)
	{
		if (image == TextureNormal) return;
		Tween = CreateTween();
		Tween.SetEase(Tween.EaseType.Out);
		Tween.SetTrans(Tween.TransitionType.Cubic);
		Tween.SetParallel(true);
		Tween.TweenProperty(this, "scale", inScale, Speed);
		Tween.TweenProperty(this, "modulate:a", 0f, Speed);
		Tween.TweenCallback(Callable.From(() => TextureNormal = image)).SetDelay(Speed);
		Tween.TweenCallback(Callable.From(() => Scale = OutScale)).SetDelay(Speed);
		Tween.TweenProperty(this, "scale", NormalScale, Speed).SetDelay(Speed);
		Tween.TweenProperty(this, "modulate:a", 1f, Speed).SetDelay(Speed);
	}

	/// <summary>
	/// Processes the next Event from EventData
	/// </summary>
	async private void NextEvent()
	{
		if (ApiOperator.EventData.Count == 0) return;
		EventLegend Event = ApiOperator.EventData[ID];
		if (Event.DownloadedData == null)
		{
			await ApiOperator.DownloadImage(Event.url, (ImageTexture texture) =>
			{
				GD.Print("Loading downloaded image...");
				Event.DownloadedData = texture;
				TransitionImage(texture);
			});
		}
		else
		{
			TransitionImage(Event.DownloadedData);
			GD.Print("Loading preloaded image...");
		}

		ID++;
		if (ID > ApiOperator.EventData.Count - 1)
			ID = 0;
	}

	private void Pressed()
	{
		if (Event.redirect != "")
		{
			OS.ShellOpen(Event.redirect);
		}
	}

	private void Hover()
	{
		Delay.Paused = true;
		GD.Print("hover?");
		HoverTween?.Kill();
		HoverTween = CreateTween();
		HoverTween.SetEase(Tween.EaseType.Out);
		HoverTween.SetTrans(Tween.TransitionType.Cubic);
		HoverTween.TweenProperty(this, "self_modulate", NormalModulate * 1.2f, Speed);
		
		
	}

	private void Unhover()
	{
		Delay.Paused = false;
		HoverTween?.Kill();
		HoverTween = CreateTween();
		HoverTween.SetEase(Tween.EaseType.Out);
		HoverTween.SetTrans(Tween.TransitionType.Cubic);
		HoverTween.TweenProperty(this, "self_modulate", NormalModulate, Speed);
	}
}
