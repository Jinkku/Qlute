using Godot;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text;
using System.Collections.Generic;
using System.Linq;

public class CatalogCardLegend
{
	public string? cover { get; set; }
	public string? card { get; set; }
	public string? list { get; set; }
	public string? slimcover { get; set; }
}

public class CatalogBeatmapInfoLegend
{
	public int id { get; set; }
	public double level { get; set; }
	[JsonPropertyName("pp")] public float? ppRaw { get; set; }
	[JsonIgnore] public float pp => ppRaw ?? 0;
	public int count_circles { get; set; }
	public int count_sliders { get; set; }
	public int? max_combo { get; set; }
	public int ranked { get; set; }
	public int total_length { get; set; }
	public string? version { get; set; }
	public double bpm { get; set; }
	public long playcount { get; set; }
	/// <summary>Only in the Recommended category: this difficulty matches the player's level.</summary>
	public bool recommended { get; set; }
}

/// <summary>apiv2/recommendation (Chikara/recommend.py): the level the server thinks the player is comfortable at.</summary>
public class RecommendationLegend
{
	public double target_pp { get; set; }
	public double min_pp { get; set; }
	public double max_pp { get; set; }
	public int level { get; set; }
	public int min_level { get; set; }
	public int max_level { get; set; }
	public int based_on { get; set; }
}

/// <summary>One set from apiv2/search (Chikara/catalog.py set_json).</summary>
public class BrowseCatalogLegend
{
	public int id { get; set; }
	public string? artist { get; set; }
	public string? artist_unicode { get; set; }
	public string? title { get; set; }
	public string? title_unicode { get; set; }
	public string? creator { get; set; }
	public string? source { get; set; }
	public string? tags { get; set; }
	public double bpm { get; set; }
	public string? preview_url { get; set; }
	public string? download_url { get; set; }
	public double last_updated { get; set; }
	public int genre { get; set; }
	public string? genre_name { get; set; }
	public int language { get; set; }
	public string? language_name { get; set; }
	public bool nsfw { get; set; }
	public bool video { get; set; }
	public bool storyboard { get; set; }
	public long play_count { get; set; }
	/// <summary>The mapper's linked Qlute account (claimed / self-uploaded sets), else empty.</summary>
	public string? mapper_user { get; set; }
	public bool favorited { get; set; }
	public int favourite_count { get; set; }
	public CatalogCardLegend? covers { get; set; }
	public List<CatalogBeatmapInfoLegend>? beatmaps { get; set; }
}

/// <summary>apiv2/beatmapfilters</summary>
public class CatalogFilterLists
{
	public class Option
	{
		public JsonElement id { get; set; }
		public string name { get; set; } = "";
		public bool desc { get; set; }
		public string Id => id.ValueKind == JsonValueKind.Number ? id.GetInt32().ToString() : id.GetString() ?? "";
	}
	public List<Option>? genres { get; set; }
	public List<Option>? languages { get; set; }
	public List<Option>? sorts { get; set; }
}

/// <summary>
/// The online beatmap listing, styled after the website's /beatmaps page. Every filter maps to an
/// apiv2/search parameter (see Chikara/catalog.py); the lists come from apiv2/beatmapfilters.
/// </summary>
public partial class Browse : Control
{
	public static List<BrowseCatalogLegend> BrowseCatalog = new();
	public static HttpRequest BrowseApi { get; set; } = null!;
	public static int ScrollVertical { get; set; }

	private const int PerPage = 50;
	private const int CardWidth = 340; // catalog.css: minmax(340px, 1fr)
	private const int Gap = 10;
	private const string Mine = "mine";
	/// <summary>Video / storyboard filters and badges (Browse, cards, beatmap info). Off until they're supported in game.</summary>
	public static readonly bool ShowVideoStoryboard = false;
	private const string Rec = "rec";       // "Recommended Difficulty": apiv2/search?recommended=1
	private static readonly Opt MatchSort = new("match", "Best match", false);
	private static RecommendationLegend? RecProfile;
	private static string RecLabel => RecProfile != null ? $"Recommended Difficulty (Lv. {RecProfile.level})" : "Recommended Difficulty";

