using Godot;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public partial class BeatmapInfo : Control
{
	public  SettingsOperator SettingsOperator { get; set; }
	private Label Length { get; set; }
	private Label BPM { get; set; }
	private Label NoteCount { get; set; }
	private Label MaxPP { get; set; }
	private Label LevelRating { get; set; }
	private Label Title { get; set; }
	private Label Artist { get; set; }
	private Label Mapper { get; set; }
	private Label Submitted { get; set; }
	private Label Difficulty { get; set; }
	private HBoxContainer Beatmaps { get; set; }
	private List<CatalogBeatmapInfoLegend> BeatmapList { get; set; }
	public static int BeatmapIndex { get; set; } = -1;
	public static int BeatmapIndexH { get; set; } = -1;
	private int NoteCountTotal { get; set; }
	public bool Downloaded { get; set; } = false;
	public int BeatmapID { get; set; }
	private Button Download { get; set; }
	private Button GoShortcut { get; set; }
	public int Index { get; set; }
	private int ID { get; set; }
	
	private void _play()
	{
		SettingsOperator.SelectSongID(ID);
		GetNode<SceneTransition>("/root/Transition").Switch("res://Panels/Screens/song_select.tscn");
	}

    public override void _ExitTree()
	{
		BeatmapIndex = -1;
		BeatmapIndexH = -1;
	}
	public override void _PhysicsProcess(double delta)
	{
		Existance();
	}

	/// <summary>
	/// Checks if it's already downloaded.
	/// </summary>
	private void Existance()
	{
		var beatmap = SettingsOperator.Beatmaps.FirstOrDefault(b => b.Osubeatidset == BeatmapID);
		Downloaded = beatmap != null;
		if (Downloaded)
		{
			ID = beatmap.ID;
			Download.Visible = false;
			GoShortcut.Visible = true;
		}
		else
		{
			Download.Visible = true;
			GoShortcut.Visible = false;
		}
	}
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		SettingsOperator = GetNode<SettingsOperator>("/root/SettingsOperator");
		Title = GetNode<Label>("Pill/Padding/Info/Columns/Info/SongTitle");
		Artist = GetNode<Label>("Pill/Padding/Info/Columns/Info/SongArtist");
		Mapper = GetNode<Label>("Pill/Padding/Info/Columns/Info/SongMapper");
		Submitted = GetNode<Label>("Pill/Padding/Info/Columns/Info/SongRelease");
		Difficulty = GetNode<Label>("Pill/Padding/Info/Difficulty");
		Length = GetNode<Label>("Pill/Padding/Info/Columns/Details/Row/Column1/Value");
		BPM = GetNode<Label>("Pill/Padding/Info/Columns/Details/Row/Column2/Value");
		NoteCount = GetNode<Label>("Pill/Padding/Info/Columns/Details/Row/Column3/Value");
		MaxPP = GetNode<Label>("Pill/Padding/Info/Columns/Details/Row/Column4/Value");
		LevelRating = GetNode<Label>("Pill/Padding/Info/Columns/Details/Row/Column5/Value");
		Beatmaps = GetNode<HBoxContainer>("Pill/Padding/Info/Beatmaps");
		GoShortcut = GetNode<Button>("Pill/Padding/Info/Columns/Info/HBoxContainer/Play");
		Download = GetNode<Button>("Pill/Padding/Info/Columns/Info/HBoxContainer/Download");


		BeatmapList = Browse.BrowseCatalog[Index].beatmaps;


		var ind = 0;
		foreach (var beat in BeatmapList)
		{
			var icon = GD.Load<PackedScene>("res://Panels/BrowseElements/GameModeDifficulty.tscn").Instantiate().GetNode<TextureButton>(".");
			icon.SetMeta("index", ind);
			icon.SetMeta("rootindex", Index);
			Beatmaps.AddChild(icon);
			ind++;
		}

		if (BeatmapList.Count > 0)
		{
			BeatmapIndex = 0;
			NoteCountTotal = BeatmapList[BeatmapIndex].count_circles + BeatmapList[BeatmapIndex].count_sliders;
		}


		ReloadStats();
	}

	private void _download()
	{
		ApiOperator.DownloadBeatmap(BeatmapID, Index);
	}

	private void ReloadStats()
	{
		var cache = Browse.BrowseCatalog[Index];
		if (Index != -1)
		{
			NoteCountTotal = BeatmapList[BeatmapIndex].count_circles + BeatmapList[BeatmapIndex].count_sliders;
			double lastUpdatedSeconds = cache.last_updated / 1000; // example epoch timestamp
			DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
			DateTime date = epoch.AddSeconds(lastUpdatedSeconds);
			string formatted = $"{Extras.GetDayWithSuffix(date.Day)} {date:MMM yyyy}";
			Length.Text = $"{TimeSpan.FromSeconds(BeatmapList[BeatmapIndex].total_length):mm\\:ss}";
			BPM.Text = BeatmapList[BeatmapIndex].bpm.ToString("N0");
			MaxPP.Text = $"{SettingsOperator.Get_ppvalue(NoteCountTotal, 0, 0, 0, 1, NoteCountTotal, BeatmapList[BeatmapIndex].total_length).ToString("N0")}pp";
			NoteCount.Text = NoteCountTotal.ToString("N0");
			LevelRating.Text = (SettingsOperator.GetLevelRating(NoteCountTotal, BeatmapList[BeatmapIndex].total_length)).ToString("N0");
			Title.Text = cache.title;
			Artist.Text = cache.artist;
			Mapper.Text = $"mapped by {cache.creator}";
			Submitted.Text = $"submitted at {formatted}";
		}
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		ReloadStats();
		if (BeatmapIndexH != -1)
		{
			Difficulty.Text = BeatmapList[BeatmapIndexH].version;
		}
		else if (BeatmapIndexH == -1 && BeatmapIndex != -1)
		{
			Difficulty.Text = BeatmapList[BeatmapIndex].version;
		}
		else
		{
			Difficulty.Text = "";
		}
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
