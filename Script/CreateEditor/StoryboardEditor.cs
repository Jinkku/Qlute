using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using QluteStoryboard;

namespace CreateEditor;

/// <summary>
/// Storyboard tab of the Create editor: object list, a live preview (click to select, drag to move)
/// and a property / command editor. Edits the difficulty's own storyboard (saved into its [Events]);
/// objects from the set's .osb are shown in the preview but can't be edited here.
/// </summary>
public partial class StoryboardEditor : Control
{
	public Create Editor { get; set; }

	private static readonly string[] ImageExtensions = { ".png", ".jpg", ".jpeg", ".webp", ".bmp" };
	private static readonly (string label, SbCommandType type, char param)[] CommandKinds =
	{
		("Fade", SbCommandType.F, ' '), ("Scale", SbCommandType.S, ' '), ("Stretch (X/Y scale)", SbCommandType.V, ' '),
		("Rotate (radians)", SbCommandType.R, ' '), ("Move", SbCommandType.M, ' '), ("Move X", SbCommandType.MX, ' '),
		("Move Y", SbCommandType.MY, ' '), ("Colour (0-255)", SbCommandType.C, ' '),
		("Flip horizontally", SbCommandType.P, 'H'), ("Flip vertically", SbCommandType.P, 'V'), ("Additive blending", SbCommandType.P, 'A'),
	};

	private StoryboardDoc Doc => Editor.Storyboard;
	private SbObject _selected;
	private int _selectedCommand = -1;
	private bool _suppress;

	// nodes
	private ItemList _objects;
	private Control _previewArea;
	private TextureRect _previewBg;
	public StoryboardPlayer _player;
	private StoryboardPreviewOverlay _overlay;
	private VBoxContainer _props;
	private OptionButton _image, _layer, _origin, _loop;
	private SpinBox _x, _y, _frames, _delay;
	private CheckBox _animation;
	private ItemList _commands;
	private OptionButton _cmdType, _cmdEase;
	private SpinBox _cmdStart, _cmdEnd;
	private SpinBox[] _from = new SpinBox[3], _to = new SpinBox[3];
	private Label _fromLabel, _toLabel, _hint;
	private HBoxContainer _fromRow, _toRow;
	private List<string> _imageFiles = new();

	public override void _Ready()
	{
		var split = new HBoxContainer { AnchorRight = 1, AnchorBottom = 1 };
		split.AddThemeConstantOverride("separation", 0);
		AddChild(split);
		split.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

		split.AddChild(BuildObjectPanel());
		split.AddChild(BuildPreview());
		split.AddChild(BuildPropertyPanel());
		Refresh();
	}

	private static PanelContainer Panel(float width)
	{
		var p = new PanelContainer { CustomMinimumSize = new Vector2(width, 0) };
		var style = new StyleBoxFlat { BgColor = new Color(0.08f, 0.1f, 0.08f, 0.92f), ContentMarginLeft = 13, ContentMarginRight = 13, ContentMarginTop = 13, ContentMarginBottom = 13 };
		p.AddThemeStyleboxOverride("panel", style);
		return p;
	}

	private static Label Title(string text) => new() { Text = text, ThemeTypeVariation = "HeaderSmall" };

	private static Button Btn(string text, Action onPress, string tip = null)
	{
		var b = new Button { Text = text, FocusMode = FocusModeEnum.None, TooltipText = tip ?? "", SizeFlagsHorizontal = SizeFlags.ExpandFill };
		b.Pressed += onPress;
		return b;
	}

	// =====================================================================
	// Layout
	// =====================================================================

