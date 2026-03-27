using Godot;
using System;
using System.Text.Json;
using System.Text;
using System.Collections.Generic;
using System.Linq;

public class CatalogCardLegend
{
	public string? Cover { get; set; }
	public string? Card { get; set; }
	public string? List { get; set; }
	public string? SlimCover { get; set; }

	// JSON property aliases — kept lowercase to match the API response
	[System.Text.Json.Serialization.JsonPropertyName("cover")]
	public string? cover { get => Cover; set => Cover = value; }
	[System.Text.Json.Serialization.JsonPropertyName("card")]
	public string? card { get => Card; set => Card = value; }
	[System.Text.Json.Serialization.JsonPropertyName("list")]
	public string? list { get => List; set => List = value; }
	[System.Text.Json.Serialization.JsonPropertyName("slimcover")]
	public string? slimcover { get => SlimCover; set => SlimCover = value; }
}

public class CatalogBeatmapInfoLegend
{
	public int id { get; set; }
	public double level { get; set; }
	public double pp { get; set; }
	public int count_circles { get; set; }
	public int count_sliders { get; set; }
	public int? max_combo { get; set; }
	public int ranked { get; set; }
	public int total_length { get; set; }
	public string? version { get; set; }
	public double bpm { get; set; }
}

public class BrowseCatalogLegend
{
	public int id { get; set; }
	public string? artist { get; set; }
	public string? title { get; set; }
	public string? creator { get; set; }
	public string? source { get; set; }
	public string? preview_url { get; set; }
	public string? download_url { get; set; }
	public double last_updated { get; set; }
	public CatalogCardLegend? covers { get; set; }
	public List<CatalogBeatmapInfoLegend>? beatmaps { get; set; }
}

public partial class Browse : Control
{
	public static List<BrowseCatalogLegend> BrowseCatalog = new();
	public static HttpRequest BrowseApi { get; set; } = null!;
	public static int ScrollVertical { get; set; }
	public int CardSizeX { get; set; }
	public AnimationPlayer? Loadinganimation { get; set; }
	private Tween? BlankAni { get; set; }
	public Sprite2D? Loadingicon { get; set; }
	public ColorRect? Blank { get; set; }
	private int page { get; set; } = 0;
	private GridContainer BeatmapGrid { get; set; } = null!;
	private ScrollContainer BeatmapScroll { get; set; } = null!;
	private bool ContinuePage { get; set; }
	private bool Empty { get; set; }

	public override void _Ready()
	{
		BeatmapScroll = GetNode<ScrollContainer>("BeatmapSec/Scroll");
		BeatmapGrid = GetNode<GridContainer>("BeatmapSec/Scroll/Center/Spacer/Beatmaps");
		BrowseCatalog.Clear();
		BrowseApi = new HttpRequest();
		BrowseApi.Timeout = 60;
		AddChild(BrowseApi);
		BrowseApi.Connect("request_completed", new Callable(this, nameof(_BrowseAPI_finished)));
		StartBrowse();
	}

	private string _searchText { get; set; } = "";
	private int _ranktype { get; set; }
	private double timetext { get; set; }
	private bool textchanging { get; set; }
	private bool isLoading { get; set; }

	private void _textchanged(string searchText)
	{
		textchanging = true;
		timetext = Extras.GetMilliseconds();
		_searchText = searchText;
	}

	private void _submit(string searchText)
	{
		textchanging = false;
		_searchText = searchText;
		BrowseCatalog.Clear();
		page = 0;
		Empty = false;
		StartBrowse();
	}

	private void _rankchanged(int rank)
	{
		_ranktype = rank;
		BrowseCatalog.Clear();
		page = 0;
		Empty = false;
		StartBrowse();
	}

	private static string ConvertTypetoRank(int rank) => rank switch
	{
		1 => "Ranked",
		4 => "Special",
		0 => "Unranked",
		_ => "Unknown"
	};

	private static Color ConvertTypetoColor(int rank) => rank switch
	{
		1 => new Color(0.2f, 0.8f, 0.2f, 1f),  // Green  – Ranked
		4 => new Color(0.8f, 0.8f, 0.2f, 1f),  // Yellow – Special
		0 => new Color(0.8f, 0.2f, 0.2f, 1f),  // Red    – Unranked
		_ => new Color(0.5f, 0.5f, 0.5f, 1f),  // Gray   – Unknown
	};

