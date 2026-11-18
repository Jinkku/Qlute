using Godot;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// A beatmap set in song select's carousel (Panels/SongSelectButtons/MusicCard.tscn): cover, title, artist,
/// "mapped by", status pill and a skyline of its difficulties' levels. Layout, fonts and styleboxes are in the scene;
/// the colours that change with the state are the exports below.
/// </summary>
public partial class MusicCard : CarouselCard
{
	/// <summary>Border and title colour of the open set (the one holding the selected difficulty).</summary>
	[Export] public Color OpenColor { get; set; } = new("#f3baff");
	[Export] public int OpenBorderWidth { get; set; } = 3;
	[Export] public Color TitleColor { get; set; } = Colors.White;
	[Export] public float StripeWidth { get; set; } = 7;
	[Export] public float OpenStripeWidth { get; set; } = 12;

	protected override bool IsSetCard => true;

	private TextureRect _cover;
	private ColorRect _stripe;
	private Label _title, _artist, _mapper, _statusText;
	private PanelContainer _status;
	private LevelSkyline _skyline;
	private string _coverPath;
	private bool _expanded, _painted;

	protected override void Setup()
	{
		_cover = GetNode<TextureRect>("%Cover");
		_stripe = GetNode<ColorRect>("%Stripe");
		_title = GetNode<Label>("%Title");
		_artist = GetNode<Label>("%Artist");
		_mapper = GetNode<Label>("%Mapper");
		_status = GetNode<PanelContainer>("%Status");
		_statusText = GetNode<Label>("%StatusText");
		_skyline = GetNode<LevelSkyline>("%Skyline");
	}

	public void Bind(List<int> songs, bool expanded)
	{
		var b = SettingsOperator.Beatmaps[songs[0]];
		SongId = songs[0];
		bool uni = SongCarousel.Unicode;
		_title.Text = uni && !string.IsNullOrWhiteSpace(b.TitleUnicode) ? b.TitleUnicode : b.Title;
		_artist.Text = uni && !string.IsNullOrWhiteSpace(b.ArtistUnicode) ? b.ArtistUnicode : b.Artist;
		_mapper.Text = $"mapped by {b.Mapper}  ·  {songs.Count} difficult{(songs.Count == 1 ? "y" : "ies")}";
		_stripe.Color = SongFilter.SetColor(b);
		var (name, colour) = Status(b.RankStatus);
		_statusText.Text = name;
		_statusText.AddThemeColorOverride("font_color", colour);
		_status.AddThemeStyleboxOverride("panel", new PillBox(new Color(colour, 0.15f), new Color(colour, 0.6f), 10, 1));
		_skyline.Levels = songs.Select(id => SettingsOperator.Beatmaps[id].Levelrating).ToList();
		if (!_painted || _expanded != expanded)
		{
			_painted = true;
			_expanded = expanded;
			Restyle((s, _) =>
			{
				if (!expanded) return;
				s.BorderColor = OpenColor;
				s.SetBorderWidthAll(OpenBorderWidth);
			});
			// the open set: a wider stripe and a coloured title
			_stripe.OffsetRight = _stripe.OffsetLeft + (expanded ? OpenStripeWidth : StripeWidth);
			_title.AddThemeColorOverride("font_color", expanded ? OpenColor : TitleColor);
		}
		var path = b.Path + b.Background;
		if (path != _coverPath)
		{
			_coverPath = path;
			_cover.Texture = null;
			SongCarousel.LoadCover(b.Background == null ? null : path, tex => { if (IsInstanceValid(this) && _coverPath == path) _cover.Texture = tex; });
		}
	}

	public static (string, Color) Status(int status) => status switch
	{
		1 => ("RANKED", new Color("#7ee37e")),
		2 => ("SPECIAL", new Color("#f5cf6c")),
		0 => ("UNRANKED", new Color("#c9c3d6")),
		_ => ("LOCAL", new Color("#9aa7c7")),
	};
}
