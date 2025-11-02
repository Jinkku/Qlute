using Godot;
using System;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using System.Net.Http;
using System.Text;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public class CatalogCardLegend {
	public string cover { get; set; }
	public string card { get; set; }
	public string list { get; set; }
	public string slimcover { get; set; }
}
public class CatalogBeatmapInfoLegend
{
	public int id { get; set; }
	public double difficulty_rating { get; set; }
	public int count_circles { get; set; }
	public int count_sliders { get; set; }
	public int? max_combo { get; set; }
	public int ranked { get; set; }
	public int total_length { get; set; }
	public string version { get; set; }
	public double bpm { get; set; }
}
public class BrowseCatalogLegend {
	public int id { get; set; }
	public string artist { get; set; }
	public string title { get; set; }
	public string creator { get; set; }
	public string source { get; set; }
	public string preview_url { get; set; }
	public double last_updated { get; set; }
	public CatalogCardLegend covers { get; set; }
	public List<CatalogBeatmapInfoLegend> beatmaps { get; set; }
}
public partial class Browse : Control
{
	// Called when the node enters the scene tree for the first time.
    public static List<BrowseCatalogLegend> BrowseCatalog = new List<BrowseCatalogLegend>();
	public static HttpRequest BrowseApi { get; set; }
	public static int ScrollVertical { get; set; }
	public int CardSizeX { get; set; }
	public AnimationPlayer Loadinganimation {get ; set; }
	private Tween BlankAni { get; set; }
	// Loading icon
	// This is the icon that will be shown while the API is loading
	// It will be centered on the screen
	public Sprite2D Loadingicon { get; set; }
	public ColorRect Blank {get ; set; }
	private int page { get; set; } = 0;
	private GridContainer BeatmapGrid { get; set; }
	private ScrollContainer BeatmapScroll { get; set; }
	private int BeatmapScrollHalfwayPoint { get; set; }
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
	private double timetextnow { get; set; }
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