	// Filter state survives leaving the screen, like the website keeps it in the URL.
	private static class State
	{
		public static string Query = "";
		public static string Status = "1";      // "1" ranked, "2" special, "0" unranked, "mine"
		public static string Genre = "";
		public static string Language = "";
		public static string Explicit = "show";
		public static HashSet<string> Extra = new();
		public static string Played = "";
		public static string Sort = "newest";
		public static string Dir = "";          // "" = the sort's default direction
		public static bool FiltersOpen = true;
	}

	private record Opt(string Id, string Name, bool Desc = true);

	// Same ids/order as Chikara/catalog.py; replaced by apiv2/beatmapfilters when it answers.
	private static List<Opt> Genres = new()
	{
		new("1", "Unspecified"), new("2", "Video Game"), new("3", "Anime"), new("4", "Rock"), new("5", "Pop"),
		new("6", "Other"), new("7", "Novelty"), new("9", "Hip Hop"), new("10", "Electronic"), new("11", "Metal"),
		new("12", "Classical"), new("13", "Folk"), new("14", "Jazz"),
	};
	private static List<Opt> Languages = new()
	{
		new("2", "English"), new("3", "Japanese"), new("4", "Chinese"), new("6", "Korean"), new("7", "French"),
		new("8", "German"), new("11", "Italian"), new("10", "Spanish"), new("9", "Swedish"), new("12", "Russian"),
		new("13", "Polish"), new("5", "Instrumental"), new("1", "Unspecified"), new("14", "Other"),
	};
	private static List<Opt> Sorts = new()
	{
		new("newest", "Newest"), new("title", "Title", false), new("artist", "Artist", false),
		new("mapper", "Mapper", false), new("difficulty", "Difficulty"), new("bpm", "BPM"),
		new("length", "Length"), new("plays", "Plays"),
	};

	private static readonly Color Dim = new(1, 1, 1, 0.55f);
	private static readonly Color LabelColour = new(0.549f, 0.863f, 1f);
	private static readonly Color AccentColour = new(0.953f, 0.729f, 1f);

	private ScrollContainer BeatmapScroll = null!;
	private GridContainer BeatmapGrid = null!;
	private VBoxContainer Filters = null!;
	private HBoxContainer SortOptions = null!;
	private Button FiltersButton = null!;
	private LineEdit SearchBox = null!;
	private Label Hint = null!;
	private Button ClearFilters = null!;
	private TextureRect Backdrop = null!;
	private Sprite2D Loadingicon = null!;
	private AnimationPlayer Loadinganimation = null!;
	private HttpRequest FilterApi = null!;
	private HttpRequest RecApi = null!;
	private Label RecInfo = null!;
	private Font BoldFont = null!;

	private int page;
	private bool isLoading;
	private bool done;
	private int request;
	private int pendingRequest;
	private double timetext;
	private bool textchanging;
	private double lastFail = -1e9;

	private static StyleBoxFlat? chipOff, chipHover, sortOff, sortHover, sortOn, rowLine;
	private static StyleBox? chipOn;
	private static readonly Dictionary<Color, ImageTexture> Dots = new();

	private static bool LoggedIn => SettingsOperator.SessionConfig.Loggedin
		&& !string.IsNullOrEmpty(ApiOperator.Username) && ApiOperator.Username != "Guest";

