using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

/// <summary>
/// Placement of a single gameplay HUD component, stored per skin (settings.json -> "Layout").
/// Modelled after osu!lazer's skinnable info: an anchor point on the parent, an origin point on the
/// element, an offset between the two, plus scale and rotation.
/// </summary>
public class SkinLayoutEntry
{
	/// <summary>Scene path for components added through the skin editor. Null for built-in components.</summary>
	public string Scene { get; set; }
	/// <summary>Anchor on the parent, 0..8 (row * 3 + column, TopLeft = 0, BottomRight = 8).</summary>
	public int Anchor { get; set; }
	/// <summary>Origin on the element, 0..8, same layout as <see cref="Anchor"/>.</summary>
	public int Origin { get; set; }
	/// <summary>When true the anchor is re-picked from the element's position after every move.</summary>
	public bool ClosestAnchor { get; set; } = true;
	public float X { get; set; }
	public float Y { get; set; }
	public float ScaleX { get; set; } = 1;
	public float ScaleY { get; set; } = 1;
	public float Rotation { get; set; }
	public bool Visible { get; set; } = true;

	public SkinLayoutEntry Clone() => (SkinLayoutEntry)MemberwiseClone();
}

public static class SkinLayout
{
	public const string CustomizablePath = "res://Panels/GameplayElements/Customizable/";
	public const string AddedMeta = "skin_layout_key";
	public static readonly string[] PointNames =
	{
		"Top Left", "Top Centre", "Top Right",
		"Centre Left", "Centre", "Centre Right",
		"Bottom Left", "Bottom Centre", "Bottom Right",
	};

	public static Vector2 Frac(int point) => new Vector2(point % 3 * 0.5f, point / 3 * 0.5f);

	public static Vector2 ParentSize(Control c)
	{
		if (c.GetParent() is Control p) return p.Size;
		return c.GetViewportRect().Size;
	}

	/// <summary>The scene root of the active gameplay screen, or null.</summary>
	public static Control GameplayRoot(SceneTree tree)
	{
		return tree.CurrentScene is Control c && c.Name == "Gameplay" && GodotObject.IsInstanceValid(c) ? c : null;
	}

	public static string KeyOf(Control c)
	{
		if (c.HasMeta(AddedMeta)) return c.GetMeta(AddedMeta).AsString();
		return c.Name;
	}

	public static bool IsEditable(Control c)
	{
		if (c.HasMeta(AddedMeta)) return true;
		var scene = c.SceneFilePath;
		return !string.IsNullOrEmpty(scene)
			&& scene.StartsWith(CustomizablePath)
			&& !scene.GetFile().StartsWith("Playfield");
	}

	/// <summary>Editable components in draw order (last = top-most).</summary>
	public static List<Control> GetEditable(Control root)
	{
		var list = new List<Control>();
		if (root == null) return list;
		foreach (var child in root.GetChildren())
		{
			if (child is Control c && IsEditable(c)) list.Add(c);
			if (child.Name == "HUD")
			{
				foreach (var hudChild in child.GetChildren())
					if (hudChild is Control hc && IsEditable(hc)) list.Add(hc);
			}
		}
		return list;
	}

	/// <summary>Container new components get added to.</summary>
	public static Control AddTarget(Control root) => root?.GetNodeOrNull<Control>("HUD") ?? root;

	public static void Apply(Control c, SkinLayoutEntry e)
	{
		if (!e.Visible)
		{
			c.Visible = false;
			c.ProcessMode = Node.ProcessModeEnum.Disabled;
			return;
		}
		if (c.ProcessMode == Node.ProcessModeEnum.Disabled)
		{
			c.ProcessMode = Node.ProcessModeEnum.Inherit;
			c.Visible = true;
		}
		var pivot = Frac(e.Origin) * c.Size;
		c.PivotOffset = pivot;
		c.Scale = new Vector2(e.ScaleX, e.ScaleY);
		c.RotationDegrees = e.Rotation;
		c.Position = Frac(e.Anchor) * ParentSize(c) + new Vector2(e.X, e.Y) - pivot;
	}

	/// <summary>Where a point in the element's local (unscaled) space ends up in parent space.</summary>
	public static Vector2 LocalToParent(Control c, Vector2 local) => c.GetTransform() * local;

	public static int ClosestPoint(Vector2 point, Vector2 size)
	{
		int col = point.X < size.X / 3 ? 0 : point.X < size.X * 2 / 3 ? 1 : 2;
		int row = point.Y < size.Y / 3 ? 0 : point.Y < size.Y * 2 / 3 ? 1 : 2;
		return row * 3 + col;
	}

	/// <summary>Builds an entry that reproduces the element's current on-screen placement.</summary>
	public static SkinLayoutEntry Capture(Control c)
	{
		var psize = ParentSize(c);
		var centre = LocalToParent(c, c.Size / 2);
		int point = ClosestPoint(centre, psize);
		var e = new SkinLayoutEntry
		{
			Anchor = point,
			Origin = point,
			ScaleX = c.Scale.X,
			ScaleY = c.Scale.Y,
			Rotation = c.RotationDegrees,
			Visible = true,
		};
		SetOffsetFromCurrent(c, e);
		return e;
	}

