using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.IO.Compression;
using System.IO;
using System.Linq;

public partial class SkinEditor : Control
{
	Node currentScene;
	private SettingsOperator SettingsOperator { get; set; }
	private VBoxContainer Menu { get; set; }
	private HBoxContainer ViewPoints { get; set; }
	private PopupMenu FileMenu { get; set; }
	public override void _Ready()
	{
		Skin.PreElement = Skin.Element;
		currentScene = GetTree().CurrentScene;
		SettingsOperator = GetNode<SettingsOperator>("/root/SettingsOperator");
		Menu = GetNode<VBoxContainer>("Creativity/VBoxContainer/ScrollContainer/Elements");
		ViewPoints = GetNode<HBoxContainer>("ToolBar/VBoxContainer/PartA/ViewPoints");
		FileMenu = GetNode<MenuButton>("ToolBar/VBoxContainer/PartA/File").GetPopup();
		FileMenu.Connect("id_pressed", new Callable(this, nameof(FileID)));
		SettingsOperator.toppaneltoggle(false);
		SetupCanvas();
		SetupEditMenu();
		SetupInspector();
		Check();
	}
	private void Export()
	{
		GD.Print("Save changes before exporting");
		SaveChanges();
		GD.Print("Should be saved..");
		var path = Skin.Element.SkinPath;
		if (Skin.Element.SkinPath == null)
		{
			path = SettingsOperator.skinsdir.PathJoin(Skin.Element.Name);
		}
		ZipFile.CreateFromDirectory(path, SettingsOperator.exportdir.PathJoin($"{Skin.Element.Name}.qsk"), CompressionLevel.Optimal, true);
		Notify.Post($"Exported {Skin.Element.Name}.\nClick to view.", $"file://{SettingsOperator.exportdir}");
	}
	private Dictionary<string, object> BuildSettings() => new Dictionary<string, object> // Settings for the skin!!!!
	{
		{"Name" , Skin.Element.Name},
		{"NoteLane1" , Skin.Element.LaneNotes[0].ToHtml(false)},
		{"NoteLane2" , Skin.Element.LaneNotes[1].ToHtml(false)},
		{"NoteLane3" , Skin.Element.LaneNotes[2].ToHtml(false)},
		{"NoteLane4" , Skin.Element.LaneNotes[3].ToHtml(false)},
		{"Layout" , Skin.Element.Layout},
	};

	private static bool HasWritablePath(SkinningLegend skin) =>
		!string.IsNullOrEmpty(skin.SkinPath) && !skin.SkinPath.StartsWith("res://") && Directory.Exists(skin.SkinPath);

	private static void WriteSettings(string path, Dictionary<string, object> settings)
	{
		using var saveFile = Godot.FileAccess.Open(path.PathJoin("settings.json"), Godot.FileAccess.ModeFlags.Write);
		if (saveFile == null)
		{
			GD.PrintErr($"[Qlute] [skin] Failed to write settings.json: {Godot.FileAccess.GetOpenError()}");
			return;
		}
		saveFile.StoreString(JsonSerializer.Serialize(settings));
	}

	private const string DefaultSkinName = "Slia (Qlute's Default skin 2025)";

	private static bool IsBuiltIn(SkinningLegend skin) =>
		string.IsNullOrEmpty(skin.SkinPath) || skin.SkinPath.StartsWith("res://");

	/// <summary>A folder name in the skins directory that no other skin uses yet.</summary>
	private static string UniqueSkinName(string name, SkinningLegend self)
	{
		var invalid = System.IO.Path.GetInvalidFileNameChars();
		var clean = new string(name.Where(ch => !invalid.Contains(ch)).ToArray()).Trim();
		if (clean.Length == 0 || clean == DefaultSkinName) clean = "Slia (Custom)";
		var candidate = clean;
		for (int n = 2; Directory.Exists(SettingsOperator.skinsdir.PathJoin(candidate)) || Skin.List.Any(s => s != self && s.Name == candidate); n++)
			candidate = $"{clean} ({n})";
		return candidate;
	}

