using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// Song select's list: one card per beatmap set; the set holding the selected difficulty unfolds into a row per
/// difficulty, tinted with its level colour. Only the rows on screen exist as nodes. The list glides to keep the
/// selection centred (the wheel scrolls freely until the selection changes) and cards curve away from the centre.
/// </summary>
public partial class SongCarousel : Control
{
	/// <summary>Select this difficulty (SongID).</summary>
	public Action<int> Select;
	/// <summary>The selected difficulty was clicked again: play it.</summary>
	public Action Play;

	public const float Gap = 8, Indent = 46, PopOut = 30, Arc = 60, Overhang = 60;
	/// <summary>The card scenes: edit their look in the editor. Row heights are their custom_minimum_size.y.</summary>
	public const string SetScene = "res://Panels/SongSelectButtons/MusicCard.tscn", DiffScene = "res://Panels/SongSelectButtons/DifficultyCard.tscn";
	private static PackedScene _setScene, _diffScene;
	public float SetH { get; private set; } = 122;
	public float DiffH { get; private set; } = 66;
	/// <summary>Cards start this far in, so the selected one can pop out left without being clipped.</summary>
	public const float Inset = 34;
	/// <summary>
	/// The list fills the screen height, but the header covers its top and the bottom bar its bottom: the selection
	/// is centred in the band between these two margins.
	/// </summary>
	public float FocusTop, FocusBottom;

	private struct Row { public bool IsSet; public int Group; public int Song; public float Y; public float H; }

	private readonly List<List<int>> _groups = new();
	private readonly Dictionary<int, int> _groupOf = new();
	private readonly List<Row> _rows = new();
	private readonly Dictionary<string, Control> _live = new();
	private int _selected = -1, _expanded = -1;
	private float _scroll, _target, _contentH;
	private bool _fresh = true;

	public SongCarousel()
	{
		ClipContents = true; // the list spans the screen height, so this only trims at the screen edges
		MouseFilter = MouseFilterEnum.Pass;
		_setScene ??= GD.Load<PackedScene>(SetScene);
		_diffScene ??= GD.Load<PackedScene>(DiffScene);
		SetH = HeightOf(_setScene, SetH);
		DiffH = HeightOf(_diffScene, DiffH);
	}

	private static float HeightOf(PackedScene scene, float fallback)
	{
		var card = scene.Instantiate<Control>();
		float h = card.CustomMinimumSize.Y > 0 ? card.CustomMinimumSize.Y : fallback;
		card.Free();
		return h;
	}

	private CarouselCard NewCard(bool isSet)
	{
		var card = (isSet ? _setScene : _diffScene).Instantiate<CarouselCard>();
		card.Carousel = this;
		return card;
	}

	// ------------------------------------------------------------------ data

	/// <summary>The (already grouped by set) list from SongFilter. Cards that are still listed are kept.</summary>
	public void SetData(List<int> ordered)
	{
		_groups.Clear();
		_groupOf.Clear();
		string lastKey = null;
		foreach (var id in ordered)
		{
			var key = SongFilter.SetKey(SettingsOperator.Beatmaps[id]);
			if (key != lastKey) { _groups.Add(new List<int>()); lastKey = key; }
			_groups[^1].Add(id);
			_groupOf[id] = _groups.Count - 1;
		}
		_expanded = _groupOf.TryGetValue(_selected, out var g) ? g : -1;
		Layout();
		foreach (var pair in _live.ToList()) // drop cards whose rows are gone; rebind the rest (their set may have changed)
		{
			if (!_rows.Any(r => Key(r) == pair.Key)) { pair.Value.QueueFree(); _live.Remove(pair.Key); }
		}
		foreach (var r in _rows) if (_live.TryGetValue(Key(r), out var c)) Bind(c, r);
		Follow(_selected, instant: _fresh);
		_fresh = false;
	}

