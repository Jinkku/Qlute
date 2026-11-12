using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Interactive overlay over the gameplay preview, in the style of osu!lazer's skin editor:
/// click to select, drag to move, drag the box handles to scale (Shift keeps aspect ratio),
/// drag the top knob to rotate (Shift snaps to 15°). Anchor and origin are drawn as a linked pair.
/// </summary>
public partial class SkinEditorCanvas : Control
{
	public event Action SelectionChanged;
	public event Action LayoutChanged;

	private static readonly Color Accent = new(1f, 0.6f, 0.1f);
	private static readonly Color AnchorColour = new(1f, 0.3f, 0.45f);
	private static readonly Color OriginColour = new(1f, 0.85f, 0.2f);
	private const float HandleRadius = 7f;
	private const float RotateDistance = 28f;
	private const float MinScale = 0.05f;

	private enum DragMode { None, Move, Scale, Rotate }

	public Control Root { get; private set; }
	private List<Control> _elements = new();
	/// <summary>Editable components in draw order (refreshed every frame; freed nodes filtered out).</summary>
	public List<Control> Elements
	{
		get
		{
			if (_elements.Any(c => !IsInstanceValid(c))) _elements = _elements.Where(IsInstanceValid).ToList();
			return _elements;
		}
		private set => _elements = value;
	}
	public string SelectedKey { get; private set; }
	public Control Selected => Elements.FirstOrDefault(c => SkinLayout.KeyOf(c) == SelectedKey);
	private static Dictionary<string, SkinLayoutEntry> Layout => Skin.Element.Layout;

	private DragMode _mode = DragMode.None;
	private Vector2I _handle;
	private Vector2 _lastMouse;
	private Vector2 _startMouse;
	private SkinLayoutEntry _startEntry;
	private Transform2D _startTransform;
	private Vector2 _startSize;
	private Control _hovered;

	private readonly List<string> _undo = new();
	private readonly List<string> _redo = new();
	private string _pending;
	private SkinLayoutEntry _clipboard;
	private string _clipboardScene;