	private void SaveChanges()
	{
		_autoSaveIn = -1; // a full save covers any pending auto-save
		SkinningLegend PreElement = Skin.Element;
		bool fork = IsBuiltIn(PreElement);
		if (PreElement.Name.Length < 1)
		{
			PreElement.Name = DefaultSkinName;
		}
		var path = PreElement.SkinPath;
		if (fork)
		{
			// The built-in skin can't be written to: save the edits as a brand new skin instead.
			PreElement.Name = UniqueSkinName(PreElement.Name, PreElement);
			path = SettingsOperator.skinsdir.PathJoin(PreElement.Name);
		}
		System.IO.Directory.CreateDirectory(path);
		var index = 0;
		foreach (Texture2D img in new List<Texture2D>([PreElement.NoteBack, PreElement.NoteFore, PreElement.Cursor, PreElement.Perfect, PreElement.Good, PreElement.Bad, PreElement.FullCombo, PreElement.JudgePerfect, PreElement.JudgeGreat, PreElement.JudgeMeh, PreElement.JudgeMiss]))
		{
			Error resultPng = img.GetImage().SavePng(path.PathJoin(Skin.ImageNames[index]));
			if (resultPng == Error.Ok)
			{
				GD.Print($"Image saved successfully to: {path.PathJoin(Skin.ImageNames[index])}");
			}
			else
			{
				GD.PrintErr($"Failed to save image to PNG: {resultPng}");
			}
			index++;
		}
		WriteSettings(path, BuildSettings());
		PreElement.SkinPath = path;
		if (fork)
		{
			// Put an untouched default skin back in its slot and switch to the new copy.
			int defaultIdx = Skin.List.IndexOf(PreElement);
			if (defaultIdx >= 0)
				Skin.List[defaultIdx] = new SkinningLegend { Name = DefaultSkinName, ID = defaultIdx };
			PreElement.ID = Skin.List.Count;
			Skin.List.Add(PreElement);
			Skin.SkinIndex = PreElement.ID;
			SettingsOperator.SetSetting("skin", path);
			Notify.Post($"Default skin changed, saved as new skin \"{PreElement.Name}\"");
			return;
		}
		Notify.Post($"Saved {Skin.Element.Name}");
	}

	// ------------------------------------------------------------------ auto-save

	private const double AutoSaveDelay = 0.4; // seconds; batches rapid edits (spin boxes, nudges)
	private double _autoSaveIn = -1;

	/// <summary>Queues a save after every committed layout change (place, move, scale, rotate, delete, undo...).</summary>
	public void QueueAutoSave() => _autoSaveIn = AutoSaveDelay;

	private void TickAutoSave(double delta)
	{
		if (_autoSaveIn < 0) return;
		_autoSaveIn -= delta;
		if (_autoSaveIn <= 0) AutoSave();
	}

	private void AutoSave()
	{
		_autoSaveIn = -1;
		if (!IsBuiltIn(Skin.Element) && HasWritablePath(Skin.Element))
		{
			// Rewriting settings.json is enough (layout, colours, name) and stays quiet.
			WriteSettings(Skin.Element.SkinPath, BuildSettings());
		}
		else
		{
			// Built-in skin (or missing folder): a full save forks / recreates it.
			SaveChanges();
		}
	}

	public override void _ExitTree()
	{
		if (_autoSaveIn >= 0) AutoSave(); // don't lose the last edit when the editor closes
	}

	private bool MissingFunction { get; set; } = false;
	private Container MissingDisclaimer { get; set; } = null;

	private void Check()
	{
		bool inGameplay = SkinLayout.GameplayRoot(GetTree()) != null;
		if (Canvas != null) Canvas.Visible = inGameplay && Settings == null;
		if (Inspector != null) Inspector.Visible = inGameplay;
		if (GetTree().CurrentScene == null && MissingFunction == false)
		{
			MissingFunction = true;
			Menu.Hide();
		}
		else if (GetTree().CurrentScene.Name == "Gameplay" && MissingFunction == true)
		{
			MissingFunction = false;
			Menu.Show();
		}
		else if (GetTree().CurrentScene.Name != "Gameplay" && MissingFunction == false)
		{
			MissingFunction = true;
			Menu.Hide();
		}
	}
	public override void _Process(double delta)
	{
		Check();
		RefreshComponentList();
		TickAutoSave(delta);
	}