	public override void _Ready()
	{
		const string content = "BeatmapSec/Scroll/Page/Content/";
		BeatmapScroll = GetNode<ScrollContainer>("BeatmapSec/Scroll");
		BeatmapGrid   = GetNode<GridContainer>(content + "Beatmaps");
		Filters       = GetNode<VBoxContainer>(content + "Panel/Inner/Top/VBox/Filters");
		SortOptions   = GetNode<HBoxContainer>(content + "Panel/Inner/SortBar/HBox/Track/SortOptions");
		FiltersButton = GetNode<Button>(content + "Panel/Inner/Top/VBox/SearchRow/FiltersButton");
		SearchBox     = GetNode<LineEdit>(content + "Panel/Inner/Top/VBox/SearchRow/SearchBox");
		Backdrop      = GetNode<TextureRect>(content + "Panel/Backdrop");
		Hint          = GetNode<Label>(content + "Hint");
		ClearFilters  = GetNode<Button>(content + "ClearFilters");
		Loadingicon   = GetNode<Sprite2D>("Loadingicon");
		Loadinganimation = GetNode<AnimationPlayer>("Loadingicon/Loadinganimation");
		BoldFont = GD.Load<Font>("res://Fonts/font-bold.otf");

		if ((State.Status == Mine || State.Status == Rec) && !LoggedIn) State.Status = "1";
		if (State.Status != Rec && State.Sort == MatchSort.Id) { State.Sort = "newest"; State.Dir = ""; }

		// The line explaining the recommendation, above the cards.
		RecInfo = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, Visible = false };
		RecInfo.AddThemeFontSizeOverride("font_size", 14);
		RecInfo.AddThemeColorOverride("font_color", new Color(1, 1, 1, 0.85f));
		BeatmapGrid.GetParent().AddChild(RecInfo);
		BeatmapGrid.GetParent().MoveChild(RecInfo, BeatmapGrid.GetIndex());
		if (!LoggedIn) State.Played = "";
		SearchBox.Text = State.Query;
		FiltersButton.SetPressedNoSignal(State.FiltersOpen);
		Filters.Visible = State.FiltersOpen;

		BrowseCatalog.Clear();
		BrowseApi = new HttpRequest { Timeout = 60 };
		AddChild(BrowseApi);
		BrowseApi.RequestCompleted += _BrowseAPI_finished;

		FilterApi = new HttpRequest { Timeout = 20 };
		AddChild(FilterApi);
		FilterApi.RequestCompleted += _FilterAPI_finished;
		FilterApi.Request($"{SettingsOperator.GetSetting("api")}apiv2/beatmapfilters");

		// The level on the "Recommended Difficulty (Lv. X)" chip.
		RecApi = new HttpRequest { Timeout = 20 };
		AddChild(RecApi);
		RecApi.RequestCompleted += _RecAPI_finished;
		if (LoggedIn)
			RecApi.Request($"{SettingsOperator.GetSetting("api")}apiv2/recommendation", AuthHeaders());