	private Control BuildObjectPanel()
	{
		var panel = Panel(300);
		var box = new VBoxContainer();
		panel.AddChild(box);
		box.AddChild(Title("Storyboard objects"));
		_objects = new ItemList { SizeFlagsVertical = SizeFlags.ExpandFill, FocusMode = FocusModeEnum.None, TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis };
		_objects.AddThemeFontSizeOverride("font_size", 16);
		_objects.ItemSelected += i => Select(ObjectAtRow((int)i));
		_objects.ItemActivated += i =>
		{
			var o = ObjectAtRow((int)i);
			if (o != null && o.HasCommands) Editor.Seek(o.StartTime);
		};
		box.AddChild(_objects);
		var row1 = new HBoxContainer();
		row1.AddChild(Btn("Add sprite", AddSpriteFromPicker, "Add an image (it's copied into the beatmap folder). You can also drop images here."));
		row1.AddChild(Btn("Copy", Duplicate, "Duplicate the selected object"));
		box.AddChild(row1);
		var row2 = new HBoxContainer();
		row2.AddChild(Btn("Up", () => MoveSelected(1), "Draw above the next object"));
		row2.AddChild(Btn("Down", () => MoveSelected(-1), "Draw below the previous object"));
		row2.AddChild(Btn("Delete", DeleteSelected));
		box.AddChild(row2);
		_hint = new Label
		{
			Text = "Click a sprite in the preview to select it, drag to move it.\nDouble-click a list entry to jump to it.",
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			Modulate = new Color(1, 1, 1, 0.6f),
		};
		_hint.AddThemeFontSizeOverride("font_size", 14);
		box.AddChild(_hint);
		return panel;
	}

	private Control BuildPreview()
	{
		_previewArea = new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill, ClipContents = true, MouseFilter = MouseFilterEnum.Ignore };
		var black = new ColorRect { Color = Colors.Black, MouseFilter = MouseFilterEnum.Ignore };
		_previewArea.AddChild(black);
		black.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		_previewBg = new TextureRect
		{
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		_previewArea.AddChild(_previewBg);
		_player = new StoryboardPlayer { TimeSource = () => Editor.CurrentMs, BackgroundToFollow = _previewBg, PlaySamples = false };
		_previewArea.AddChild(_player);
		_overlay = new StoryboardPreviewOverlay { Host = this };
		_previewArea.AddChild(_overlay);
		_previewArea.Resized += LayoutPreview;
		return _previewArea;
	}

	/// <summary>Keep the preview at 16:9 inside the available space, like the game screen.</summary>
	private void LayoutPreview()
	{
		var avail = _previewArea.Size;
		var size = new Vector2(avail.X, avail.X * 9f / 16f);
		if (size.Y > avail.Y) size = new Vector2(avail.Y * 16f / 9f, avail.Y);
		var pos = (avail - size) / 2f;
		foreach (var c in new Control[] { _previewBg, _player, _overlay })
		{
			c.Position = pos;
			c.Size = size;
		}
		if (_player.OverlayHost != null) { _player.OverlayHost.Position = pos; _player.OverlayHost.Size = size; }
	}