	private string ConvertTypetoRank(int rank)
	{
		switch (rank)
		{
			case 1:
				return "Ranked";
			case 4:
				return "Special";
			case 0:
				return "Unranked";
			default:
				return "Unknown";
		}
	}
	private Color ConvertTypetoColor(int rank)
	{
		switch (rank)
		{
			case 1:
				return  new Color(0.2f, 0.8f, 0.2f, 1f); // Green for Ranked
			case 4:
				return new Color(0.8f, 0.8f, 0.2f, 1f); // Yellow for Special
			case 0:
				return new Color(0.8f, 0.2f, 0.2f, 1f); // Red for Unranked
			default:
				return new Color(0.5f, 0.5f, 0.5f, 1f); // Gray for Unknown
		}
	}
	public void StartBrowse(bool noani = false)
	{
		isLoading = true;
		var ranktype = -3;
		if (_ranktype == 0)
		{
			ranktype = 1;
		}
		else if (_ranktype == 1)
		{
			ranktype = 4;
		}

		try
		{
			if (Blank != null)
			{
				Blank.QueueFree();
			}
		}
		catch (Exception e)
		{
			GD.Print("Guessing it already been freed: " + e.Message);
		}
		if (!noani)
		{
			Blank = new ColorRect();
			Blank.Color = new Color(0, 0, 0, 0.5f);
			Blank.AnchorLeft = 0;
			Blank.AnchorTop = 0;
			Blank.AnchorRight = 1;
			Blank.AnchorBottom = 1;
			Blank.ZIndex = 4; // Ensure it is on top of everything
			Blank.Modulate = new Color(0, 0, 0, 0f);
			Blank.Name = "Blanko-Chan";
			AddChild(Blank);
			Blank.Size = new Vector2(Blank.Size.X, Blank.Size.Y - 50);
			if (BlankAni != null)
			{
				BlankAni.Kill();
			}
			BlankAni = CreateTween();
			BlankAni.TweenProperty(Blank, "modulate", new Color(1f, 1f, 1f, 0.5f), 0.5f)
				.SetTrans(Tween.TransitionType.Cubic)
				.SetEase(Tween.EaseType.Out);
			BlankAni.Play();
			Loadingicon = GetNode<Sprite2D>("Loadingicon");
			Loadinganimation = GetNode<AnimationPlayer>("Loadingicon/Loadinganimation");
			Loadingicon.Position = new Vector2((GetViewportRect().Size.X / 2) - (Loadingicon.Texture.GetSize().X / 2), (GetViewportRect().Size.Y / 2) - (Loadingicon.Texture.GetSize().Y / 2)); // Get size of the Loading Icon and center it
			Loadingicon.Visible = true;
			Loadinganimation.Play("loading");
		}
		else
		{
			Blank = null;
		}
		var uritext = "";
		if (_searchText != "")
		{
			uritext = Uri.EscapeDataString(_searchText);
		}
		var uri = $"{ApiOperator.Beatmapapi}/api/v2/search?query={uritext}&status={ranktype}&page={page}";
		GD.Print($"Looking up with: {uri}");
		BrowseApi.Request(uri);
	}

public static async Task
	DownloadImage(string path, Action<ImageTexture> callback)
	{
		try
		{
			// 1. Download the image
			using (var client = new System.Net.Http.HttpClient())
			{
				byte[] imageBytes = await client.GetByteArrayAsync(path);

				// 2. Create a Godot Image object
				Godot.Image image = new Godot.Image();
				using (var stream = new MemoryStream(imageBytes))
				{
					image.LoadJpgFromBuffer(imageBytes);
				}

				// 3. Create a texture and display the image
				ImageTexture texture = new ImageTexture();
				texture.SetImage(image);

				// Call the callback function with the texture
				callback(texture);
				texture.Dispose();
			}
		}
		catch (Exception e)
		{
			GD.PrintErr("Error downloading or processing image: " + e.Message);
		}
	}
	private void _BrowseAPI_finished(long result, long responseCode, string[] headers, byte[] body)
	{
		string BrowseEntries = (string)Encoding.UTF8.GetString(body);
		List<BrowseCatalogLegend> items = JsonSerializer.Deserialize<List<BrowseCatalogLegend>>(BrowseEntries);

		var index = BrowseCatalog.Count;
		BrowseCatalog.AddRange(items);
		if (Blank != null)
		{
			Loadingicon.Visible = false;
			Loadinganimation.Stop();
			if (BlankAni != null)
			{
				BlankAni.Kill();
			}
			BlankAni = CreateTween();
			BlankAni.TweenProperty(Blank, "modulate", new Color(0f, 0f, 0f, 0f), 0.5f)
				.SetTrans(Tween.TransitionType.Cubic)
				.SetEase(Tween.EaseType.Out);
			BlankAni.Play();
			BlankAni.TweenCallback(Callable.From(() => Blank.QueueFree()));
		}

		var beatmapsContainer = GetNode<GridContainer>("BeatmapSec/Scroll/Center/Spacer/Beatmaps");
		if (page < 1)
		{
			foreach (Node child in beatmapsContainer.GetChildren())
			{
				beatmapsContainer.RemoveChild(child);
				child.QueueFree();
			} // Clears up the browse page.
		}



		try
		{
			foreach (BrowseCatalogLegend line in items)
			{
				List<CatalogBeatmapInfoLegend> beatmaps = (List<CatalogBeatmapInfoLegend>)line.beatmaps.OrderBy(d => d.count_circles + d.count_sliders).ToList();
				line.beatmaps = beatmaps;

				var Element = GD.Load<PackedScene>("res://Panels/BrowseElements/Card.tscn").Instantiate().GetNode<Button>(".");
				CardSizeX = (int)Element.Size.X + 5;
				Element.GetNode<Label>("Info/SongTitle").Text = line.title;
				Element.GetNode<Label>("Info/SongArtist").Text = line.artist;
				Element.GetNode<Label>("Info/SongMapper").Text = "mapped by " + line.creator;
				Element.GetNode<PanelContainer>("InfoBar-Base/InfoBar-Space/InfoBar/RankColor").SelfModulate = ConvertTypetoColor(line.beatmaps.First().ranked);
				Element.GetNode<Label>("InfoBar-Base/InfoBar-Space/InfoBar/RankColor/RankText").Text = ConvertTypetoRank(line.beatmaps.First().ranked);
				Element.GetNode<Label>("InfoBar-Base/InfoBar-Space/InfoBar/RankColor/RankText").TooltipText = line.beatmaps.First().difficulty_rating.ToString("0.00");

				Element.GetNode<Label>("InfoBar-Base/InfoBar-Space/InfoBar/LvStartColor/LvStartText").Text = "Lv. " + SettingsOperator.GetLevelRating(line.beatmaps.First().count_circles + line.beatmaps.First().count_sliders, line.beatmaps.First().total_length).ToString("N0");
				Element.GetNode<Label>("InfoBar-Base/InfoBar-Space/InfoBar/LvEndColor/LvEndText").Text = "Lv. " + SettingsOperator.GetLevelRating(line.beatmaps.Last().count_circles + line.beatmaps.Last().count_sliders, line.beatmaps.Last().total_length).ToString("N0");
				Element.SetMeta("pic", line.covers.card);
				Element.SetMeta("beatmap", line.id);
				Element.SetMeta("index", index);
				Element.GetNode<TextureButton>("SongBackgroundPreview/Playbutton").SetMeta("preview_url", "https:" + line.preview_url);
				Element.Modulate = new Color(1f, 1f, 1f, 0f);
				GetNode<GridContainer>("BeatmapSec/Scroll/Center/Spacer/Beatmaps").AddChild(Element);
				index++;
			}
		}
		catch (Exception e)
		{
			GD.Print(e);
		}
		if (index < 1)
		{
			Empty = true;
		}
		isLoading = false;
	}
	// Called every frame. 'delta' is the elapsed time since the previous frame.
	private void _on_back(){
		GetNode<SceneTransition>("/root/Transition").Switch("res://Panels/Screens/home_screen.tscn");
	}
	public override void _Process(double delta)
	{
		var waba = (int)(GetViewportRect().Size.X/CardSizeX);
		if (waba <1){
			waba = 1;
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
		GetNode<GridContainer>("BeatmapSec/Scroll/Center/Spacer/Beatmaps").Columns = waba;
	}
}