	public void StartBrowse(bool noani = false)
	{
		isLoading = true;

		// Map the UI rank index to the API status value
		int ranktype = _ranktype switch
		{
			0 => 1,
			1 => 2,
			_ => 0
		};

		// Safely free any existing overlay
		if (Blank != null && IsInstanceValid(Blank))
		{
			Blank.QueueFree();
			Blank = null;
		}

		if (!noani)
		{
			Blank = new ColorRect
			{
				Color = new Color(0, 0, 0, 0.5f),
				AnchorLeft = 0,
				AnchorTop = 0,
				AnchorRight = 1,
				AnchorBottom = 1,
				ZIndex = 4,
				Modulate = new Color(0, 0, 0, 0f),
				Name = "Blanko-Chan"
			};
			AddChild(Blank);
			Blank.Size = new Vector2(Blank.Size.X, Blank.Size.Y - 50);

			BlankAni?.Kill();
			BlankAni = CreateTween();
			BlankAni.TweenProperty(Blank, "modulate", new Color(1f, 1f, 1f, 0.5f), 0.5f)
				.SetTrans(Tween.TransitionType.Cubic)
				.SetEase(Tween.EaseType.Out);
			BlankAni.Play();

			Loadingicon = GetNode<Sprite2D>("Loadingicon");
			Loadinganimation = GetNode<AnimationPlayer>("Loadingicon/Loadinganimation");

			var iconSize = Loadingicon.Texture.GetSize();
			var viewport = GetViewportRect().Size;
			Loadingicon.Position = new Vector2(
				(viewport.X / 2) - (iconSize.X / 2),
				(viewport.Y / 2) - (iconSize.Y / 2)
			);
			Loadingicon.Visible = true;
			Loadinganimation.Play("loading");
		}

		var uritext = string.IsNullOrEmpty(_searchText)
			? ""
			: Uri.EscapeDataString(_searchText);

		var uri = $"{SettingsOperator.GetSetting("api")}apiv2/search?query={uritext}&status={ranktype}&page={page}";
		GD.Print($"Looking up with: {uri}");
		BrowseApi.Request(uri);
	}

	private void _BrowseAPI_finished(long result, long responseCode, string[] headers, byte[] body)
	{
		// Deserialise — guard against null or empty responses
		List<BrowseCatalogLegend>? items = null;
		try
		{
			var json = Encoding.UTF8.GetString(body);
			items = JsonSerializer.Deserialize<List<BrowseCatalogLegend>>(json);
		}
		catch (Exception e)
		{
			GD.PrintErr($"Failed to parse browse API response: {e.Message}");
		}

		items ??= new List<BrowseCatalogLegend>();

		var index = BrowseCatalog.Count;
		BrowseCatalog.AddRange(items);

		// Hide the loading overlay
		if (Blank != null && IsInstanceValid(Blank))
		{
			if (Loadingicon != null) Loadingicon.Visible = false;
			Loadinganimation?.Stop();

			BlankAni?.Kill();
			BlankAni = CreateTween();
			BlankAni.TweenProperty(Blank, "modulate", new Color(0f, 0f, 0f, 0f), 0.5f)
				.SetTrans(Tween.TransitionType.Cubic)
				.SetEase(Tween.EaseType.Out);
			BlankAni.TweenCallback(Callable.From(() => Blank?.QueueFree()));
			BlankAni.Play();
		}

		var beatmapsContainer = GetNode<GridContainer>("BeatmapSec/Scroll/Center/Spacer/Beatmaps");

		// Clear existing cards on a fresh search (page 0)
		if (page < 1)
		{
			foreach (Node child in beatmapsContainer.GetChildren())
			{
				beatmapsContainer.RemoveChild(child);
				child.QueueFree();
			}
		}

		try
		{
			foreach (BrowseCatalogLegend line in items)
			{
				// Skip entries with no beatmaps
				if (line.beatmaps is not { Count: > 0 }) continue;

				line.beatmaps = line.beatmaps
					.OrderBy(d => d.count_circles + d.count_sliders)
					.ToList();

				var Element = GD.Load<PackedScene>("res://Panels/BrowseElements/Card.tscn")
					.Instantiate()
					.GetNode<CardFunctions>(".");

				// Ensure the preview URL is absolute
				if (!string.IsNullOrEmpty(line.preview_url) && !line.preview_url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
					line.preview_url = "https:" + line.preview_url;

				Element.GetNode<MusicPreview>("SongBackgroundPreview/Playbutton").audioPath = line.preview_url;
				Element.BannerPicture = line.covers?.card;
				Element.BeatmapID = line.id;
				Element.Index = index;

				beatmapsContainer.AddChild(Element);
				CardSizeX = (int)Element.Size.X + 5;

				var first = line.beatmaps.First();
				var last  = line.beatmaps.Last();

				Element.Title.Text  = line.title   ?? string.Empty;
				Element.Artist.Text = line.artist  ?? string.Empty;
				Element.Mapper.Text = $"mapped by {line.creator ?? "unknown"}";
				Element.RankColour.SelfModulate = ConvertTypetoColor(first.ranked);
				Element.RankText.Text = ConvertTypetoRank(first.ranked);
				Element.LvStart.Text = $"Lv. {first.level}";
				Element.LvEnd.Text   = $"Lv. {last.level}";
				Element.Modulate = new Color(1f, 1f, 1f, 0f);

				index++;
			}
		}
		catch (Exception e)
		{
			GD.PrintErr($"Error building browse cards: {e}");
		}

		if (index < 1) Empty = true;
		isLoading = false;
	}

	private void _on_back()
	{
		GetNode<SceneTransition>("/root/Transition").Switch("res://Panels/Screens/home_screen.tscn");
	}

	public override void _Process(double delta)
	{
		if (CardSizeX > 0)
		{
			int columns = Math.Max(1, (int)(GetViewportRect().Size.X / CardSizeX));
			GetNode<GridContainer>("BeatmapSec/Scroll/Center/Spacer/Beatmaps").Columns = columns;
		}

		if (Extras.GetMilliseconds() - timetext > 500 && textchanging)
		{
			textchanging = false;
			_submit(_searchText);
		}

		ScrollVertical = BeatmapScroll.ScrollVertical;

		if (ScrollVertical > BeatmapGrid.Size.Y / 2 && !isLoading && !Empty)
		{
			page++;
			StartBrowse(noani: true);
		}
	}
}