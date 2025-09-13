using Godot;
using System;
using System.Collections;

public partial class BeatmapInfo : Control
{
	private Label Length { get; set; }
	private HBoxContainer Beatmaps { get; set; }
	private int index { get; set; }
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		index = (int)GetMeta("index");
		Length = GetNode<Label>("Pill/Padding/Info/Columns/Details/GridContainer/Length"); ;
		Beatmaps = GetNode<HBoxContainer>("Pill/Padding/Info/Beatmaps");
		foreach (var beat in Browse.BrowseCatalog[index].beatmaps)
		{
			var icon = GD.Load<PackedScene>("res://Panels/BrowseElements/GameModeDifficulty.tscn").Instantiate().GetNode<TextureButton>(".");
			icon.TooltipText = $"Lv. {(beat.count_circles + beat.count_sliders) * SettingsOperator.ppbase}";
			Beatmaps.AddChild(icon);
		}
 		ReloadStats();
	}

	private void ReloadStats()
	{
		Length.Text = $"Length: {TimeSpan.FromSeconds(Browse.BrowseCatalog[index].beatmaps[0].total_length):mm\\:ss}";
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}
	private void _endit()
	{
		GetTree().CurrentScene.GetNode<ColorRect>("Blank-Chan").QueueFree();
		QueueFree();
	}
	private void _close()
	{
		var Animation = CreateTween();
		var Blank = GetTree().CurrentScene.GetNode<ColorRect>("Blank-Chan");
		var BeatmapInfoCard = GetTree().CurrentScene.GetNode<Control>("BeatmapInfo");
		var Back = BeatmapInfoCard.GetNode<Button>("Back");
		Animation.Connect("finished", new Callable(this, nameof(_endit)));
		Animation.SetParallel(true);
		Animation.TweenProperty(Blank, "modulate", new Color(1f, 1f, 1f, 0f), 0.5f)
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.Out);
		Animation.TweenProperty(BeatmapInfoCard, "position", new Vector2(0, GetViewportRect().Size.Y), 0.5f)
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.Out);
		Animation.Play();
	}
}
