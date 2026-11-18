using Godot;
using System;

/// <summary>
/// One difficulty of the open set in song select's carousel (Panels/SongSelectButtons/DifficultyCard.tscn):
/// tinted with its level colour, name + mapper, a level badge and a 10-step level meter.
/// Layout and styleboxes are in the scene; the tint strengths are the exports below.
/// </summary>
public partial class DifficultyCard : CarouselCard
{
	/// <summary>Background before the level colour is mixed in.</summary>
	[Export] public Color BaseColor { get; set; } = new(0.08f, 0.06f, 0.1f);
	[Export(PropertyHint.Range, "0,1")] public float Tint { get; set; } = 0.24f;
	[Export(PropertyHint.Range, "0,1")] public float HoverTint { get; set; } = 0.34f;
	[Export(PropertyHint.Range, "0,1")] public float SelectedTint { get; set; } = 0.55f;
	[Export(PropertyHint.Range, "0,1")] public float SelectedHoverTint { get; set; } = 0.6f;
	[Export(PropertyHint.Range, "0,1")] public float Opacity { get; set; } = 0.95f;
	/// <summary>Text colours: each label uses whichever has more contrast on what's behind it (light on dark, dark on light).</summary>
	[Export] public Color TextLight { get; set; } = Colors.White;
	[Export] public Color TextDark { get; set; } = new("#1b1620");
	[Export] public float TabWidth { get; set; } = 10;
	[Export] public float SelectedTabWidth { get; set; } = 16;

	protected override bool IsSetCard => false;

	private ColorRect _tab;
	private Label _name, _mapper, _badgeText;
	private PanelContainer _badge;
	private StyleBox _badgeStyle;
	private float _mapperAlpha = 0.7f;
	private LevelMeter _meter;
	private bool? _wasSelected;
	private int _bound = -1;

	protected override void Setup()
	{
		_tab = GetNode<ColorRect>("%Tab");
		_name = GetNode<Label>("%DiffName");
		_mapper = GetNode<Label>("%Mapper");
		_badge = GetNode<PanelContainer>("%Badge");
		_badgeText = GetNode<Label>("%BadgeText");
		_meter = GetNode<LevelMeter>("%Meter");
		_badgeStyle = _badge.GetThemeStylebox("panel");
		_mapperAlpha = _mapper.GetThemeColor("font_color").A;
	}

	public void Bind(int songId, bool selected)
	{
		if (_bound == songId && _wasSelected == selected) return;
		_bound = SongId = songId;
		_wasSelected = selected;
		var b = SettingsOperator.Beatmaps[songId];
		var level = (int)Math.Clamp(b.Levelrating, 0, 99);
		var c = SettingsOperator.ReturnLevelColour(level);
		_tab.Color = c;
		_name.Text = (b.Version ?? "");
		_mapper.Text = $"mapped by {b.Mapper}";
		if (_badgeStyle is StyleBoxFlat badge)
		{
			var copy = (StyleBoxFlat)badge.Duplicate();
			copy.BgColor = c;
			_badge.AddThemeStyleboxOverride("panel", copy);
		}
		_badgeText.AddThemeColorOverride("font_color", TextOn(c, TextLight, TextDark));
		_badgeText.Text = $"Lv {b.Levelrating:0}";
		_meter.Set(b.Levelrating, c);

		// selected: a stronger tint and a wider tab (no outline: it didn't sit well on a card that runs off-screen)
		Color Bg(bool hover) => BaseColor.Lerp(c, selected ? (hover ? SelectedHoverTint : SelectedTint) : (hover ? HoverTint : Tint)) with { A = Opacity };
		Restyle((s, hover) => s.BgColor = Bg(hover));
		// name + mapper: black or white, whichever reads better on this card (the mapper keeps its dimmer alpha)
		var text = TextOn(Bg(false), TextLight, TextDark);
		_name.AddThemeColorOverride("font_color", text);
		_mapper.AddThemeColorOverride("font_color", text with { A = _mapperAlpha });
		_tab.OffsetRight = _tab.OffsetLeft + (selected ? SelectedTabWidth : TabWidth);
	}
}