	private Control BuildPropertyPanel()
	{
		var panel = Panel(400);
		var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
		panel.AddChild(scroll);
		_props = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		scroll.AddChild(_props);

		_props.AddChild(Title("Sprite"));
		_image = new OptionButton { FocusMode = FocusModeEnum.None, ClipText = true };
		_image.ItemSelected += i =>
		{
			if (_suppress || _selected == null || i < 0 || i >= _imageFiles.Count) return;
			Change("image", o => o.Path = _imageFiles[(int)i]);
		};
		_props.AddChild(Labeled("Image", _image));

		_layer = new OptionButton { FocusMode = FocusModeEnum.None };
		foreach (var l in Enum.GetNames<SbLayer>()) _layer.AddItem(l);
		_layer.ItemSelected += i => { if (!_suppress) Change("layer", o => o.Layer = (SbLayer)(int)i, rebuildList: true); };
		_layer.TooltipText = "Background/Foreground: behind the notes. Pass/Fail: only while the player is passing/failing. Overlay: on top of the notes.";
		_props.AddChild(Labeled("Layer", _layer));

		_origin = new OptionButton { FocusMode = FocusModeEnum.None };
		foreach (var o in Enum.GetNames<SbOrigin>()) _origin.AddItem(o);
		_origin.ItemSelected += i => { if (!_suppress) Change("origin", o => o.Origin = (SbOrigin)(int)i); };
		_props.AddChild(Labeled("Origin", _origin));

		_x = Spin(-2000, 2000, 1);
		_y = Spin(-2000, 2000, 1);
		_x.ValueChanged += v => { if (!_suppress) Change("x", o => o.X = (float)v); };
		_y.ValueChanged += v => { if (!_suppress) Change("y", o => o.Y = (float)v); };
		var pos = new HBoxContainer();
		pos.AddChild(_x);
		pos.AddChild(_y);
		_props.AddChild(Labeled("Position (640x480, centre 320,240)", pos));

		_animation = new CheckBox { Text = "Animation", FocusMode = FocusModeEnum.None, TooltipText = "Frames are image0.png, image1.png, ... next to the image" };
		_animation.Toggled += v => { if (!_suppress) Change("anim", o => o.IsAnimation = v); };
		_props.AddChild(_animation);
		_frames = Spin(1, 1000, 1);
		_delay = Spin(1, 100000, 1);
		_frames.ValueChanged += v => { if (!_suppress) Change("frames", o => o.FrameCount = (int)v); };
		_delay.ValueChanged += v => { if (!_suppress) Change("delay", o => o.FrameDelay = (float)v); };
		_loop = new OptionButton { FocusMode = FocusModeEnum.None };
		_loop.AddItem("Loop");
		_loop.AddItem("Once");
		_loop.ItemSelected += i => { if (!_suppress) Change("loop", o => o.LoopType = (SbLoopType)(int)i); };
		var anim = new HBoxContainer();
		anim.AddChild(_frames);
		anim.AddChild(_delay);
		anim.AddChild(_loop);
		_props.AddChild(Labeled("Frames / ms per frame", anim));

		_props.AddChild(new HSeparator());
		_props.AddChild(Title("Commands"));
		_commands = new ItemList { CustomMinimumSize = new Vector2(0, 188), FocusMode = FocusModeEnum.None };
		_commands.AddThemeFontSizeOverride("font_size", 16);
		_commands.ItemSelected += i => { _selectedCommand = (int)i; LoadCommandEditor(); };
		_commands.ItemActivated += i =>
		{
			if (_selected != null && i < _selected.Commands.Count) Editor.Seek(_selected.Commands[(int)i].StartTime);
		};
		_props.AddChild(_commands);

		var quick = new HBoxContainer();
		quick.AddChild(Btn("Fade in", () => QuickFade(true), "Fade from 0 to 1 over one beat, starting now"));
		quick.AddChild(Btn("Fade out", () => QuickFade(false), "Fade from 1 to 0 over one beat, starting now"));
		quick.AddChild(Btn("Move here", QuickMove, "Move from the current position to where it is now, over one beat"));
		_props.AddChild(quick);

		_cmdType = new OptionButton { FocusMode = FocusModeEnum.None };
		foreach (var k in CommandKinds) _cmdType.AddItem(k.label);
		_cmdType.ItemSelected += _ => UpdateValueRows();
		_props.AddChild(Labeled("Type", _cmdType));
		_cmdEase = new OptionButton { FocusMode = FocusModeEnum.None };
		foreach (var e in SbEasing.Names) _cmdEase.AddItem(e);
		_props.AddChild(Labeled("Easing", _cmdEase));

		_cmdStart = Spin(-100000, 10000000, 1);
		_cmdEnd = Spin(-100000, 10000000, 1);
		var times = new GridContainer { Columns = 2 };
		times.AddChild(_cmdStart);
		times.AddChild(Btn("Now", () => _cmdStart.Value = Math.Round(Editor.CurrentMs)));
		times.AddChild(_cmdEnd);
		times.AddChild(Btn("Now", () => _cmdEnd.Value = Math.Round(Editor.CurrentMs)));
		_props.AddChild(Labeled("Start / end time (ms)", times));

		_fromRow = new HBoxContainer();
		_toRow = new HBoxContainer();
		for (int k = 0; k < 3; k++)
		{
			_from[k] = Spin(-100000, 100000, 0.01);
			_to[k] = Spin(-100000, 100000, 0.01);
			_fromRow.AddChild(_from[k]);
			_toRow.AddChild(_to[k]);
		}
		_fromLabel = new Label { Text = "From" };
		_toLabel = new Label { Text = "To" };
		_props.AddChild(_fromLabel);
		_props.AddChild(_fromRow);
		_props.AddChild(_toLabel);
		_props.AddChild(_toRow);

		var cmdButtons = new HBoxContainer();
		cmdButtons.AddChild(Btn("Add", AddCommand, "Add this command to the sprite"));
		cmdButtons.AddChild(Btn("Update", UpdateCommand, "Replace the selected command"));
		cmdButtons.AddChild(Btn("Remove", RemoveCommand));
		_props.AddChild(cmdButtons);
		UpdateValueRows();
		return panel;
	}