		BuildFilters();
		Restart();
	}

	// ------------------------------------------------------------------ filter UI

	private static StyleBoxFlat Box(Color bg, float radius, float padX, float padY, Color? border = null, int bottomLine = 0, int borderWidth = 1)
	{
		var s = new StyleBoxFlat
		{
			BgColor = bg,
			ContentMarginLeft = padX, ContentMarginRight = padX, ContentMarginTop = padY, ContentMarginBottom = padY,
			CornerDetail = Mathf.Clamp((int)radius, 8, 24), // smooth pill ends (default 8 looks polygonal)
		};
		s.SetCornerRadiusAll((int)radius);
		if (border is Color b)
		{
			s.BorderColor = b;
			s.SetBorderWidthAll(borderWidth);
		}
		if (bottomLine > 0)
		{
			s.BorderColor = AccentColour;
			s.BorderWidthBottom = bottomLine;
		}
		return s;
	}

	/// <summary>Filter chip border (px); the same on every state so chips don't change size when picked.</summary>
	private const int ChipBorder = 1;

	private static void EnsureStyles()
	{
		if (chipOff != null) return;
		chipOff   = Box(new Color(1, 1, 1, 0), 999, 12, 5, new Color(1, 1, 1, 0), 0, ChipBorder);
		chipHover = Box(new Color(1, 1, 1, 0.07f), 999, 12, 5, new Color(1, 1, 1, 0), 0, ChipBorder);
		// one smooth outline (PillBox): a StyleBoxFlat border is lighter where its straight and round parts meet
		chipOn    = new PillBox(new Color(0.72f, 0.5f, 0.82f, 0.26f), new Color(0.953f, 0.729f, 1f, 0.4f), 12, 5, ChipBorder)
			{ GlowColor = new Color(0.953f, 0.729f, 1f, 0.12f), GlowSize = 8 };
		sortOff   = Box(new Color(1, 1, 1, 0), 8, 13, 6);
		sortHover = Box(new Color(1, 1, 1, 0.063f), 8, 13, 6);
		sortOn    = Box(new Color(1, 1, 1, 0.11f), 8, 13, 6, null, 2);
		rowLine = new StyleBoxFlat { DrawCenter = false, BorderColor = new Color(1, 1, 1, 0.06f), BorderWidthTop = 1, ContentMarginTop = 6, ContentMarginBottom = 6 };
	}

	/// <summary>A small filled circle, the "on" marker in front of a selected chip.</summary>
	private static ImageTexture Dot(Color colour)
	{
		if (Dots.TryGetValue(colour, out var tex)) return tex;
		const int size = 12; // drawn at 2x and shown at 6px
		var img = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
		var c = (size - 1) / 2f;
		for (int y = 0; y < size; y++)
			for (int x = 0; x < size; x++)
			{
				var d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
				var a = Mathf.Clamp(size / 2f - d, 0, 1);
				img.SetPixel(x, y, new Color(colour, colour.A * a));
			}
		return Dots[colour] = ImageTexture.CreateFromImage(img);
	}

	private Button Chip(string text, bool on, Color dot, Action pressed)
	{
		EnsureStyles();
		var b = new Button { Text = text, FocusMode = FocusModeEnum.None, MouseDefaultCursorShape = CursorShape.PointingHand };
		b.AddThemeStyleboxOverride("normal", on ? chipOn! : chipOff!);
		b.AddThemeStyleboxOverride("hover", on ? chipOn! : chipHover!);
		b.AddThemeStyleboxOverride("pressed", chipOn!);
		b.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
		b.AddThemeFontSizeOverride("font_size", 14);
		b.AddThemeColorOverride("font_color", on ? Colors.White : Dim);
		b.AddThemeColorOverride("font_hover_color", Colors.White);
		b.AddThemeColorOverride("font_pressed_color", Colors.White);
		if (on)
		{
			b.AddThemeFontOverride("font", BoldFont);
			b.Icon = Dot(dot);
			b.AddThemeConstantOverride("icon_max_width", 6);
			b.AddThemeConstantOverride("h_separation", 7);
		}
		b.Pressed += pressed;
		return b;
	}

	private void AddRow(string label, IEnumerable<Button> chips)
	{
		var row = new PanelContainer();
		if (Filters.GetChildCount() > 0) row.AddThemeStyleboxOverride("panel", rowLine!);
		else row.AddThemeStyleboxOverride("panel", new StyleBoxFlat { DrawCenter = false, ContentMarginTop = 6, ContentMarginBottom = 6 });
		var h = new HBoxContainer();
		h.AddThemeConstantOverride("separation", 10);
		var l = new Label
		{
			Text = label.ToUpperInvariant(),
			CustomMinimumSize = new Vector2(96, 30),
			VerticalAlignment = VerticalAlignment.Center,
			SizeFlagsVertical = SizeFlags.ShrinkBegin,
		};
		l.AddThemeFontOverride("font", BoldFont);
		l.AddThemeFontSizeOverride("font_size", 11);
		l.AddThemeColorOverride("font_color", LabelColour);
		var flow = new HFlowContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		flow.AddThemeConstantOverride("h_separation", 6);
		flow.AddThemeConstantOverride("v_separation", 4);
		foreach (var c in chips) flow.AddChild(c);
		h.AddChild(l);
		h.AddChild(flow);
		row.AddChild(h);
		Filters.AddChild(row);
	}

	/// <summary>Single-choice row: clicking a chip sets <paramref name="set"/> and reloads.</summary>
	private IEnumerable<Button> Radio(IEnumerable<Opt> opts, string current, Action<string> set, Func<string, Color>? dot = null) =>
		opts.Select(o => Chip(o.Name, o.Id == current, dot?.Invoke(o.Id) ?? AccentColour, () =>
		{
			if (o.Id == current) return;
			set(o.Id);
			Reload();
		})).ToList();

	private void BuildFilters()
	{
		EnsureStyles();
		foreach (Node c in Filters.GetChildren()) { Filters.RemoveChild(c); c.QueueFree(); }

		var status = new List<Opt> { new("1", "Ranked"), new("2", "Special"), new("0", "Unranked") };
		if (LoggedIn) { status.Add(new(Rec, RecLabel)); status.Add(new(Mine, "Yours")); }
		AddRow("Category", Radio(status, State.Status, v =>
		{
			// Recommended sorts by closeness to the player's level unless they pick another sort.
			if (v == Rec && State.Sort == "newest" && State.Dir == "") State.Sort = MatchSort.Id;
			if (v != Rec && State.Sort == MatchSort.Id) { State.Sort = "newest"; State.Dir = ""; }
			State.Status = v;
		}, id => id switch
		{
			Mine => LabelColour,
			Rec => AccentColour,
			_ => CardFunctions.StatusColour(int.Parse(id)),
		}));

		var any = new Opt("", "Any");
		AddRow("Genre", Radio(Genres.Prepend(any), State.Genre, v => State.Genre = v));
		AddRow("Language", Radio(Languages.Prepend(any), State.Language, v => State.Language = v));
		AddRow("Explicit", Radio(new[] { new Opt("show", "Show"), new Opt("hide", "Hide") }, State.Explicit, v => State.Explicit = v));
		if (ShowVideoStoryboard) AddRow("Extra", new[] { new Opt("video", "Has video"), new Opt("storyboard", "Has storyboard") }.Select(o =>
			Chip(o.Name, State.Extra.Contains(o.Id), AccentColour, () =>
			{
				if (!State.Extra.Remove(o.Id)) State.Extra.Add(o.Id);
				Reload();
			})).ToList());
		if (LoggedIn && State.Status != Rec) // Recommended only shows maps you haven't played
			AddRow("Played", Radio(new[] { any, new Opt("played", "Played"), new Opt("unplayed", "Unplayed") }, State.Played, v => State.Played = v));

		BuildSorts();

		var count = FilterCount();
		FiltersButton.Text = count > 0 ? $"≡  Filters ({count})" : "≡  Filters";
	}

	private static int FilterCount() =>
		new[] { State.Genre, State.Language, State.Played }.Count(s => s != "")
		+ (ShowVideoStoryboard && State.Extra.Count > 0 ? 1 : 0)
		+ (State.Explicit == "hide" ? 1 : 0);

	private IEnumerable<Opt> ShownSorts => State.Status == Rec ? Sorts.Prepend(MatchSort) : Sorts;
	private bool SortDesc() => State.Dir != "" ? State.Dir == "desc" : ShownSorts.FirstOrDefault(s => s.Id == State.Sort)?.Desc ?? true;

	private void BuildSorts()
	{
		foreach (Node c in SortOptions.GetChildren()) { SortOptions.RemoveChild(c); c.QueueFree(); }
		foreach (var s in ShownSorts)
		{
			bool on = s.Id == State.Sort;
			var b = new Button
			{
				Text = on ? $"{s.Name} {(SortDesc() ? "↓" : "↑")}" : s.Name,
				FocusMode = FocusModeEnum.None,
				MouseDefaultCursorShape = CursorShape.PointingHand,
				TooltipText = on ? $"Sorted {(SortDesc() ? "high to low" : "low to high")}; click to flip" : "",
			};
			b.AddThemeStyleboxOverride("normal", on ? sortOn! : sortOff!);
			b.AddThemeStyleboxOverride("hover", on ? sortOn! : sortHover!);
			b.AddThemeStyleboxOverride("pressed", sortOn!);
			b.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
			b.AddThemeFontSizeOverride("font_size", 14);
			b.AddThemeColorOverride("font_color", on ? Colors.White : Dim);
			b.AddThemeColorOverride("font_hover_color", Colors.White);
			if (on) b.AddThemeFontOverride("font", BoldFont);
			b.Pressed += () =>
			{
				if (on) State.Dir = SortDesc() ? "asc" : "desc";
				else { State.Sort = s.Id; State.Dir = ""; }
				Reload();
			};
			SortOptions.AddChild(b);
		}
	}

	private void _filterstoggled(bool open)
	{
		State.FiltersOpen = open;
		Filters.Visible = open;
	}

	private void _clearfilters()
	{
		State.Genre = State.Language = State.Played = "";
		State.Extra.Clear();
		State.Explicit = "show";
		Restart();
	}

	private void _FilterAPI_finished(long result, long responseCode, string[] headers, byte[] body)
	{
		if (result != (long)HttpRequest.Result.Success || responseCode != 200) return;
		try
		{
			var lists = JsonSerializer.Deserialize<CatalogFilterLists>(Encoding.UTF8.GetString(body));
			if (lists == null) return;
			if (lists.genres is { Count: > 0 }) Genres = lists.genres.Select(o => new Opt(o.Id, o.name)).ToList();
			if (lists.languages is { Count: > 0 }) Languages = lists.languages.Select(o => new Opt(o.Id, o.name)).ToList();
			if (lists.sorts is { Count: > 0 }) Sorts = lists.sorts.Select(o => new Opt(o.Id, o.name, o.desc)).ToList();
			BuildFilters();
		}
		catch (Exception e)
		{
			GD.PrintErr($"[Browse] Couldn't read beatmapfilters: {e.Message}");
		}
	}

	// ------------------------------------------------------------------ search

	private void _textchanged(string searchText)
	{
		textchanging = true;
		timetext = Extras.GetMilliseconds();
		State.Query = searchText;
	}

	private void _submit(string searchText)
	{
		textchanging = false;
		State.Query = searchText;
		Restart();
	}

	/// <summary>Restart from a chip's own pressed signal: the rebuild frees that chip, so wait a frame.</summary>
	private void Reload() => Callable.From(Restart).CallDeferred();

	private static string[] AuthHeaders()
	{
		var headers = new List<string> { $"User-Agent: {ApiOperator.AgentString}" };
		if (LoggedIn)
		{
			headers.Add($"USERNAME: {ApiOperator.Username}");
			headers.Add($"PASSWORD: {ApiOperator.PasswordHash}");
		}
		return headers.ToArray();
	}

	private void _RecAPI_finished(long result, long responseCode, string[] headers, byte[] body)
	{
		if (result != (long)HttpRequest.Result.Success || responseCode != 200) return;
		SetRecProfile(Encoding.UTF8.GetString(body));
	}

	/// <summary>Updates the "Recommended Difficulty (Lv. X)" chip and the explanation line from a profile JSON.</summary>
	private void SetRecProfile(string json)
	{
		try
		{
			var profile = JsonSerializer.Deserialize<RecommendationLegend>(json);
			if (profile == null) return;
			bool changed = RecProfile?.level != profile.level;
			RecProfile = profile;
			if (changed) BuildFilters();
			UpdateRecInfo();
		}
		catch (Exception e)
		{
			GD.PrintErr($"[Browse] Couldn't read the recommendation: {e.Message}");
		}
	}

	private void UpdateRecInfo()
	{
		RecInfo.Visible = State.Status == Rec && RecProfile != null;
		if (!RecInfo.Visible) return;
		var r = RecProfile!;
		RecInfo.Text = r.based_on > 0
			? $"From your {r.based_on} best play{(r.based_on == 1 ? "" : "s")}, you're comfortable around Lv {r.level} (~{r.target_pp:0}pp). " +
			  $"Showing ranked difficulties Lv {r.min_level}–{r.max_level} you haven't played yet, best match first."
			: $"Play a few ranked maps and this will match your skill. For now: beginner-friendly Lv {r.min_level}–{r.max_level}.";
	}

	/// <summary>Drops the results and loads page 0 with the current filters.</summary>
	private void Restart()
	{
		UpdateRecInfo();
		BuildFilters();
		BrowseApi.CancelRequest();
		request++;
		isLoading = false;
		done = false;
		page = 0;
		BrowseCatalog.Clear();
		foreach (Node child in BeatmapGrid.GetChildren())
		{
			BeatmapGrid.RemoveChild(child);
			child.QueueFree();
		}
		ClearFilters.Visible = false;
		BeatmapScroll.ScrollVertical = 0;
		lastFail = -1e9;
		LoadPage();
	}

	private string ApiUrl()
	{
		var p = new List<string> { $"query={Uri.EscapeDataString(State.Query)}", $"page={page}" };
		if (State.Status == Mine) p.Add($"uploader={Uri.EscapeDataString(ApiOperator.Username)}");
		else if (State.Status == Rec) p.Add("recommended=1");
		else p.Add($"status={State.Status}");
		if (State.Genre != "") p.Add($"genre={State.Genre}");
		if (State.Language != "") p.Add($"language={State.Language}");
		if (ShowVideoStoryboard && State.Extra.Count > 0) p.Add($"extra={string.Join(",", State.Extra.OrderBy(e => e))}");
		if (State.Played != "" && State.Status != Rec) p.Add($"played={State.Played}");
		if (State.Explicit == "hide") p.Add("explicit=hide");
		p.Add($"sort={State.Sort}");
		if (State.Dir != "") p.Add($"dir={State.Dir}");
		return $"{SettingsOperator.GetSetting("api")}apiv2/search?{string.Join("&", p)}";
	}

	private void LoadPage()
	{
		if (isLoading || done) return;
		isLoading = true;
		pendingRequest = request;
		Hint.Visible = true;
		Hint.Text = "Loading beatmaps...";
		if (page == 0)
		{
			Loadingicon.Visible = true;
			Loadinganimation.Play("loading");
		}

		var uri = ApiUrl();
		GD.Print($"[Browse] {uri}");
		var err = BrowseApi.Request(uri, AuthHeaders()); // "played" and "recommended" need to know who is asking
		if (err != Error.Ok)
		{
			isLoading = false;
			lastFail = Extras.GetMilliseconds();
			Hint.Text = "Couldn't load beatmaps right now. Retrying...";
		}
	}

	private void _BrowseAPI_finished(long result, long responseCode, string[] headers, byte[] body)
	{
		if (pendingRequest != request) return; // filters changed while this was in flight
		isLoading = false;
		Loadingicon.Visible = false;
		Loadinganimation.Stop();

		List<BrowseCatalogLegend>? items = null;
		if (result == (long)HttpRequest.Result.Success && responseCode == 200)
		{
			try
			{
				items = JsonSerializer.Deserialize<List<BrowseCatalogLegend>>(Encoding.UTF8.GetString(body));
			}
			catch (Exception e)
			{
				GD.PrintErr($"[Browse] Failed to parse search response: {e.Message}");
			}
		}
		if (items == null)
		{
			lastFail = Extras.GetMilliseconds();
			Hint.Text = "Couldn't load beatmaps right now. Retrying...";
			return;
		}

		if (page == 0) LoadBackdrop(items.FirstOrDefault());
		if (State.Status == Rec)
		{
			var recHeader = headers.FirstOrDefault(h => h.StartsWith("X-Qlute-Recommend:", StringComparison.OrdinalIgnoreCase));
			if (recHeader != null) SetRecProfile(recHeader[(recHeader.IndexOf(':') + 1)..].Trim());
		}

		var scene = GD.Load<PackedScene>("res://Panels/BrowseElements/Card.tscn");
		foreach (var set in items)
		{
			if (set.beatmaps is not { Count: > 0 }) continue;
			set.beatmaps = set.beatmaps.OrderBy(d => d.level).ThenBy(d => d.count_circles + d.count_sliders).ToList();

			// Ensure the preview URL is absolute
			if (!string.IsNullOrEmpty(set.preview_url) && !set.preview_url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
				set.preview_url = "https:" + set.preview_url;

			int index = BrowseCatalog.Count;
			BrowseCatalog.Add(set);
			var shown = State.Status == Mine ? set.beatmaps[0].ranked : State.Status == Rec ? 1 : int.Parse(State.Status);
			var card = scene.Instantiate<CardFunctions>();
			card.Modulate = new Color(1, 1, 1, 0);
			BeatmapGrid.AddChild(card);
			card.Setup(set, index, shown);
		}

		page++;
		done = items.Count < PerPage;
		if (BrowseCatalog.Count == 0) ShowEmpty();
		else
		{
			Hint.Visible = !done;
			Hint.Text = "Loading beatmaps...";
		}
	}

	private void ShowEmpty()
	{
		Hint.Visible = true;
		bool filtered = FilterCount() > 0;
		if (State.Status == Rec && State.Query == "" && !filtered)
		{
			Hint.Text = "No unplayed ranked difficulties around your level right now -n-\nCheck back when new maps are ranked.";
			return;
		}
		if (State.Status == Mine && State.Query == "" && !filtered)
		{
			Hint.Text = "You haven't submitted any beatmaps yet.\nSubmit them from the editor: File → Submit to Qlute.";
			return;
		}
		Hint.Text = State.Query != "" ? $"Nothing matches \"{State.Query}\" here -n-" : "Nothing matches here -n-";
		ClearFilters.Visible = filtered;
	}

	/// <summary>The top result's cover, blurred behind the search panel. Shrinking it to a few pixels and
	/// letting linear filtering stretch it back up is a cheap blur that works on every renderer.</summary>
	private void LoadBackdrop(BrowseCatalogLegend? set)
	{
		var url = set?.covers?.cover;
		if (string.IsNullOrEmpty(url))
		{
			Backdrop.Texture = null;
			return;
		}
		int mine = request;
		_ = ApiOperator.DownloadImage(url, texture =>
		{
			if (!IsInstanceValid(this) || mine != request)
			{
				texture.Dispose();
				return;
			}
			var img = texture.GetImage();
			texture.Dispose();
			if (img == null || img.IsEmpty()) return;
			if (img.IsCompressed()) img.Decompress();
			img.Resize(24, Math.Max(1, 24 * img.GetHeight() / Math.Max(1, img.GetWidth())), Image.Interpolation.Bilinear);
			Backdrop.Texture = ImageTexture.CreateFromImage(img);
		});
	}

	private void _on_back()
	{
		GetNode<SceneTransition>("/root/Transition").Switch("res://Panels/Screens/home_screen.tscn");
	}

	public override void _Process(double delta)
	{
		// As many CardWidth columns as fit; the cards stretch to fill the row.
		// Measured from the scroll area, not the grid: the grid's width follows its columns, which would feed back.
		var pageBox = GetNode<MarginContainer>("BeatmapSec/Scroll/Page");
		var width = BeatmapScroll.Size.X - pageBox.GetThemeConstant("margin_left") - pageBox.GetThemeConstant("margin_right")
			- BeatmapScroll.GetVScrollBar().Size.X;
		if (width > 0)
		{
			var columns = Math.Max(1, (int)((width + Gap) / (CardWidth + Gap)));
			if (BeatmapGrid.Columns != columns) BeatmapGrid.Columns = columns;
		}

		if (textchanging && Extras.GetMilliseconds() - timetext > 500)
		{
			textchanging = false;
			_submit(State.Query);
		}

		ScrollVertical = BeatmapScroll.ScrollVertical;

		var area = BeatmapSec().GetGlobalRect();
		Loadingicon.Position = new Vector2(area.GetCenter().X, BeatmapGrid.GetGlobalRect().Position.Y + 60);

		// Infinite scroll: fetch the next page when the end is near.
		var bar = BeatmapScroll.GetVScrollBar();
		if (!isLoading && !done && Extras.GetMilliseconds() - lastFail > 3000
			&& ScrollVertical + BeatmapScroll.Size.Y > bar.MaxValue - 600)
			LoadPage();
	}

	private Control BeatmapSec() => GetNode<Control>("BeatmapSec");
}