	private void _on_back()
	{
		Global._SkinStart = !Global._SkinStart;
	}
	private void FileID(int index)
	{
		if (index == 0)
		{
			SaveChanges();
		}
		else if (index == 1)
		{
			Export();
		}
		else if (index == 2)
		{
			string uri = "file://" + Skin.Element.SkinPath;
        	OS.ShellOpen(uri);
		}
		else if (index == 3)
		{
			var path = Skin.Element.SkinPath;
			if (!string.IsNullOrEmpty(path) && !path.StartsWith("res://") && Directory.Exists(path))
			{
				var reloaded = Skin.ReloadSkin(path);
				reloaded.ID = Skin.Element.ID;
				int idx = Skin.List.IndexOf(Skin.Element);
				if (idx >= 0) Skin.List[idx] = reloaded;
				Skin.Element = reloaded;
			}
			Notify.Post($"Reloaded {Skin.Element.Name}");
		}
	}
	private PanelContainer Settings { get; set; }
	private void _Game()
	{
		if (Settings != null)
		{
			Settings.QueueFree();
			Settings = null;
		}
	}
	private void _Settings()
	{
		if (Settings == null)
		{
			Settings = GD.Load<PackedScene>("res://Panels/SkinEditorElements/Settings.tscn").Instantiate().GetNode<PanelContainer>(".");
			AddChild(Settings);
			Settings.Size = new Vector2(Settings.Size.X, Settings.Size.Y - 150);
			Settings.Position = new Vector2(Settings.Position.X, 100);
		}
	}

	// ------------------------------------------------------------------ layout editing

	private SkinEditorCanvas Canvas { get; set; }
	private VBoxContainer Inspector { get; set; }
	private VBoxContainer ComponentList { get; set; }
	private string _componentSignature = "";
	private Label SelectedName { get; set; }
	private Container Properties { get; set; }
	private OptionButton AnchorOption { get; set; }
	private OptionButton OriginOption { get; set; }
	private SpinBox PosX { get; set; }
	private SpinBox PosY { get; set; }
	private SpinBox ScaleX { get; set; }
	private SpinBox ScaleY { get; set; }
	private SpinBox RotationBox { get; set; }
	private CheckBox VisibleBox { get; set; }
	private Button RemoveButton { get; set; }
	private PopupMenu EditMenu { get; set; }

	private enum EditId { Cut = 0, Copy = 1, Paste = 2, Duplicate = 3, Delete = 4, ResetSelected = 5, ResetAll = 6, Undo = 10, Redo = 11 }

	private void SetupCanvas()
	{
		Canvas = new SkinEditorCanvas { Name = "LayoutCanvas" };
		AddChild(Canvas);
		MoveChild(Canvas, 0); // below every panel so the sidebars keep their input
		Canvas.SetAnchorsPreset(LayoutPreset.FullRect);
		Canvas.OffsetLeft = 222;
		Canvas.OffsetTop = 104;
		Canvas.OffsetRight = -230;
		Canvas.OffsetBottom = -50;
		Canvas.SelectionChanged += RefreshInspector;
		Canvas.LayoutChanged += RefreshInspector;
		Canvas.LayoutChanged += QueueAutoSave;
	}

	private void SetupEditMenu()
	{
		EditMenu = GetNode<MenuButton>("ToolBar/VBoxContainer/PartA/Edit").GetPopup();
		EditMenu.Clear();
		EditMenu.AddItem("Undo  (Ctrl+Z)", (int)EditId.Undo);
		EditMenu.AddItem("Redo  (Ctrl+Y)", (int)EditId.Redo);
		EditMenu.AddSeparator();
		EditMenu.AddItem("Cut  (Ctrl+X)", (int)EditId.Cut);
		EditMenu.AddItem("Copy  (Ctrl+C)", (int)EditId.Copy);
		EditMenu.AddItem("Paste  (Ctrl+V)", (int)EditId.Paste);
		EditMenu.AddItem("Duplicate  (Ctrl+D)", (int)EditId.Duplicate);
		EditMenu.AddItem("Delete  (Del)", (int)EditId.Delete);
		EditMenu.AddSeparator();
		EditMenu.AddItem("Reset selected", (int)EditId.ResetSelected);
		EditMenu.AddItem("Reset whole layout", (int)EditId.ResetAll);
		EditMenu.AboutToPopup += () =>
		{
			bool sel = Canvas.Selected != null;
			void Set(EditId id, bool enabled) => EditMenu.SetItemDisabled(EditMenu.GetItemIndex((int)id), !enabled);
			Set(EditId.Undo, Canvas.CanUndo);
			Set(EditId.Redo, Canvas.CanRedo);
			Set(EditId.Cut, sel);
			Set(EditId.Copy, sel);
			Set(EditId.Paste, Canvas.HasClipboard);
			Set(EditId.Duplicate, sel);
			Set(EditId.Delete, sel);
			Set(EditId.ResetSelected, sel);
			Set(EditId.ResetAll, Skin.Element.Layout.Count > 0);
		};
		EditMenu.IdPressed += id =>
		{
			switch ((EditId)id)
			{
				case EditId.Undo: Canvas.Undo(); break;
				case EditId.Redo: Canvas.Redo(); break;
				case EditId.Cut: Canvas.Cut(); break;
				case EditId.Copy: Canvas.Copy(); break;
				case EditId.Paste: Canvas.Paste(); break;
				case EditId.Duplicate: Canvas.Copy(); Canvas.Paste(); break;
				case EditId.Delete: Canvas.DeleteSelected(); break;
				case EditId.ResetSelected: Canvas.ResetSelected(); break;
				case EditId.ResetAll: Canvas.ResetAll(); break;
			}
		};
	}