	private static Control Labeled(string label, Control c)
	{
		var box = new VBoxContainer();
		var l = new Label { Text = label, Modulate = new Color(1, 1, 1, 0.75f) };
		l.AddThemeFontSizeOverride("font_size", 15);
		box.AddChild(l);
		c.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		box.AddChild(c);
		return box;
	}

	private static SpinBox Spin(double min, double max, double step) => new()
	{
		MinValue = min, MaxValue = max, Step = step, AllowGreater = true, AllowLesser = true,
		SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(75, 0),
		SelectAllOnFocus = true,
	};

	// =====================================================================
	// State
	// =====================================================================

	/// <summary>Rebuild everything from the editor's storyboard (after load, undo, tab switch).</summary>
	public void Refresh()
	{
		if (Editor?.Storyboard == null || _objects == null) return;
		if (_selected != null && !Doc.Objects.Contains(_selected)) _selected = null;
		_previewBg.Texture = Editor.BackgroundTexture;
		ScanImages();
		Doc.CompileAll();
		_player.Load(Doc, Editor.SetFolderPath, async: false);
		if (_player.OverlayHost.GetParent() == null)
		{
			_previewArea.AddChild(_player.OverlayHost);
			_previewArea.MoveChild(_overlay, -1);
		}
		LayoutPreview();
		RebuildList();
		LoadProperties();
	}

	private readonly List<SbObject> _rows = new();

	private SbObject ObjectAtRow(int row) => row >= 0 && row < _rows.Count ? _rows[row] : null;

	private void RebuildList()
	{
		_objects.Clear();
		_rows.Clear();
		foreach (var o in Doc.Objects)
		{
			var name = Path.GetFileName(o.Path);
			var when = o.HasCommands ? $"  {Editor.FormatTime(o.StartTime)}" : "  (no commands)";
			int i = _objects.AddItem($"{(o.IsAnimation ? "[A] " : "")}{name}  · {o.Layer}{when}{(o.FromOsb ? "  (.osb)" : "")}");
			if (o.FromOsb) _objects.SetItemCustomFgColor(i, new Color(1, 1, 1, 0.4f));
			_rows.Add(o);
			if (o == _selected) _objects.Select(i);
		}
	}

	private void Select(SbObject o)
	{
		_selected = o;
		_selectedCommand = -1;
		int row = _rows.IndexOf(o);
		if (row >= 0) _objects.Select(row);
		else _objects.DeselectAll();
		if (row >= 0) _objects.EnsureCurrentIsVisible();
		LoadProperties();
	}

	internal bool Editable => _selected != null && !_selected.FromOsb;

