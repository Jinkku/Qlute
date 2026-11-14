using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using QluteShader;

namespace CreateEditor;

/// <summary>
/// Shader tab of the Create editor: build a beatmap shader by wiring nodes (no code), with a live preview
/// over the map's background and storyboard. Saved as <see cref="ShaderGraph.FileName"/> in the set folder.
/// </summary>
public partial class ShaderEditor : Control
{
	public Create Editor { get; set; }

	private ShaderGraph Graph => Editor.ShaderGraph;
	private GraphEdit _graph;
	private PopupMenu _addMenu;
	private Vector2 _addAt;
	private SubViewport _viewport;
	private TextureRect _previewBg;
	private StoryboardPlayer _previewSb;
	private BeatmapShaderLayer _layer;
	private OptionButton _target;
	private TextEdit _code;
	private Label _help;
	private bool _suppress;
	private bool _recompile;
	private readonly List<NodeDef> _menuDefs = new();
	private readonly Dictionary<int, GraphNode> _ui = new();

	private static readonly Color[] KindColours =
	{
		new(0.6f, 0.85f, 1f),   // float
		new(0.55f, 1f, 0.55f),  // vec2
		new(1f, 0.6f, 0.85f),   // colour
		new(1f, 1f, 1f),        // any
	};

	public override void _Ready()
	{
		var split = new HBoxContainer();
		split.AddThemeConstantOverride("separation", 0);
		AddChild(split);
		split.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

		_graph = new GraphEdit
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			RightDisconnects = true,
			ShowGrid = true,
			MinimapEnabled = false,
		};
		_graph.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color(0.07f, 0.08f, 0.07f) });
		_graph.AddThemeColorOverride("grid_major", new Color(1, 1, 1, 0.07f));
		_graph.AddThemeColorOverride("grid_minor", new Color(1, 1, 1, 0.03f));
		for (int a = 0; a < 4; a++)
			for (int b = 0; b < 4; b++)
				_graph.AddValidConnectionType(a, b);
		_graph.ConnectionRequest += OnConnect;
		_graph.DisconnectionRequest += OnDisconnect;
		_graph.DeleteNodesRequest += OnDelete;
		_graph.PopupRequest += at => OpenAddMenu(at);
		_graph.EndNodeMove += () => { SyncPositions(); Editor.MarkDirty(); };
		_graph.NodeSelected += n => ShowHelp(n as GraphNode);
		split.AddChild(_graph);

		_addMenu = new PopupMenu();
		foreach (var cat in new[] { "Input", "Distort", "Colour", "Math" })
		{
			_addMenu.AddSeparator(cat);
			foreach (var def in ShaderGraph.Defs.Values.Where(d => d.Category == cat))
			{
				_addMenu.AddItem(def.Title, _menuDefs.Count);
				_addMenu.SetItemTooltip(_addMenu.ItemCount - 1, def.Help ?? "");
				_menuDefs.Add(def);
			}
		}
		_addMenu.IdPressed += id => AddNode(_menuDefs[(int)id], _addAt);
		AddChild(_addMenu);

		split.AddChild(BuildSidePanel());
		Refresh();
	}

	private Control BuildSidePanel()
	{
		var panel = new PanelContainer { CustomMinimumSize = new Vector2(380, 0) };
		panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(0.08f, 0.1f, 0.08f, 0.94f), ContentMarginLeft = 10, ContentMarginRight = 10, ContentMarginTop = 10, ContentMarginBottom = 10,
		});
		var box = new VBoxContainer();
		panel.AddChild(box);
		box.AddChild(new Label { Text = "Preview", ThemeTypeVariation = "HeaderSmall" });

		var container = new SubViewportContainer { Stretch = true, CustomMinimumSize = new Vector2(360, 202) };
		_viewport = new SubViewport { HandleInputLocally = false, TransparentBg = false };
		container.AddChild(_viewport);
		box.AddChild(container);
		var black = new ColorRect { Color = Colors.Black };
		_viewport.AddChild(black);
		black.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		_previewBg = new TextureRect { ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered };
		_viewport.AddChild(_previewBg);
		_previewBg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		_previewSb = new StoryboardPlayer { TimeSource = () => Editor.CurrentMs, BackgroundToFollow = _previewBg, PlaySamples = false };
		_viewport.AddChild(_previewSb);
		_previewSb.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		_layer = new BeatmapShaderLayer { TimeSource = () => Editor.CurrentMs };
		_viewport.AddChild(_layer);
		_layer.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

		box.AddChild(new Label { Text = "Applies to" });
		_target = new OptionButton { FocusMode = FocusModeEnum.None };
		_target.AddItem("Background and storyboard");
		_target.AddItem("Whole screen (notes too)");
		_target.ItemSelected += i =>
		{
			if (_suppress) return;
			Editor.PushUndo();
			Graph.Target = ShaderGraph.Targets[(int)i];
			Changed();
		};
		box.AddChild(_target);

		var buttons = new HBoxContainer();
		var add = new Button { Text = "Add node", FocusMode = FocusModeEnum.None, SizeFlagsHorizontal = SizeFlags.ExpandFill };
		add.Pressed += () => OpenAddMenu(_graph.Size / 2 - new Vector2(80, 40));
		buttons.AddChild(add);
		var reset = new Button { Text = "Start over", FocusMode = FocusModeEnum.None, SizeFlagsHorizontal = SizeFlags.ExpandFill, TooltipText = "Back to an empty graph (no shader). Saving then removes the shader file." };
		reset.Pressed += () =>
		{
			Editor.PushUndo();
			Editor.ShaderGraph = ShaderGraph.CreateDefault();
			Editor.MarkDirty();
			Refresh();
		};
		buttons.AddChild(reset);
		box.AddChild(buttons);

		_help = new Label
		{
			Text = "Right-click the graph to add nodes. Drag from a dot on the right of a node to a dot on the left of another to connect them. " +
			       "Whatever reaches Output is shown during gameplay. Select a node and press Delete to remove it.",
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			Modulate = new Color(1, 1, 1, 0.7f),
			CustomMinimumSize = new Vector2(340, 0),
		};
		_help.AddThemeFontSizeOverride("font_size", 12);
		box.AddChild(_help);

		var showCode = new CheckBox { Text = "Show generated shader code", FocusMode = FocusModeEnum.None };
		box.AddChild(showCode);
		_code = new TextEdit { Editable = false, Visible = false, SizeFlagsVertical = SizeFlags.ExpandFill, WrapMode = TextEdit.LineWrappingMode.None };
		_code.AddThemeFontSizeOverride("font_size", 11);
		showCode.Toggled += on => { _code.Visible = on; _recompile = true; };
		box.AddChild(_code);
		return panel;
	}

	// =====================================================================
	// Graph <-> UI
	// =====================================================================

	/// <summary>Rebuild the node UI from the editor's graph (load, undo, start over).</summary>
	public void Refresh()
	{
		if (_graph == null || Editor?.ShaderGraph == null) return;
		_suppress = true;
		_graph.ClearConnections();
		foreach (var n in _ui.Values) { _graph.RemoveChild(n); n.QueueFree(); }
		_ui.Clear();
		foreach (var node in Graph.Nodes) CreateNodeUi(node);
		foreach (var l in Graph.Links)
			if (_ui.ContainsKey(l.From) && _ui.ContainsKey(l.To))
				_graph.ConnectNode(_ui[l.From].Name, l.FromPort, _ui[l.To].Name, l.ToPort);
		_target.Select(Math.Max(0, Array.IndexOf(ShaderGraph.Targets, Graph.Target)));
		_suppress = false;
		UpdateInputVisibility();
		RefreshPreviewSources();
		_recompile = true;
	}

	/// <summary>Background / storyboard behind the preview (call when they change).</summary>
	public void RefreshPreviewSources()
	{
		if (_previewBg == null) return;
		_previewBg.Texture = Editor.BackgroundTexture;
		if (Editor.Storyboard != null)
		{
			Editor.Storyboard.CompileAll();
			_previewSb.Load(Editor.Storyboard, Editor.SetFolderPath, async: false);
		}
		var beats = new BeatClock();
		foreach (var tp in Editor.Map.TimingPoints) beats.Add(tp.Offset, tp.BeatLength, tp.Kiai);
		beats.Sort();
		_layer.Beats = beats;
	}

	private GraphNode CreateNodeUi(ShaderGraphNode node)
	{
		var def = ShaderGraph.Defs[node.Type];
		var g = new GraphNode
		{
			Name = "n" + node.Id,
			Title = def.Title,
			PositionOffset = new Vector2(node.X, node.Y),
			TooltipText = def.Help ?? "",
			CustomMinimumSize = new Vector2(150, 0),
		};
		g.SetMeta("id", node.Id);
		StyleNode(g, def.Category);
		int rows = Math.Max(def.Inputs.Length, def.Outputs.Length);
		for (int r = 0; r < rows; r++)
		{
			var row = new HBoxContainer();
			if (r < def.Inputs.Length)
			{
				var port = def.Inputs[r];
				row.AddChild(new Label { Text = port.Name });
				if (port.Editable)
				{
					int idx = r;
					var spin = new SpinBox
					{
						MinValue = -100000, MaxValue = 100000, Step = 0.001, AllowGreater = true, AllowLesser = true,
						Value = node.Get("in" + r, port.Default), CustomMinimumSize = new Vector2(80, 0),
						SelectAllOnFocus = true, Name = "Value",
					};
					spin.ValueChanged += v =>
					{
						if (_suppress) return;
						Editor.PushUndo($"sh:{node.Id}:{idx}");
						node.Values["in" + idx] = (float)v;
						Changed();
					};
					row.AddChild(spin);
				}
			}
			var spacer = new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill };
			row.AddChild(spacer);
			if (r < def.Outputs.Length)
				row.AddChild(new Label { Text = def.Outputs[r].name, HorizontalAlignment = HorizontalAlignment.Right });
			g.AddChild(row);

			bool hasIn = r < def.Inputs.Length, hasOut = r < def.Outputs.Length;
			int inKind = hasIn ? (int)def.Inputs[r].Kind : 0;
			int outKind = hasOut ? (int)def.Outputs[r].kind : 0;
			g.SetSlot(r, hasIn, inKind, KindColours[inKind], hasOut, outKind, KindColours[outKind]);
		}

		// constants edited on the node
		if (def.IsColorConstant)
		{
			var picker = new ColorPickerButton
			{
				Color = new Color(node.Get("r", 1), node.Get("g", 0.4f), node.Get("b", 0.8f), node.Get("a", 1)),
				CustomMinimumSize = new Vector2(120, 28),
			};
			picker.ColorChanged += c =>
			{
				Editor.PushUndo($"sh:{node.Id}:colour");
				node.Values["r"] = c.R; node.Values["g"] = c.G; node.Values["b"] = c.B; node.Values["a"] = c.A;
				Changed();
			};
			g.AddChild(picker);
		}
		else
		{
			foreach (var (fname, fdef) in def.Fields)
			{
				var spin = new SpinBox
				{
					MinValue = -100000, MaxValue = 100000, Step = 0.001, AllowGreater = true, AllowLesser = true,
					Value = node.Get(fname, fdef), SelectAllOnFocus = true,
				};
				spin.ValueChanged += v =>
				{
					if (_suppress) return;
					Editor.PushUndo($"sh:{node.Id}:{fname}");
					node.Values[fname] = (float)v;
					Changed();
				};
				g.AddChild(spin);
			}
		}
		_graph.AddChild(g);
		_ui[node.Id] = g;
		return g;
	}

	private static readonly Dictionary<string, Color> CategoryColours = new()
	{
		["Output"] = new Color(0.55f, 0.25f, 0.45f), ["Input"] = new Color(0.2f, 0.35f, 0.5f),
		["Distort"] = new Color(0.25f, 0.45f, 0.25f), ["Colour"] = new Color(0.5f, 0.3f, 0.5f), ["Math"] = new Color(0.35f, 0.35f, 0.35f),
	};

	/// <summary>Solid node panels with a title bar coloured by category (the theme's are see-through).</summary>
	private static void StyleNode(GraphNode g, string category)
	{
		var title = CategoryColours.TryGetValue(category, out var c) ? c : new Color(0.3f, 0.3f, 0.3f);
		StyleBoxFlat Box(Color bg, int top, int bottom, bool border) => new()
		{
			BgColor = bg,
			CornerRadiusTopLeft = top, CornerRadiusTopRight = top, CornerRadiusBottomLeft = bottom, CornerRadiusBottomRight = bottom,
			ContentMarginLeft = 10, ContentMarginRight = 10, ContentMarginTop = 4, ContentMarginBottom = 6,
			BorderColor = new Color(1f, 0.85f, 0.3f), BorderWidthLeft = border ? 2 : 0, BorderWidthRight = border ? 2 : 0,
			BorderWidthTop = border ? 2 : 0, BorderWidthBottom = border ? 2 : 0,
		};
		var body = new Color(0.13f, 0.14f, 0.15f, 0.98f);
		g.AddThemeStyleboxOverride("panel", Box(body, 0, 6, false));
		g.AddThemeStyleboxOverride("panel_selected", Box(body, 0, 6, true));
		g.AddThemeStyleboxOverride("titlebar", Box(title, 6, 0, false));
		g.AddThemeStyleboxOverride("titlebar_selected", Box(title.Lightened(0.15f), 6, 0, true));
	}

	/// <summary>A connected input doesn't use its typed-in value, so hide the box.</summary>
	private void UpdateInputVisibility()
	{
		foreach (var (id, g) in _ui)
		{
			var node = Graph.Nodes.FirstOrDefault(n => n.Id == id);
			if (node == null) continue;
			var def = ShaderGraph.Defs[node.Type];
			for (int r = 0; r < def.Inputs.Length && r < g.GetChildCount(); r++)
			{
				var spin = g.GetChild(r).GetNodeOrNull<SpinBox>("Value");
				if (spin != null) spin.Visible = !Graph.Links.Any(l => l.To == id && l.ToPort == r);
			}
		}
	}

	private int IdOf(StringName name) => _graph.GetNodeOrNull<GraphNode>(new NodePath(name.ToString()))?.GetMeta("id").AsInt32() ?? -1;

	private void SyncPositions()
	{
		foreach (var (id, g) in _ui)
		{
			var node = Graph.Nodes.FirstOrDefault(n => n.Id == id);
			if (node == null) continue;
			node.X = MathF.Round(g.PositionOffset.X);
			node.Y = MathF.Round(g.PositionOffset.Y);
		}
	}

	/// <summary>Called by Create before saving / snapshotting so dragged node positions are kept.</summary>
	public void SyncToGraph()
	{
		if (_graph != null && Editor?.ShaderGraph != null) SyncPositions();
	}

	private void OnConnect(StringName fromNode, long fromPort, StringName toNode, long toPort)
	{
		int from = IdOf(fromNode), to = IdOf(toNode);
		if (from < 0 || to < 0 || from == to || Reaches(to, from)) return; // no loops
		Editor.PushUndo();
		// an input takes one wire: replace the old one
		foreach (var old in Graph.Links.Where(l => l.To == to && l.ToPort == toPort).ToList())
		{
			Graph.Links.Remove(old);
			if (_ui.TryGetValue(old.From, out var oldUi)) _graph.DisconnectNode(oldUi.Name, old.FromPort, toNode, (int)toPort);
		}
		Graph.Links.Add(new ShaderGraphLink { From = from, FromPort = (int)fromPort, To = to, ToPort = (int)toPort });
		_graph.ConnectNode(fromNode, (int)fromPort, toNode, (int)toPort);
		Changed();
	}

	/// <summary>True when data flows from <paramref name="start"/> to <paramref name="target"/>.</summary>
	private bool Reaches(int start, int target)
	{
		var seen = new HashSet<int>();
		var stack = new Stack<int>();
		stack.Push(start);
		while (stack.Count > 0)
		{
			int n = stack.Pop();
			if (n == target) return true;
			if (!seen.Add(n)) continue;
			foreach (var l in Graph.Links.Where(l => l.From == n)) stack.Push(l.To);
		}
		return false;
	}

	private void OnDisconnect(StringName fromNode, long fromPort, StringName toNode, long toPort)
	{
		int from = IdOf(fromNode), to = IdOf(toNode);
		Editor.PushUndo();
		Graph.Links.RemoveAll(l => l.From == from && l.FromPort == fromPort && l.To == to && l.ToPort == toPort);
		_graph.DisconnectNode(fromNode, (int)fromPort, toNode, (int)toPort);
		Changed();
	}

	private void OnDelete(Godot.Collections.Array<StringName> names)
	{
		var ids = names.Select(IdOf).Where(id => id >= 0).ToList();
		ids.RemoveAll(id => Graph.Nodes.FirstOrDefault(n => n.Id == id)?.Type == "output");
		if (ids.Count == 0) return;
		Editor.PushUndo();
		SyncPositions();
		Graph.Nodes.RemoveAll(n => ids.Contains(n.Id));
		Graph.Links.RemoveAll(l => ids.Contains(l.From) || ids.Contains(l.To));
		Editor.MarkDirty();
		Refresh();
	}

	private void OpenAddMenu(Vector2 at)
	{
		_addAt = (at + _graph.ScrollOffset) / _graph.Zoom;
		// popups are embedded in the game window, so this is in the window's canvas coordinates
		_addMenu.Popup(new Rect2I((Vector2I)(_graph.GetGlobalRect().Position + at), Vector2I.Zero));
	}

	private void AddNode(NodeDef def, Vector2 at)
	{
		if (Graph.Nodes.Count >= ShaderGraph.MaxNodes)
		{
			Notify.Post($"A shader can have up to {ShaderGraph.MaxNodes} nodes.", Type: NotificationIcons.NotificationType.Warning);
			return;
		}
		Editor.PushUndo();
		SyncPositions();
		var node = new ShaderGraphNode
		{
			Id = Graph.Nodes.Count == 0 ? 1 : Graph.Nodes.Max(n => n.Id) + 1,
			Type = def.Type, X = MathF.Round(at.X), Y = MathF.Round(at.Y),
		};
		Graph.Nodes.Add(node);
		var ui = CreateNodeUi(node);
		_graph.SetSelected(ui);
		ShowHelp(ui);
		Changed();
	}

	private void ShowHelp(GraphNode g)
	{
		if (g == null || _help == null) return;
		var node = Graph.Nodes.FirstOrDefault(n => n.Id == g.GetMeta("id").AsInt32());
		if (node == null) return;
		var def = ShaderGraph.Defs[node.Type];
		_help.Text = $"{def.Title}: {def.Help ?? "no extra settings."}";
	}

	private void Changed()
	{
		Editor.MarkDirty();
		UpdateInputVisibility();
		_recompile = true;
	}

	public override void _Process(double delta)
	{
		if (!_recompile || Editor?.ShaderGraph == null || !IsVisibleInTree()) return;
		_recompile = false;
		var code = Graph.Compile();
		_layer.SetCode(code);
		_layer.Visible = Graph.DoesSomething();
		if (_code.Visible) _code.Text = code;
	}

	public void HandleDelete()
	{
		var selected = _graph.GetChildren().OfType<GraphNode>().Where(g => g.Selected).Select(g => (StringName)g.Name).ToArray();
		if (selected.Length > 0) OnDelete(new Godot.Collections.Array<StringName>(selected));
	}
}