	/// <summary>Centre on this difficulty and unfold its set.</summary>
	public void Follow(int songId, bool instant = false)
	{
		_selected = songId;
		int g = _groupOf.TryGetValue(songId, out var gi) ? gi : -1;
		if (g != _expanded)
		{
			_expanded = g;
			Layout();
			foreach (var pair in _live.ToList()) // the old set's difficulty rows are gone
				if (!_rows.Any(r => Key(r) == pair.Key)) { pair.Value.QueueFree(); _live.Remove(pair.Key); }
		}
		foreach (var r in _rows) if (_live.TryGetValue(Key(r), out var c)) Bind(c, r);
		var row = _rows.FirstOrDefault(r => !r.IsSet && r.Song == songId);
		if (row.H <= 0) row = _rows.FirstOrDefault(r => r.IsSet && r.Group == g);
		if (row.H > 0) _target = row.Y + row.H / 2;
		if (instant) _scroll = _target;
	}

	private void Layout()
	{
		_rows.Clear();
		float y = 0;
		for (int g = 0; g < _groups.Count; g++)
		{
			_rows.Add(new Row { IsSet = true, Group = g, Song = _groups[g][0], Y = y, H = SetH });
			y += SetH + Gap;
			if (g != _expanded) continue;
			foreach (var id in _groups[g])
			{
				_rows.Add(new Row { IsSet = false, Group = g, Song = id, Y = y, H = DiffH });
				y += DiffH + Gap;
			}
			y += Gap * 2; // a little air before the next set
		}
		_contentH = y;
	}

	private string Key(Row r) => r.IsSet ? "s:" + SongFilter.SetKey(SettingsOperator.Beatmaps[r.Song]) : "d:" + r.Song;

	// ------------------------------------------------------------------ frame

	public override void _Process(double delta)
	{
		int current = SettingsOperator.SessionConfig.SongID;
		if (current != _selected && _groupOf.ContainsKey(current)) Follow(current);

		_scroll = Mathf.Lerp(_scroll, _target, 1f - Mathf.Exp(-(float)delta * 9f));
		float bandTop = Mathf.Clamp(FocusTop, 0, Size.Y), bandBottom = Mathf.Max(bandTop, Size.Y - FocusBottom);
		float centre = (bandTop + bandBottom) / 2, bandHalf = Mathf.Max(1, (bandBottom - bandTop) / 2);
		float top = _scroll - centre - 150, bottom = _scroll + (Size.Y - centre) + 150;

		// rows are sorted by Y: binary search the first one on screen
		int lo = 0, hi = _rows.Count;
		while (lo < hi) { int mid = (lo + hi) / 2; if (_rows[mid].Y + _rows[mid].H < top) lo = mid + 1; else hi = mid; }
		var wanted = new HashSet<string>();
		for (int i = lo; i < _rows.Count && _rows[i].Y < bottom; i++)
		{
			var r = _rows[i];
			var key = Key(r);
			wanted.Add(key);
			if (!_live.TryGetValue(key, out var card))
			{
				card = NewCard(r.IsSet);
				AddChild(card);
				Bind(card, r);
				_live[key] = card;
				card.Modulate = new Color(1, 1, 1, 0);
				card.CreateTween().TweenProperty(card, "modulate:a", 1f, 0.18f);
			}
			float screenY = r.Y - _scroll + centre;
			float d = Mathf.Clamp((screenY + r.H / 2 - centre) / bandHalf, -2f, 2f);
			bool selected = r.IsSet ? r.Group == _expanded : r.Song == _selected;
			// set cards curve away from the centre; an open set's difficulties stay in one straight, even column
			float x = Inset + (r.IsSet ? d * d * Arc : Indent) - (selected ? PopOut : 0);
			var pos = card.Position;
			pos.X = Mathf.IsZeroApprox(pos.X) && card.Modulate.A < 0.05f ? x : Mathf.Lerp(pos.X, x, 1f - Mathf.Exp(-(float)delta * 12f));
			pos.Y = screenY;
			card.Position = pos;
			card.Size = new Vector2(Size.X + Overhang - pos.X, r.H);
		}
		foreach (var pair in _live.ToList())
			if (!wanted.Contains(pair.Key)) { pair.Value.QueueFree(); _live.Remove(pair.Key); }
	}