	public bool CanUndo => _undo.Count > 0;
	public bool CanRedo => _redo.Count > 0;
	public bool HasClipboard => _clipboard != null;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Stop;
		FocusMode = FocusModeEnum.Click;
	}

	public override void _Process(double delta)
	{
		Root = SkinLayout.GameplayRoot(GetTree());
		Elements = SkinLayout.GetEditable(Root).Where(c => !c.IsQueuedForDeletion()).ToList();
		if (_hovered != null && !IsInstanceValid(_hovered)) _hovered = null;
		if (SelectedKey != null && Selected == null && !Layout.ContainsKey(SelectedKey))
			Select(null);
		QueueRedraw();
	}

	// ---------------------------------------------------------------- selection / layout access

	public void Select(Control c)
	{
		var key = c == null ? null : SkinLayout.KeyOf(c);
		if (key == SelectedKey) return;
		SelectedKey = key;
		SelectionChanged?.Invoke();
	}

	public void SelectKey(string key)
	{
		if (key == SelectedKey) return;
		SelectedKey = key;
		SelectionChanged?.Invoke();
	}

	/// <summary>The stored entry for an element, capturing its current placement the first time.</summary>
	public SkinLayoutEntry EntryFor(Control c, bool create = true)
	{
		var key = SkinLayout.KeyOf(c);
		if (Layout.TryGetValue(key, out var e)) return e;
		e = SkinLayout.Capture(c);
		if (create) Layout[key] = e;
		return e;
	}

	/// <summary>Call before a modification; pair with <see cref="EndChange"/>.</summary>
	public void BeginChange()
	{
		_pending ??= SkinLayout.Serialize(Layout);
	}

	public void EndChange()
	{
		if (_pending == null) return;
		var now = SkinLayout.Serialize(Layout);
		if (now != _pending)
		{
			_undo.Add(_pending);
			if (_undo.Count > 200) _undo.RemoveAt(0);
			_redo.Clear();
			LayoutChanged?.Invoke();
		}
		_pending = null;
	}

	/// <summary>Runs a single undoable modification.</summary>
	public void Change(Action action)
	{
		BeginChange();
		action();
		EndChange();
	}

	private void RestoreSnapshot(string json)
	{
		var restored = SkinLayout.Deserialize(json);
		Layout.Clear();
		foreach (var (k, v) in restored) Layout[k] = v;
		foreach (var c in Elements)
			if (Layout.TryGetValue(SkinLayout.KeyOf(c), out var e)) SkinLayout.Apply(c, e);
		LayoutChanged?.Invoke();
		SelectionChanged?.Invoke();
	}

	public void Undo()
	{
		if (_undo.Count == 0) return;
		_redo.Add(SkinLayout.Serialize(Layout));
		var snap = _undo[^1];
		_undo.RemoveAt(_undo.Count - 1);
		RestoreSnapshot(snap);
	}

	public void Redo()
	{
		if (_redo.Count == 0) return;
		_undo.Add(SkinLayout.Serialize(Layout));
		var snap = _redo[^1];
		_redo.RemoveAt(_redo.Count - 1);
		RestoreSnapshot(snap);
	}

	// ---------------------------------------------------------------- commands

	private string NewAddedKey()
	{
		int n = 1;
		while (Layout.ContainsKey($"Added:{n}")) n++;
		return $"Added:{n}";
	}

	private SkinLayoutApplier Applier => Root?.GetNodeOrNull<SkinLayoutApplier>("SkinLayoutApplier");

	/// <summary>Adds a new component from a scene, centred on the gameplay area.</summary>
	public void AddComponent(string scenePath, SkinLayoutEntry template = null)
	{
		var applier = Applier;
		if (applier == null) return;
		var key = NewAddedKey();
		var node = applier.AddComponent(key, scenePath);
		if (node == null) return;
		var e = template?.Clone() ?? new SkinLayoutEntry { Anchor = 4, Origin = 4 };
		e.Scene = scenePath;
		e.Visible = true;
		Change(() => Layout[key] = e);
		SkinLayout.Apply(node, e);
		SelectKey(key);
	}

	/// <summary>Removes added components; hides built-in ones (they can be shown again from the inspector).</summary>
	public void DeleteSelected()
	{
		var c = Selected;
		if (c == null) return;
		var key = SelectedKey;
		Change(() =>
		{
			if (c.HasMeta(SkinLayout.AddedMeta)) Layout.Remove(key);
			else EntryFor(c).Visible = false;
		});
		if (c.HasMeta(SkinLayout.AddedMeta)) Select(null);
		else SelectionChanged?.Invoke();
	}

	public void ResetSelected()
	{
		var c = Selected;
		if (c == null) return;
		var key = SelectedKey;
		Change(() =>
		{
			if (c.HasMeta(SkinLayout.AddedMeta))
			{
				var scene = Layout[key].Scene;
				Layout[key] = new SkinLayoutEntry { Scene = scene, Anchor = 4, Origin = 4 };
			}
			else Layout.Remove(key);
		});
		SelectionChanged?.Invoke();
	}

	public void ResetAll()
	{
		Change(() => Layout.Clear());
		Select(null);
	}

	public void Copy()
	{
		var c = Selected;
		if (c == null) return;
		_clipboard = EntryFor(c, create: false).Clone();
		_clipboardScene = c.HasMeta(SkinLayout.AddedMeta) ? Layout[SelectedKey].Scene : c.SceneFilePath;
	}

	public void Cut()
	{
		Copy();
		DeleteSelected();
	}

	/// <summary>Pastes the copied component as a new one, nudged so it doesn't sit exactly on top.</summary>
	public void Paste()
	{
		if (_clipboard == null || string.IsNullOrEmpty(_clipboardScene)) return;
		var t = _clipboard.Clone();
		t.X += 20; t.Y += 20;
		AddComponent(_clipboardScene, t);
	}

	public void Nudge(Vector2 by)
	{
		var c = Selected;
		if (c == null) return;
		Change(() =>
		{
			var e = EntryFor(c);
			e.X += by.X; e.Y += by.Y;
			SkinLayout.Apply(c, e);
			SkinLayout.UpdateClosestAnchor(c, e);
		});
		SelectionChanged?.Invoke();
	}

	// ---------------------------------------------------------------- geometry helpers

	private static Transform2D ParentGlobal(Control c) =>
		c.GetParent() is CanvasItem p ? p.GetGlobalTransform() : Transform2D.Identity;

	private static Transform2D ElementGlobal(Control c) => ParentGlobal(c) * c.GetTransform();

	private static Vector2 HandleLocal(Vector2I h, Vector2 size) =>
		new((h.X + 1) * 0.5f * size.X, (h.Y + 1) * 0.5f * size.Y);

	/// <summary>Handles worth showing; edge-middle handles are dropped when that edge is too short on screen.</summary>
	private static IEnumerable<Vector2I> Handles(Control c)
	{
		var t = ElementGlobal(c);
		float w = t.BasisXform(new Vector2(c.Size.X, 0)).Length();
		float h = t.BasisXform(new Vector2(0, c.Size.Y)).Length();
		for (int y = -1; y <= 1; y++)
			for (int x = -1; x <= 1; x++)
			{
				if (x == 0 && y == 0) continue;
				if (x == 0 && w < 48) continue;
				if (y == 0 && h < 48) continue;
				yield return new Vector2I(x, y);
			}
	}

	private static Vector2 RotateKnob(Control c)
	{
		var t = ElementGlobal(c);
		var top = t * new Vector2(c.Size.X / 2, 0);
		var up = -t.Y.Normalized();
		if (up == Vector2.Zero) up = Vector2.Up;
		return top + up * RotateDistance;
	}

	/// <summary>Applies the entry, then shifts the offset so <paramref name="local"/> lands on <paramref name="global"/>.</summary>
	private static void ApplyKeeping(Control c, SkinLayoutEntry e, Vector2 local, Vector2 global)
	{
		SkinLayout.Apply(c, e);
		var now = ElementGlobal(c) * local;
		var d = ParentGlobal(c).AffineInverse().BasisXform(global - now);
		e.X = MathF.Round(e.X + d.X, 2);
		e.Y = MathF.Round(e.Y + d.Y, 2);
		SkinLayout.Apply(c, e);
	}

	private Control HitElement(Vector2 global)
	{
		for (int i = Elements.Count - 1; i >= 0; i--)
		{
			var c = Elements[i];
			if (Layout.TryGetValue(SkinLayout.KeyOf(c), out var e) && !e.Visible) continue;
			if (c.Size.X <= 0 || c.Size.Y <= 0) continue;
			var local = ElementGlobal(c).AffineInverse() * global;
			if (new Rect2(Vector2.Zero, c.Size).HasPoint(local)) return c;
		}
		return null;
	}

	private bool HitHandle(Control c, Vector2 global, out Vector2I handle, out bool rotate)
	{
		handle = Vector2I.Zero;
		rotate = false;
		if (c == null) return false;
		if (RotateKnob(c).DistanceTo(global) <= HandleRadius + 2)
		{
			rotate = true;
			return true;
		}
		var t = ElementGlobal(c);
		foreach (var h in Handles(c))
		{
			if ((t * HandleLocal(h, c.Size)).DistanceTo(global) <= HandleRadius + 2)
			{
				handle = h;
				return true;
			}
		}
		return false;
	}

	// ---------------------------------------------------------------- input

	public override void _GuiInput(InputEvent @event)
	{
		if (Root == null) return;
		var mouse = @event is InputEventMouse me ? GetGlobalTransform() * me.Position : GetGlobalMousePosition();

		if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left)
		{
			if (mb.Pressed)
			{
				GrabFocus();
				var sel = Selected;
				if (HitHandle(sel, mouse, out var h, out var rotate))
				{
					StartDrag(sel, rotate ? DragMode.Rotate : DragMode.Scale, h, mouse);
				}
				else
				{
					var hit = HitElement(mouse);
					Select(hit);
					if (hit != null) StartDrag(hit, DragMode.Move, Vector2I.Zero, mouse);
				}
			}
			else if (_mode != DragMode.None)
			{
				var c = Selected;
				if (c != null && Layout.TryGetValue(SelectedKey, out var e))
				{
					SkinLayout.UpdateClosestAnchor(c, e);
					SkinLayout.Apply(c, e);
				}
				_mode = DragMode.None;
				EndChange();
				SelectionChanged?.Invoke();
			}
			AcceptEvent();
		}
		else if (@event is InputEventMouseMotion mm)
		{
			if (_mode != DragMode.None) Drag(mouse, mm.ShiftPressed);
			else UpdateHover(mouse);
			AcceptEvent();
		}
		else if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true })
		{
			Select(HitElement(mouse));
			AcceptEvent();
		}
	}

	private void UpdateHover(Vector2 mouse)
	{
		var sel = Selected;
		if (HitHandle(sel, mouse, out var h, out var rotate))
		{
			_hovered = sel;
			if (rotate) MouseDefaultCursorShape = CursorShape.Drag;
			else
			{
				// Pick the resize cursor that matches the handle direction on screen.
				var dir = ElementGlobal(sel).BasisXform(new Vector2(h.X, h.Y)).Normalized();
				float ang = Mathf.PosMod(Mathf.RadToDeg(dir.Angle()), 180f);
				MouseDefaultCursorShape = ang < 22.5f || ang >= 157.5f ? CursorShape.Hsize
					: ang < 67.5f ? CursorShape.Fdiagsize
					: ang < 112.5f ? CursorShape.Vsize
					: CursorShape.Bdiagsize;
			}
			return;
		}
		_hovered = HitElement(mouse);
		MouseDefaultCursorShape = _hovered != null ? CursorShape.Move : CursorShape.Arrow;
	}

	private void StartDrag(Control c, DragMode mode, Vector2I handle, Vector2 mouse)
	{
		BeginChange();
		var e = EntryFor(c);
		SkinLayout.Apply(c, e);
		_mode = mode;
		_handle = handle;
		_startMouse = _lastMouse = mouse;
		_startEntry = e.Clone();
		_startTransform = ElementGlobal(c);
		_startSize = c.Size;
	}

	private void Drag(Vector2 mouse, bool shift)
	{
		var c = Selected;
		if (c == null || !Layout.TryGetValue(SelectedKey, out var e))
		{
			_mode = DragMode.None;
			EndChange();
			return;
		}

		switch (_mode)
		{
			case DragMode.Move:
			{
				var d = ParentGlobal(c).AffineInverse().BasisXform(mouse - _lastMouse);
				e.X = MathF.Round(e.X + d.X, 2);
				e.Y = MathF.Round(e.Y + d.Y, 2);
				SkinLayout.Apply(c, e);
				SkinLayout.UpdateClosestAnchor(c, e);
				break;
			}
			case DragMode.Scale:
			{
				var size = _startSize;
				if (size.X <= 0 || size.Y <= 0) break;
				var local = _startTransform.AffineInverse() * mouse;
				float rx = _handle.X switch { 1 => local.X / size.X, -1 => (size.X - local.X) / size.X, _ => 1f };
				float ry = _handle.Y switch { 1 => local.Y / size.Y, -1 => (size.Y - local.Y) / size.Y, _ => 1f };
				if (shift)
				{
					float r = _handle.X != 0 && _handle.Y != 0
						? (Math.Abs(rx - 1) > Math.Abs(ry - 1) ? rx : ry)
						: _handle.X != 0 ? rx : ry;
					rx = ry = r;
				}
				e.ScaleX = MathF.Round(Math.Max(MinScale, _startEntry.ScaleX * rx), 3);
				e.ScaleY = MathF.Round(Math.Max(MinScale, _startEntry.ScaleY * ry), 3);
				// The opposite edge / corner stays put.
				var fixedLocal = HandleLocal(-_handle, size);
				ApplyKeeping(c, e, fixedLocal, _startTransform * fixedLocal);
				break;
			}
			case DragMode.Rotate:
			{
				var centreLocal = _startSize / 2;
				var centre = _startTransform * centreLocal;
				float delta = Mathf.RadToDeg((mouse - centre).Angle() - (_startMouse - centre).Angle());
				float rot = _startEntry.Rotation + delta;
				if (shift) rot = MathF.Round(rot / 15f) * 15f;
				rot = Mathf.PosMod(rot + 180f, 360f) - 180f;
				e.Rotation = MathF.Round(rot, 2);
				ApplyKeeping(c, e, centreLocal, centre);
				break;
			}
		}
		_lastMouse = mouse;
		SelectionChanged?.Invoke();
	}

	public override void _Input(InputEvent @event)
	{
		if (!IsVisibleInTree() || Root == null) return;
		if (@event is not InputEventKey { Pressed: true } key) return;
		var focus = GetViewport().GuiGetFocusOwner();
		if (focus is LineEdit || focus is TextEdit || focus is SpinBox) return;

		bool ctrl = key.CtrlPressed || key.MetaPressed;
		bool handled = true;
		float step = key.ShiftPressed ? 10f : 1f;
		switch (key.Keycode)
		{
			case Key.Z when ctrl && key.ShiftPressed: Redo(); break;
			case Key.Z when ctrl: Undo(); break;
			case Key.Y when ctrl: Redo(); break;
			case Key.C when ctrl && Selected != null: Copy(); break;
			case Key.X when ctrl && Selected != null: Cut(); break;
			case Key.V when ctrl && HasClipboard: Paste(); break;
			case Key.D when ctrl && Selected != null: Copy(); Paste(); break;
			case Key.Delete or Key.Backspace when Selected != null: DeleteSelected(); break;
			case Key.Escape when Selected != null: Select(null); break;
			case Key.Left when Selected != null: Nudge(new Vector2(-step, 0)); break;
			case Key.Right when Selected != null: Nudge(new Vector2(step, 0)); break;
			case Key.Up when Selected != null: Nudge(new Vector2(0, -step)); break;
			case Key.Down when Selected != null: Nudge(new Vector2(0, step)); break;
			default: handled = false; break;
		}
		if (handled) GetViewport().SetInputAsHandled();
	}

	// ---------------------------------------------------------------- drawing

	private void DrawQuad(Transform2D t, Vector2 size, Color colour, float width, bool dashed = false)
	{
		var pts = new[] { t * Vector2.Zero, t * new Vector2(size.X, 0), t * size, t * new Vector2(0, size.Y) };
		for (int i = 0; i < 4; i++)
		{
			if (dashed) DrawDashedLine(pts[i], pts[(i + 1) % 4], colour, width, 6f);
			else DrawLine(pts[i], pts[(i + 1) % 4], colour, width, true);
		}
	}

	public override void _Draw()
	{
		if (Root == null) return;
		DrawSetTransformMatrix(GetGlobalTransform().AffineInverse());
		var font = GetThemeDefaultFont();

		foreach (var c in Elements)
		{
			if (c == Selected) continue;
			bool hidden = Layout.TryGetValue(SkinLayout.KeyOf(c), out var e) && !e.Visible;
			if (hidden) continue;
			var col = c == _hovered ? new Color(1, 1, 1, 0.8f) : new Color(1, 1, 1, 0.18f);
			DrawQuad(ElementGlobal(c), c.Size, col, c == _hovered ? 2f : 1f);
		}

		var sel = Selected;
		if (sel == null) return;
		var entry = EntryFor(sel, create: false);
		var t = ElementGlobal(sel);
		var size = sel.Size;
		bool selHidden = !entry.Visible;
		var accent = selHidden ? new Color(0.6f, 0.6f, 0.6f) : Accent;

		DrawQuad(t, size, accent, 2f, selHidden);

		// Anchor (on parent) linked to origin (on element).
		var anchorGlobal = ParentGlobal(sel) * (SkinLayout.Frac(entry.Anchor) * SkinLayout.ParentSize(sel));
		var originGlobal = t * (SkinLayout.Frac(entry.Origin) * size);
		DrawDashedLine(anchorGlobal, originGlobal, new Color(AnchorColour, 0.8f), 1.5f, 5f);
		DrawColoredPolygon(new[]
		{
			anchorGlobal + new Vector2(0, -7), anchorGlobal + new Vector2(7, 0),
			anchorGlobal + new Vector2(0, 7), anchorGlobal + new Vector2(-7, 0),
		}, AnchorColour);
		DrawCircle(originGlobal, 5f, OriginColour);
		DrawCircle(originGlobal, 5f, Colors.Black, false, 1f);

		if (!selHidden)
		{
			var topCentre = t * new Vector2(size.X / 2, 0);
			var knob = RotateKnob(sel);
			DrawLine(topCentre, knob, accent, 1.5f, true);
			DrawCircle(knob, HandleRadius - 1, Colors.White);
			DrawCircle(knob, HandleRadius - 1, accent, false, 2f, true);

			foreach (var h in Handles(sel))
			{
				var p = t * HandleLocal(h, size);
				var r = new Rect2(p - Vector2.One * (HandleRadius - 2), Vector2.One * (HandleRadius - 2) * 2);
				DrawRect(r, Colors.White);
				DrawRect(r, accent, false, 2f);
			}
		}

		// Name + transform readout above the box.
		var label = sel.HasMeta(SkinLayout.AddedMeta) ? $"{sel.SceneFilePath.GetFile().GetBaseName()} ({SelectedKey})" : SelectedKey;
		if (selHidden) label += " (hidden)";
		var corners = new[] { t * Vector2.Zero, t * new Vector2(size.X, 0), t * size, t * new Vector2(0, size.Y) };
		var top = corners.MinBy(p => p.Y);
		var left = corners.Min(p => p.X);
		var textPos = new Vector2(left, top.Y - (selHidden ? 10 : RotateDistance + 12));
		DrawString(font, textPos + Vector2.One, label, HorizontalAlignment.Left, -1, 14, new Color(0, 0, 0, 0.7f));
		DrawString(font, textPos, label, HorizontalAlignment.Left, -1, 14, accent);
	}
}