	/// <summary>Recomputes X/Y so the element's origin stays where it currently is on screen.</summary>
	public static void SetOffsetFromCurrent(Control c, SkinLayoutEntry e)
	{
		var originParent = LocalToParent(c, Frac(e.Origin) * c.Size);
		var off = originParent - Frac(e.Anchor) * ParentSize(c);
		e.X = MathF.Round(off.X, 2);
		e.Y = MathF.Round(off.Y, 2);
	}

	/// <summary>Picks the closest anchor and adjusts the offset so nothing visibly moves.</summary>
	public static void UpdateClosestAnchor(Control c, SkinLayoutEntry e)
	{
		if (!e.ClosestAnchor) return;
		var centre = LocalToParent(c, c.Size / 2);
		int point = ClosestPoint(centre, ParentSize(c));
		if (point == e.Anchor) return;
		e.Anchor = point;
		SetOffsetFromCurrent(c, e);
	}

	public static string Serialize(Dictionary<string, SkinLayoutEntry> layout) => JsonSerializer.Serialize(layout);

	public static Dictionary<string, SkinLayoutEntry> Deserialize(string json)
	{
		try
		{
			return JsonSerializer.Deserialize<Dictionary<string, SkinLayoutEntry>>(json) ?? new();
		}
		catch (Exception ex)
		{
			GD.PrintErr($"[Qlute] [skin] Failed to read layout: {ex.Message}");
			return new();
		}
	}

	public static Dictionary<string, SkinLayoutEntry> CloneLayout(Dictionary<string, SkinLayoutEntry> layout) =>
		layout.ToDictionary(kv => kv.Key, kv => kv.Value.Clone());
}

/// <summary>
/// Lives under the gameplay scene root and keeps every HUD component placed according to the
/// current skin's layout. Re-applied every frame so content-driven size changes keep the origin fixed.
/// </summary>
public partial class SkinLayoutApplier : Node
{
	private struct Defaults
	{
		public float L, T, R, B;
		public Vector2 Pivot, Scale;
		public float Rotation;
		public bool Visible;
		public ProcessModeEnum Mode;
	}

	private readonly Dictionary<Control, Defaults> _defaults = new();
	private Dictionary<string, SkinLayoutEntry> _layout;
	private readonly HashSet<Control> _applied = new();
	private Control Root => GetParent() as Control;

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
		ProcessPriority = 1000;
		foreach (var c in SkinLayout.GetEditable(Root)) Remember(c);
	}

	private void Remember(Control c)
	{
		if (_defaults.ContainsKey(c)) return;
		_defaults[c] = new Defaults
		{
			L = c.OffsetLeft, T = c.OffsetTop, R = c.OffsetRight, B = c.OffsetBottom,
			Pivot = c.PivotOffset, Scale = c.Scale, Rotation = c.Rotation,
			Visible = c.Visible, Mode = c.ProcessMode,
		};
	}

	/// <summary>Puts a built-in component back the way the gameplay scene defined it.</summary>
	public void Restore(Control c)
	{
		if (!_defaults.TryGetValue(c, out var d)) return;
		c.OffsetLeft = d.L; c.OffsetTop = d.T; c.OffsetRight = d.R; c.OffsetBottom = d.B;
		c.PivotOffset = d.Pivot; c.Scale = d.Scale; c.Rotation = d.Rotation;
		c.Visible = d.Visible; c.ProcessMode = d.Mode;
	}

	public Control AddComponent(string key, string scenePath)
	{
		var target = SkinLayout.AddTarget(Root);
		if (target == null || !ResourceLoader.Exists(scenePath)) return null;
		if (GD.Load<PackedScene>(scenePath).Instantiate() is not Control node) return null;
		node.Name = key.Replace(":", "_");
		node.SetMeta(SkinLayout.AddedMeta, key);
		target.AddChild(node);
		return node;
	}

	public override void _Process(double delta)
	{
		var root = Root;
		if (root == null) return;
		var layout = Skin.Element?.Layout;
		if (layout == null) return;

		if (!ReferenceEquals(layout, _layout))
		{
			// Skin switched: reset everything to scene defaults before applying the new layout.
			foreach (var c in SkinLayout.GetEditable(root))
			{
				if (c.HasMeta(SkinLayout.AddedMeta)) c.QueueFree();
				else Restore(c);
			}
			_applied.Clear();
			_layout = layout;
			return; // let the freed nodes leave the tree first
		}

		if (layout.Count == 0 && _applied.Count == 0) return; // nothing customised

		var editable = SkinLayout.GetEditable(root);
		var present = new HashSet<string>();
		foreach (var c in editable)
		{
			if (c.IsQueuedForDeletion()) continue;
			var key = SkinLayout.KeyOf(c);
			present.Add(key);
			if (layout.TryGetValue(key, out var entry))
			{
				Remember(c);
				SkinLayout.Apply(c, entry);
				_applied.Add(c);
			}
			else if (c.HasMeta(SkinLayout.AddedMeta))
			{
				c.QueueFree();
			}
			else if (_applied.Remove(c))
			{
				Restore(c); // entry was reset
			}
		}
		foreach (var (key, entry) in layout)
		{
			if (entry.Scene != null && !present.Contains(key))
				AddComponent(key, entry.Scene);
		}
	}
}
