using Godot;
using System;
using System.Collections;

public partial class BeatmapInfo : Control
{
	private Label Length { get; set; }
	private Label Title { get; set; }
	private Label Artist { get; set; }
	private Label Mapper { get; set; }
	private Label Submitted { get; set; }
	private Label Difficulty { get; set; }
	private HBoxContainer Beatmaps { get; set; }
	private int index { get; set; } = -1;
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		if (HasMeta("index"))
		{
			index = (int)GetMeta("index");	
		}
		Title = GetNode<Label>("Pill/Padding/Info/Columns/Info/SongTitle");
		Artist = GetNode<Label>("Pill/Padding/Info/Columns/Info/SongArtist");
		Mapper = GetNode<Label>("Pill/Padding/Info/Columns/Info/SongMapper");
		Submitted = GetNode<Label>("Pill/Padding/Info/Columns/Info/SongRelease");
		Difficulty = GetNode<Label>("Pill/Padding/Info/Difficulty");
		Length = GetNode<Label>("Pill/Padding/Info/Columns/Details/Row/Column1/Value");
		Beatmaps = GetNode<HBoxContainer>("Pill/Padding/Info/Beatmaps");
		var ind = 0;
		foreach (var beat in Browse.BrowseCatalog[index].beatmaps)
		{
			var icon = GD.Load<PackedScene>("res://Panels/BrowseElements/GameModeDifficulty.tscn").Instantiate().GetNode<TextureButton>(".");
			icon.TooltipText = $"Lv. {(beat.count_circles + beat.count_sliders) * SettingsOperator.levelweight}";
			icon.SetMeta("index", ind);
			icon.SetMeta("rootindex", index);
			Beatmaps.AddChild(icon);
			ind++;
		}
 		ReloadStats();
	}

	private void ReloadStats()
	{
		var cache = Browse.BrowseCatalog[index];
		if (index != -1)
		{
			double lastUpdatedSeconds = cache.last_updated / 1000; // example epoch timestamp
			DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
			DateTime date = epoch.AddSeconds(lastUpdatedSeconds);
			string formatted = $"{Extras.GetDayWithSuffix(date.Day)} {date:MMM yyyy}";
			Length.Text = $"{TimeSpan.FromSeconds(cache.beatmaps[0].total_length):mm\\:ss}";
			Title.Text = cache.title;
			Artist.Text = cache.artist;
			Mapper.Text = $"mapped by {cache.creator}";
			Submitted.Text = $"submitted at {formatted}";
			
		}
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