	private void LoadProperties()
	{
		_suppress = true;
		foreach (var c in new Control[] { _image, _layer, _origin, _x, _y, _animation, _frames, _delay, _loop })
			SetEnabled(c, Editable);
		if (_selected != null)
		{
			_image.Clear();
			foreach (var f in _imageFiles) _image.AddItem(f);
			int idx = _imageFiles.FindIndex(f => string.Equals(f, _selected.Path, StringComparison.OrdinalIgnoreCase));
			if (idx < 0) { _image.AddItem(_selected.Path + " (missing)"); idx = _image.ItemCount - 1; }
			_image.Select(idx);
			_layer.Select((int)_selected.Layer);
			_origin.Select((int)_selected.Origin);
			_x.SetValueNoSignal(_selected.X);
			_y.SetValueNoSignal(_selected.Y);
			_animation.SetPressedNoSignal(_selected.IsAnimation);
			_frames.SetValueNoSignal(_selected.FrameCount);
			_delay.SetValueNoSignal(_selected.FrameDelay);
			_loop.Select((int)_selected.LoopType);
		}
		_commands.Clear();
		if (_selected != null)
		{
			foreach (var c in _selected.Commands) _commands.AddItem(StoryboardDoc.Describe(c));
			foreach (var l in _selected.Loops)
			{
				int i = _commands.AddItem($"Loop at {l.StartTime} x{l.Count} ({l.Commands.Count} commands, edit in a text editor)");
				_commands.SetItemDisabled(i, true);
			}
			if (_selected.RawTriggerLines.Count > 0)
				_commands.SetItemDisabled(_commands.AddItem("Trigger group (kept, not played)"), true);
			if (_selectedCommand >= 0 && _selectedCommand < _selected.Commands.Count) _commands.Select(_selectedCommand);
		}
		_suppress = false;
	}

	private static void SetEnabled(Control c, bool on)
	{
		switch (c)
		{
			case BaseButton b: b.Disabled = !on; break;
			case SpinBox s: s.Editable = on; break;
		}
	}

	private void LoadCommandEditor()
	{
		if (_selected == null || _selectedCommand < 0 || _selectedCommand >= _selected.Commands.Count) return;
		var c = _selected.Commands[_selectedCommand];
		int kind = Array.FindIndex(CommandKinds, k => k.type == c.Type && (c.Type != SbCommandType.P || k.param == c.Param));
		if (kind >= 0) _cmdType.Select(kind);
		_cmdEase.Select(Math.Clamp(c.Easing, 0, SbEasing.Names.Length - 1));
		_cmdStart.SetValueNoSignal(c.StartTime);
		_cmdEnd.SetValueNoSignal(c.EndTime);
		for (int k = 0; k < 3; k++)
		{
			_from[k].SetValueNoSignal(c.From[k]);
			_to[k].SetValueNoSignal(c.To[k]);
		}
		UpdateValueRows();
	}

	private void UpdateValueRows()
	{
		var kind = CommandKinds[Math.Max(0, _cmdType.Selected)];
		int n = SbCommand.Arity(kind.type);
		for (int k = 0; k < 3; k++)
		{
			_from[k].Visible = k < n;
			_to[k].Visible = k < n;
		}
		_fromRow.Visible = _toRow.Visible = _fromLabel.Visible = _toLabel.Visible = n > 0;
		_fromLabel.Text = kind.type switch
		{
			SbCommandType.M => "From (x, y)", SbCommandType.V => "From (x scale, y scale)",
			SbCommandType.C => "From (red, green, blue)", _ => "From",
		};
		_toLabel.Text = _fromLabel.Text.Replace("From", "To");
	}

	/// <summary>Apply an edit to the selected object with undo (consecutive edits of one field collapse).</summary>
	private void Change(string field, Action<SbObject> apply, bool rebuildList = false)
	{
		if (!Editable) return;
		Editor.PushUndo("sb:" + field + ":" + Doc.Objects.IndexOf(_selected));
		apply(_selected);
		Changed(rebuildList);
	}

	private void Changed(bool rebuildList = true)
	{
		Editor.MarkDirty();
		Doc.CompileAll();
		_player.Load(Doc, Editor.SetFolderPath, async: false);
		if (rebuildList) RebuildList();
		LoadProperties();
	}

	// =====================================================================
	// Objects
	// =====================================================================