	/// <summary>Called by the component previews in the left sidebar.</summary>
	public void AddComponent(string scenePath)
	{
		Canvas?.AddComponent(scenePath);
	}

	private static Label Header(string text)
	{
		var l = new Label { Text = text };
		l.AddThemeColorOverride("font_color", new Color(0, 0.765625f, 1));
		l.AddThemeFontSizeOverride("font_size", 18);
		return l;
	}

	private SpinBox AddSpin(Container parent, string label, double min, double max, double step, Action<SkinLayoutEntry, float> set)
	{
		var row = new HBoxContainer();
		row.AddChild(new Label { Text = label, CustomMinimumSize = new Vector2(70, 0) });
		var spin = new SpinBox
		{
			MinValue = min, MaxValue = max, Step = step,
			AllowGreater = true, AllowLesser = true,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
		};
		spin.ValueChanged += v => EditSelected(e => set(e, (float)v));
		row.AddChild(spin);
		parent.AddChild(row);
		return spin;
	}

	private OptionButton AddOption(Container parent, string label, IEnumerable<string> items, Action<int> picked)
	{
		parent.AddChild(new Label { Text = label });
		var opt = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, FitToLongestItem = false };
		foreach (var item in items) opt.AddItem(item);
		opt.ItemSelected += i => picked((int)i);
		parent.AddChild(opt);
		return opt;
	}

	private void SetupInspector()
	{
		var rows = GetNode<VBoxContainer>("Extras/Scroll/Rows");
		Inspector = new VBoxContainer();
		Inspector.AddThemeConstantOverride("separation", 6);
		rows.AddChild(Inspector);

		Inspector.AddChild(Header("Components"));
		ComponentList = new VBoxContainer();
		Inspector.AddChild(ComponentList);
		Inspector.AddChild(new HSeparator());

		Inspector.AddChild(Header("Selected"));
		SelectedName = new Label { Text = "Nothing selected", AutowrapMode = TextServer.AutowrapMode.WordSmart };
		Inspector.AddChild(SelectedName);

		var props = new VBoxContainer();
		props.AddThemeConstantOverride("separation", 6);
		Properties = props;
		Inspector.AddChild(props);

		AnchorOption = AddOption(props, "Anchor", new[] { "Closest (auto)" }.Concat(SkinLayout.PointNames), i => EditSelected((c, e) =>
		{
			if (i == 0)
			{
				e.ClosestAnchor = true;
				SkinLayout.UpdateClosestAnchor(c, e);
			}
			else
			{
				e.ClosestAnchor = false;
				e.Anchor = i - 1;
				SkinLayout.SetOffsetFromCurrent(c, e);
			}
		}));
		OriginOption = AddOption(props, "Origin", SkinLayout.PointNames, i => EditSelected((c, e) =>
		{
			e.Origin = i;
			SkinLayout.SetOffsetFromCurrent(c, e);
		}));

		PosX = AddSpin(props, "X", -4000, 4000, 1, (e, v) => e.X = v);
		PosY = AddSpin(props, "Y", -4000, 4000, 1, (e, v) => e.Y = v);
		ScaleX = AddSpin(props, "Scale X", 0.05, 10, 0.01, (e, v) => e.ScaleX = Math.Max(0.05f, v));
		ScaleY = AddSpin(props, "Scale Y", 0.05, 10, 0.01, (e, v) => e.ScaleY = Math.Max(0.05f, v));
		RotationBox = AddSpin(props, "Rotation", -180, 180, 1, (e, v) => e.Rotation = v);

		VisibleBox = new CheckBox { Text = "Visible" };
		VisibleBox.Toggled += on => EditSelected(e => e.Visible = on);
		props.AddChild(VisibleBox);

		var buttons = new HBoxContainer();
		var reset = new Button { Text = "Reset", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		reset.Pressed += () => Canvas.ResetSelected();
		RemoveButton = new Button { Text = "Remove", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		RemoveButton.Pressed += () => Canvas.DeleteSelected();
		buttons.AddChild(reset);
		buttons.AddChild(RemoveButton);
		props.AddChild(buttons);

		Inspector.AddChild(new HSeparator());
		Inspector.AddChild(new Label
		{
			Text = "Drag to move. Drag box handles to scale (Shift keeps ratio). Drag the top knob to rotate (Shift snaps). Arrows nudge (Shift = 10px). Click a preview on the left to add it.",
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			Modulate = new Color(1, 1, 1, 0.6f),
		});
		RefreshInspector();
	}

	private void EditSelected(Action<SkinLayoutEntry> edit) => EditSelected((_, e) => edit(e));

	private void EditSelected(Action<Control, SkinLayoutEntry> edit)
	{
		var c = Canvas?.Selected;
		if (c == null) return;
		Canvas.Change(() =>
		{
			var e = Canvas.EntryFor(c);
			SkinLayout.Apply(c, e);
			edit(c, e);
			SkinLayout.Apply(c, e);
		});
		RefreshInspector();
	}

	private void RefreshInspector()
	{
		if (Properties == null) return;
		var c = Canvas?.Selected;
		Properties.Visible = c != null;
		if (c == null)
		{
			SelectedName.Text = "Nothing selected";
			return;
		}
		var e = Canvas.EntryFor(c, create: false);
		bool added = c.HasMeta(SkinLayout.AddedMeta);
		SelectedName.Text = added ? $"{c.SceneFilePath.GetFile().GetBaseName()} ({Canvas.SelectedKey})" : Canvas.SelectedKey;
		AnchorOption.Select(e.ClosestAnchor ? 0 : e.Anchor + 1);
		AnchorOption.TooltipText = $"Current anchor: {SkinLayout.PointNames[e.Anchor]}";
		OriginOption.Select(e.Origin);
		var focus = GetViewport().GuiGetFocusOwner();
		void Set(SpinBox box, float v)
		{
			if (focus != null && box.IsAncestorOf(focus)) return; // don't fight the user's typing
			box.SetValueNoSignal(v);
		}
		Set(PosX, e.X);
		Set(PosY, e.Y);
		Set(ScaleX, e.ScaleX);
		Set(ScaleY, e.ScaleY);
		Set(RotationBox, e.Rotation);
		VisibleBox.SetPressedNoSignal(e.Visible);
		RemoveButton.Text = added ? "Remove" : "Hide";
		RemoveButton.Disabled = !added && !e.Visible;
		_componentSignature = ""; // refresh highlight
	}

	private void RefreshComponentList()
	{
		if (ComponentList == null || Canvas == null) return;
		var layout = Skin.Element.Layout;
		var sig = string.Join("|", Canvas.Elements.Select(c =>
		{
			var k = SkinLayout.KeyOf(c);
			bool hidden = layout.TryGetValue(k, out var e) && !e.Visible;
			return $"{k}:{hidden}:{k == Canvas.SelectedKey}";
		}));
		if (sig == _componentSignature) return;
		_componentSignature = sig;
		foreach (var child in ComponentList.GetChildren()) child.QueueFree();
		foreach (var c in Canvas.Elements.AsEnumerable().Reverse())
		{
			var key = SkinLayout.KeyOf(c);
			bool hidden = layout.TryGetValue(key, out var e) && !e.Visible;
			var name = c.HasMeta(SkinLayout.AddedMeta) ? $"{c.SceneFilePath.GetFile().GetBaseName()} ({key})" : key;
			var b = new Button
			{
				Text = hidden ? $"{name}  (hidden)" : name,
				ToggleMode = true,
				ButtonPressed = key == Canvas.SelectedKey,
				Alignment = HorizontalAlignment.Left,
				ClipText = true,
				Modulate = hidden ? new Color(1, 1, 1, 0.5f) : Colors.White,
			};
			b.Pressed += () => Canvas.SelectKey(key);
			ComponentList.AddChild(b);
		}
	}
}