	public override void _GuiInput(InputEvent e)
	{
		if (e is InputEventMouseButton mb && mb.Pressed && (mb.ButtonIndex == MouseButton.WheelUp || mb.ButtonIndex == MouseButton.WheelDown))
		{
			float step = (SetH + Gap) * 1.5f * (mb.ButtonIndex == MouseButton.WheelUp ? -1 : 1);
			_target = Mathf.Clamp(_target + step, 0, Mathf.Max(0, _contentH));
			AcceptEvent();
		}
	}

	private void Bind(Control card, Row r)
	{
		if (card is MusicCard s) s.Bind(_groups[r.Group], r.Group == _expanded);
		else if (card is DifficultyCard d) d.Bind(r.Song, r.Song == _selected);
	}

	internal void Clicked(int songId, bool isSetCard)
	{
		if (isSetCard)
		{
			// a closed set: open it on its easiest difficulty (or the one you had before in this set)
			int g = _groupOf.TryGetValue(songId, out var gi) ? gi : -1;
			if (g >= 0 && g == _expanded) return;
			int pick = g >= 0 && _lastInSet.TryGetValue(SongFilter.SetKey(SettingsOperator.Beatmaps[songId]), out var last) && _groups[g].Contains(last) ? last : songId;
			Select?.Invoke(pick);
			return;
		}
		if (songId == _selected) { Play?.Invoke(); return; }
		_lastInSet[SongFilter.SetKey(SettingsOperator.Beatmaps[songId])] = songId;
		Select?.Invoke(songId);
	}

	private readonly Dictionary<string, int> _lastInSet = new();

	// ------------------------------------------------------------------ covers (shared, small LRU)

	private static readonly Dictionary<string, Texture2D> Covers = new();
	private static readonly LinkedList<string> CoverOrder = new();
	private static readonly Dictionary<string, List<Action<Texture2D>>> CoverWaiting = new();
	private const int CoverCache = 60;

	internal static void LoadCover(string path, Action<Texture2D> done)
	{
		if (string.IsNullOrEmpty(path)) { done(null); return; }
		if (Covers.TryGetValue(path, out var tex)) { CoverOrder.Remove(path); CoverOrder.AddFirst(path); done(tex); return; }
		if (CoverWaiting.TryGetValue(path, out var list)) { list.Add(done); return; }
		CoverWaiting[path] = new List<Action<Texture2D>> { done };
		Task.Run(() =>
		{
			if (!FileAccess.FileExists(path)) return null;
			var img = Image.LoadFromFile(path);
			if (img == null || img.IsEmpty()) return null;
			if (img.IsCompressed()) img.Decompress();
			if (img.GetWidth() > 640) img.Resize(640, Math.Max(1, 640 * img.GetHeight() / img.GetWidth()), Image.Interpolation.Bilinear);
			return img;
		}).ContinueWith(t => Callable.From(() =>
		{
			Texture2D result = t.IsCompletedSuccessfully && t.Result != null ? ImageTexture.CreateFromImage(t.Result) : null;
			if (result != null)
			{
				Covers[path] = result;
				CoverOrder.AddFirst(path);
				while (CoverOrder.Count > CoverCache) { Covers.Remove(CoverOrder.Last.Value); CoverOrder.RemoveLast(); }
			}
			if (CoverWaiting.Remove(path, out var waiting)) foreach (var w in waiting) w(result);
		}).CallDeferred());
	}

	internal static bool Unicode => Check.CheckBoolValue(SettingsOperator.GetSetting("showunicode")?.ToString() ?? "false");

	internal static string Clock(float ms)
	{
		var t = TimeSpan.FromMilliseconds(Math.Max(0, ms) / Math.Max(0.5f, AudioPlayer.Instance?.PitchScale ?? 1f));
		return t.TotalHours >= 1 ? t.ToString(@"h\:mm\:ss") : t.ToString(@"m\:ss");
	}
}