	private void ScanImages()
	{
		_imageFiles.Clear();
		var folder = Editor.SetFolderPath;
		if (folder == null || !Directory.Exists(folder)) return;
		foreach (var f in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
		{
			if (!ImageExtensions.Contains(Path.GetExtension(f).ToLowerInvariant())) continue;
			_imageFiles.Add(Path.GetRelativePath(folder, f).Replace('\\', '/'));
			if (_imageFiles.Count > 2000) break;
		}
		_imageFiles.Sort(StringComparer.OrdinalIgnoreCase);
	}

	private void AddSpriteFromPicker()
	{
		if (!EnsureSaved()) return;
		Editor.OpenFileDialogPublic("Add a storyboard image", new[] { "*.png, *.jpg, *.jpeg, *.webp, *.bmp ; Images" }, ImportImage);
	}

	private bool EnsureSaved()
	{
		if (Editor.SetFolderPath != null) return true;
		Notify.Post("Save the beatmap once first (Ctrl+S): storyboard images are copied into its folder.", Type: NotificationIcons.NotificationType.Warning);
		return false;
	}

	/// <summary>Copy an image into the set's sb/ folder (unless it's already inside the set) and add a sprite for it.</summary>
	public void ImportImage(string path)
	{
		if (!EnsureSaved() || !File.Exists(path)) return;
		var folder = Editor.SetFolderPath;
		string rel;
		var full = Path.GetFullPath(path);
		if (full.StartsWith(Path.GetFullPath(folder) + Path.DirectorySeparatorChar))
			rel = Path.GetRelativePath(folder, full).Replace('\\', '/');
		else
		{
			var name = ManiaBeatmap.SafeFileName(Path.GetFileName(path));
			Directory.CreateDirectory(Path.Combine(folder, "sb"));
			var dest = Path.Combine(folder, "sb", name);
			if (File.Exists(dest) && new FileInfo(dest).Length != new FileInfo(path).Length)
				dest = Path.Combine(folder, "sb", Path.GetFileNameWithoutExtension(name) + "_" + DateTime.Now.Ticks % 100000 + Path.GetExtension(name));
			File.Copy(path, dest, true);
			rel = Path.GetRelativePath(folder, dest).Replace('\\', '/');
		}
		_player.RefreshFiles();
		ScanImages();
		AddSprite(rel);
	}

	private void AddSprite(string relPath)
	{
		Editor.PushUndo();
		var start = (int)Math.Round(Editor.CurrentMs);
		var o = new SbObject { Path = relPath, Layer = SbLayer.Foreground, Origin = SbOrigin.Centre, X = 320, Y = 240 };
		// visible for 4 beats from now, fading in and out over one beat
		int beat = BeatMs();
		o.Commands.Add(new SbCommand { Type = SbCommandType.F, StartTime = start, EndTime = start + beat, From = new[] { 0f, 0, 0 }, To = new[] { 1f, 0, 0 } });
		o.Commands.Add(new SbCommand { Type = SbCommandType.F, StartTime = start + beat * 3, EndTime = start + beat * 4, From = new[] { 1f, 0, 0 }, To = new[] { 0f, 0, 0 } });
		// insert after the difficulty's last own object (before .osb objects)
		int at = Doc.Objects.FindLastIndex(x => !x.FromOsb) + 1;
		Doc.Objects.Insert(at, o);
		_selected = o;
		Changed();
		Select(o);
	}

	private int BeatMs()
	{
		var tp = Editor.Map.TimingAt(Editor.CurrentMs);
		return (int)Math.Round(tp?.BeatLength ?? 500);
	}

	private void Duplicate()
	{
		if (!Editable) return;
		Editor.PushUndo();
		var copy = _selected.Clone();
		Doc.Objects.Insert(Doc.Objects.IndexOf(_selected) + 1, copy);
		_selected = copy;
		Changed();
		Select(copy);
	}

	private void DeleteSelected()
	{
		if (!Editable) return;
		Editor.PushUndo();
		Doc.Objects.Remove(_selected);
		_selected = null;
		Changed();
	}

	private void MoveSelected(int dir)
	{
		if (!Editable) return;
		int i = Doc.Objects.IndexOf(_selected);
		int j = i + dir;
		if (j < 0 || j >= Doc.Objects.Count || Doc.Objects[j].FromOsb) return;
		Editor.PushUndo();
		(Doc.Objects[i], Doc.Objects[j]) = (Doc.Objects[j], Doc.Objects[i]);
		Changed();
		Select(_selected);
	}

	// =====================================================================
	// Commands
	// =====================================================================

	private SbCommand CommandFromEditor()
	{
		var kind = CommandKinds[Math.Max(0, _cmdType.Selected)];
		var c = new SbCommand
		{
			Type = kind.type,
			Param = kind.param == ' ' ? 'H' : kind.param,
			Easing = Math.Max(0, _cmdEase.Selected),
			StartTime = (int)Math.Round(_cmdStart.Value),
			EndTime = (int)Math.Round(Math.Max(_cmdStart.Value, _cmdEnd.Value)),
		};
		for (int k = 0; k < 3; k++)
		{
			c.From[k] = (float)_from[k].Value;
			c.To[k] = (float)_to[k].Value;
		}
		return c;
	}

	private void AddCommand()
	{
		if (!Editable) return;
		Editor.PushUndo();
		_selected.Commands.Add(CommandFromEditor());
		_selectedCommand = _selected.Commands.Count - 1;
		Changed();
	}

	private void UpdateCommand()
	{
		if (!Editable || _selectedCommand < 0 || _selectedCommand >= _selected.Commands.Count) return;
		Editor.PushUndo();
		_selected.Commands[_selectedCommand] = CommandFromEditor();
		Changed();
	}

	private void RemoveCommand()
	{
		if (!Editable || _selectedCommand < 0 || _selectedCommand >= _selected.Commands.Count) return;
		Editor.PushUndo();
		_selected.Commands.RemoveAt(_selectedCommand);
		_selectedCommand = -1;
		Changed();
	}

	private void QuickFade(bool fadeIn)
	{
		if (!Editable) return;
		Editor.PushUndo();
		int start = (int)Math.Round(Editor.CurrentMs);
		_selected.Commands.Add(new SbCommand
		{
			Type = SbCommandType.F, StartTime = start, EndTime = start + BeatMs(),
			From = new[] { fadeIn ? 0f : 1f, 0, 0 }, To = new[] { fadeIn ? 1f : 0f, 0, 0 },
		});
		Changed();
	}

	/// <summary>Adds a one-beat move from where the sprite is now to its base position, so dragging + this builds a path.</summary>
	private void QuickMove()
	{
		if (!Editable) return;
		Editor.PushUndo();
		int start = (int)Math.Round(Editor.CurrentMs);
		var st = _selected.StateAt(start);
		_selected.Commands.Add(new SbCommand
		{
			Type = SbCommandType.M, StartTime = start, EndTime = start + BeatMs(), Easing = 2,
			From = new[] { st.X, st.Y, 0 }, To = new[] { _selected.X, _selected.Y, 0 },
		});
		Changed();
	}

	// =====================================================================
	// Preview interaction (called by PreviewOverlay)
	// =====================================================================

	private Vector2 _dragStartSb;
	private bool _dragging;
	private int _dragId;

	public void PreviewPressed(Vector2 local)
	{
		// topmost visible object under the mouse (overlay layer first, then reverse draw order)
		// overlay layer first, then the others by layer; later objects are drawn on top
		var order = Doc.Objects.Select((o, i) => (o, i))
			.OrderByDescending(x => x.o.Layer == SbLayer.Overlay ? 99 : (int)x.o.Layer).ThenByDescending(x => x.i).Select(x => x.o);
		var hits = order.Where(o =>
		{
			var corners = _player.CornersOf(o);
			return corners != null && Geometry2D.IsPointInPolygon(local, corners);
		}).ToList();
		// keep the selection when it's under the mouse (so it can be dragged even below other sprites),
		// else the topmost object this difficulty owns, else the topmost .osb object (view only)
		var hit = hits.Contains(_selected) ? _selected : hits.FirstOrDefault(o => !o.FromOsb) ?? hits.FirstOrDefault();
		if (hit != null && hit != _selected) Select(hit);
		if (hit == null) { Select(null); return; }
		if (!Editable) return;
		_dragging = true;
		_dragStartSb = _player.LocalToSb(local);
		_dragId++;
	}

	public void PreviewDragged(Vector2 local)
	{
		if (!_dragging || !Editable) return;
		var now = _player.LocalToSb(local);
		var d = now - _dragStartSb;
		if (d.LengthSquared() < 0.25f) return;
		Editor.PushUndo("sbdrag" + _dragId);
		_dragStartSb = now;
		Offset(_selected, d);
		Editor.MarkDirty();
		_selected.Compile();
		_suppress = true;
		_x.SetValueNoSignal(_selected.X);
		_y.SetValueNoSignal(_selected.Y);
		_suppress = false;
	}

	public void PreviewReleased()
	{
		if (!_dragging) return;
		_dragging = false;
		LoadProperties();
	}

	/// <summary>Shift the base position and every move command by <paramref name="d"/>.</summary>
	private static void Offset(SbObject o, Vector2 d)
	{
		o.X = MathF.Round(o.X + d.X, 1);
		o.Y = MathF.Round(o.Y + d.Y, 1);
		foreach (var c in o.Commands.Concat(o.Loops.SelectMany(l => l.Commands)))
		{
			switch (c.Type)
			{
				case SbCommandType.M:
					c.From[0] += d.X; c.To[0] += d.X; c.From[1] += d.Y; c.To[1] += d.Y; break;
				case SbCommandType.MX:
					c.From[0] += d.X; c.To[0] += d.X; break;
				case SbCommandType.MY:
					c.From[0] += d.Y; c.To[0] += d.Y; break;
			}
		}
	}

	internal Vector2[] SelectedCorners() => _selected == null ? null : _player.CornersOf(_selected);

	internal Vector2? SelectedAnchor()
	{
		if (_selected == null) return null;
		var st = _selected.HasCommands ? _selected.StateAt(Math.Clamp(Editor.CurrentMs, _selected.StartTime, _selected.EndTime)) : default;
		var p = _selected.HasCommands ? new Vector2(st.X, st.Y) : new Vector2(_selected.X, _selected.Y);
		return _player.SbToLocal(p);
	}

	internal bool SelectedVisibleNow => _selected != null && _selected.HasCommands &&
	                                    Editor.CurrentMs >= _selected.StartTime && Editor.CurrentMs <= _selected.EndTime;

	public void HandleDelete()
	{
		if (Editable) DeleteSelected();
	}
}

/// <summary>Draws the selection and catches mouse input over the preview.</summary>
public partial class StoryboardPreviewOverlay : Control
{
	public StoryboardEditor Host;

	public override void _Ready() => MouseFilter = MouseFilterEnum.Stop;
	public override void _Process(double delta) => QueueRedraw();

	public override void _GuiInput(InputEvent e)
	{
		if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left } mb)
		{
			if (mb.Pressed) Host.PreviewPressed(mb.Position);
			else Host.PreviewReleased();
			AcceptEvent();
		}
		else if (e is InputEventMouseMotion mm && (mm.ButtonMask & MouseButtonMask.Left) != 0)
		{
			Host.PreviewDragged(mm.Position);
			AcceptEvent();
		}
	}

	public override void _Draw()
	{
		// storyboard area outline (4:3 safe area dashed inside the 16:9 frame)
		var p = Host._player;
		var a = p.SbToLocal(new Vector2(0, 0));
		var b = p.SbToLocal(new Vector2(640, 480));
		DrawDashedLine(new Vector2(a.X, 0), new Vector2(a.X, Size.Y), new Color(1, 1, 1, 0.15f), 1, 6);
		DrawDashedLine(new Vector2(b.X, 0), new Vector2(b.X, Size.Y), new Color(1, 1, 1, 0.15f), 1, 6);

		var corners = Host.SelectedCorners();
		var colour = Host.Editable ? new Color(1f, 0.85f, 0.2f) : new Color(0.6f, 0.6f, 0.6f);
		if (corners != null)
		{
			for (int i = 0; i < 4; i++) DrawLine(corners[i], corners[(i + 1) % 4], colour, 3);
		}
		var anchor = Host.SelectedAnchor();
		if (anchor is { } at)
		{
			DrawLine(at - new Vector2(10, 0), at + new Vector2(10, 0), colour, 3);
			DrawLine(at - new Vector2(0, 10), at + new Vector2(0, 10), colour, 3);
			if (!Host.SelectedVisibleNow)
				DrawString(GetThemeDefaultFont(), at + new Vector2(13, -8), "not visible now", HorizontalAlignment.Left, -1, 15, colour);
		}
	}
}
