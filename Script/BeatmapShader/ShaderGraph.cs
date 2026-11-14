using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace QluteShader;

/// <summary>Value types flowing through the graph: float, vec2 (UV) and vec4 (colour).</summary>
public enum PortKind { Float = 0, Vec2 = 1, Color = 2, Any = 3 }

public class PortDef
{
	public string Name;
	public PortKind Kind;
	/// <summary>Default for a Float input (editable on the node when nothing is connected).</summary>
	public float Default;
	/// <summary>GLSL used when nothing is connected (overrides <see cref="Default"/>), e.g. "uv".</summary>
	public string DefaultExpr;
	public bool Editable => DefaultExpr == null && Kind is PortKind.Float or PortKind.Any;

	public PortDef(string name, PortKind kind, float def = 0, string expr = null)
	{
		Name = name; Kind = kind; Default = def; DefaultExpr = expr;
	}
}

/// <summary>
/// One kind of node. <see cref="Gen"/> gets the input expressions (already converted to the input's
/// kind, or to the resolved kind for Any inputs) and returns one GLSL expression per output.
/// Outputs of kind Any take the widest kind of the node's Any inputs.
/// </summary>
public class NodeDef
{
	public string Type, Title, Category, Help;
	public PortDef[] Inputs = Array.Empty<PortDef>();
	public (string name, PortKind kind)[] Outputs = Array.Empty<(string, PortKind)>();
	public Func<string[], ShaderGraphNode, string[]> Gen;
	/// <summary>Extra values edited on the node (constants): name -> default.</summary>
	public (string name, float def)[] Fields = Array.Empty<(string, float)>();
	public bool IsColorConstant;
}

public class ShaderGraphNode
{
	public int Id;
	public string Type = "";
	public float X, Y;
	/// <summary>Inline values: "in0".."inN" for unconnected float inputs, plus constant fields.</summary>
	public Dictionary<string, float> Values = new();

	public float Get(string key, float fallback) =>
		Values.TryGetValue(key, out var v) && float.IsFinite(v) ? v : fallback;
}

public class ShaderGraphLink
{
	public int From, FromPort, To, ToPort;
}

/// <summary>
/// A beatmap shader made in the Create editor's node graph. Saved as JSON next to the map
/// (<see cref="FileName"/>) and compiled to a canvas_item shader when played. Only known node types and
/// numbers are read from the file, so a downloaded map can't inject shader code.
/// </summary>
public class ShaderGraph
{
	public const string FileName = "qlute-shader.json";
	public const int MaxNodes = 200;
	private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

	public int Version = 1;
	/// <summary>"background" = applies to the background + storyboard only; "screen" = also the playfield.</summary>
	public string Target = "background";
	public List<ShaderGraphNode> Nodes = new();
	public List<ShaderGraphLink> Links = new();

	public static readonly string[] Targets = { "background", "screen" };

	// =====================================================================
	// Node catalogue
	// =====================================================================

	private const string Screen = "texture(screen_tex, uv)";
	public static readonly Dictionary<string, NodeDef> Defs = BuildDefs().ToDictionary(d => d.Type);

	private static IEnumerable<NodeDef> BuildDefs()
	{
		PortDef Fl(string n, float d) => new(n, PortKind.Float, d);
		PortDef Any(string n, float d) => new(n, PortKind.Any, d);
		PortDef Uv(string n = "UV") => new(n, PortKind.Vec2, 0, "uv");
		PortDef Col(string n = "Colour") => new(n, PortKind.Color, 0, Screen);

		// ---------- output ----------
		yield return new NodeDef
		{
			Type = "output", Title = "Output", Category = "Output",
			Help = "What the screen shows. Mix 0 = untouched, 1 = fully this colour.",
			Inputs = new[] { Col(), Fl("Mix", 1) },
			Gen = (i, _) => Array.Empty<string>(),
		};

		// ---------- inputs ----------
		yield return new NodeDef
		{
			Type = "screen", Title = "Screen", Category = "Input", Help = "The picture under the shader at a UV.",
			Inputs = new[] { Uv() }, Outputs = new[] { ("Colour", PortKind.Color) },
			Gen = (i, _) => new[] { $"texture(screen_tex, {i[0]})" },
		};
		yield return new NodeDef
		{
			Type = "uv", Title = "UV", Category = "Input", Help = "Screen position, 0-1 from the top left.",
			Outputs = new[] { ("UV", PortKind.Vec2), ("X", PortKind.Float), ("Y", PortKind.Float) },
			Gen = (i, _) => new[] { "uv", "uv.x", "uv.y" },
		};
		yield return new NodeDef
		{
			Type = "time", Title = "Song time", Category = "Input", Help = "Song position in seconds.",
			Outputs = new[] { ("Seconds", PortKind.Float) },
			Gen = (i, _) => new[] { "song_time" },
		};
		yield return new NodeDef
		{
			Type = "beat", Title = "Beat", Category = "Input",
			Help = "Phase goes 0->1 every beat, Pulse is 1 on the beat and fades out, Kiai is 1 in kiai time.",
			Outputs = new[] { ("Phase", PortKind.Float), ("Pulse", PortKind.Float), ("Kiai", PortKind.Float) },
			Gen = (i, _) => new[] { "beat_phase", "beat_pulse", "kiai" },
		};
		yield return new NodeDef
		{
			Type = "player", Title = "Player", Category = "Input", Help = "Health 0-1 and the current combo.",
			Outputs = new[] { ("Health", PortKind.Float), ("Combo", PortKind.Float) },
			Gen = (i, _) => new[] { "health", "combo" },
		};
		yield return new NodeDef
		{
			Type = "float", Title = "Number", Category = "Input",
			Fields = new[] { ("value", 1f) }, Outputs = new[] { ("Value", PortKind.Float) },
			Gen = (i, n) => new[] { Num(n.Get("value", 1)) },
		};
		yield return new NodeDef
		{
			Type = "color", Title = "Colour", Category = "Input", IsColorConstant = true,
			Fields = new[] { ("r", 1f), ("g", 0.4f), ("b", 0.8f), ("a", 1f) }, Outputs = new[] { ("Colour", PortKind.Color) },
			Gen = (i, n) => new[] { $"vec4({Num(n.Get("r", 1))}, {Num(n.Get("g", 0.4f))}, {Num(n.Get("b", 0.8f))}, {Num(n.Get("a", 1))})" },
		};
		yield return new NodeDef
		{
			Type = "vec2", Title = "Make UV", Category = "Input",
			Inputs = new[] { Fl("X", 0.5f), Fl("Y", 0.5f) }, Outputs = new[] { ("UV", PortKind.Vec2) },
			Gen = (i, _) => new[] { $"vec2({i[0]}, {i[1]})" },
		};
		yield return new NodeDef
		{
			Type = "split", Title = "Split colour", Category = "Input",
			Inputs = new[] { Col() },
			Outputs = new[] { ("R", PortKind.Float), ("G", PortKind.Float), ("B", PortKind.Float), ("A", PortKind.Float) },
			Gen = (i, _) => new[] { $"{i[0]}.r", $"{i[0]}.g", $"{i[0]}.b", $"{i[0]}.a" },
		};

		// ---------- math ----------
		NodeDef Bin(string type, string title, string op, float b) => new()
		{
			Type = type, Title = title, Category = "Math",
			Inputs = new[] { Any("A", 0), Any("B", b) }, Outputs = new[] { ("Result", PortKind.Any) },
			Gen = (i, _) => new[] { $"({i[0]} {op} {i[1]})" },
		};
		NodeDef Fn(string type, string title, string fn) => new()
		{
			Type = type, Title = title, Category = "Math",
			Inputs = new[] { Any("X", 0) }, Outputs = new[] { ("Result", PortKind.Any) },
			Gen = (i, _) => new[] { $"{fn}({i[0]})" },
		};
		yield return Bin("add", "Add", "+", 0);
		yield return Bin("sub", "Subtract", "-", 0);
		yield return Bin("mul", "Multiply", "*", 1);
		yield return new NodeDef
		{
			Type = "div", Title = "Divide", Category = "Math",
			Inputs = new[] { Any("A", 1), Any("B", 1) }, Outputs = new[] { ("Result", PortKind.Any) },
			Gen = (i, _) => new[] { $"({i[0]} / max(abs({i[1]}), {Num(0.0001f)}) * sign({i[1]} + {Num(0.0000001f)}))" },
		};
		yield return Fn("sin", "Sine", "sin");
		yield return Fn("cos", "Cosine", "cos");
		yield return Fn("abs", "Absolute", "abs");
		yield return Fn("fract", "Fraction", "fract");
		yield return new NodeDef
		{
			Type = "oneminus", Title = "One minus", Category = "Math",
			Inputs = new[] { Any("X", 0) }, Outputs = new[] { ("Result", PortKind.Any) },
			Gen = (i, _) => new[] { $"(1.0 - {i[0]})" },
		};
		yield return new NodeDef
		{
			Type = "mix", Title = "Mix", Category = "Math", Help = "Blend A to B by T (0-1).",
			Inputs = new[] { Any("A", 0), Any("B", 1), Fl("T", 0.5f) }, Outputs = new[] { ("Result", PortKind.Any) },
			Gen = (i, _) => new[] { $"mix({i[0]}, {i[1]}, {i[2]})" },
		};
		yield return new NodeDef
		{
			Type = "clamp", Title = "Clamp", Category = "Math",
			Inputs = new[] { Any("X", 0), Fl("Min", 0), Fl("Max", 1) }, Outputs = new[] { ("Result", PortKind.Any) },
			Gen = (i, _) => new[] { $"clamp({i[0]}, {i[1]}, max({i[1]}, {i[2]}))" },
		};
		yield return new NodeDef
		{
			Type = "smoothstep", Title = "Smooth step", Category = "Math",
			Inputs = new[] { Fl("Edge 0", 0), Fl("Edge 1", 1), Any("X", 0.5f) }, Outputs = new[] { ("Result", PortKind.Any) },
			Gen = (i, _) => new[] { $"smoothstep({i[0]}, {i[1]}, {i[2]})" },
		};
		yield return new NodeDef
		{
			Type = "pow", Title = "Power", Category = "Math",
			Inputs = new[] { Any("X", 1), Fl("Exponent", 2) }, Outputs = new[] { ("Result", PortKind.Any) },
			Gen = (i, _) => new[] { $"pow(abs({i[0]}), {i[1]})" },
		};
		yield return new NodeDef
		{
			Type = "distance", Title = "Distance from centre", Category = "Math",
			Inputs = new[] { Uv(), Fl("Centre X", 0.5f), Fl("Centre Y", 0.5f) }, Outputs = new[] { ("Distance", PortKind.Float) },
			Gen = (i, _) => new[] { $"distance({i[0]}, vec2({i[1]}, {i[2]}))" },
		};

		// ---------- UV effects ----------
		yield return new NodeDef
		{
			Type = "wave", Title = "Wave", Category = "Distort", Help = "Wobbles the picture sideways.",
			Inputs = new[] { Uv(), Fl("Amount", 0.01f), Fl("Frequency", 12), Fl("Speed", 3) }, Outputs = new[] { ("UV", PortKind.Vec2) },
			Gen = (i, _) => new[] { $"({i[0]} + vec2(sin({i[0]}.y * {i[2]} + song_time * {i[3]}) * {i[1]}, 0.0))" },
		};
		yield return new NodeDef
		{
			Type = "zoom", Title = "Zoom", Category = "Distort", Help = "1 = normal, 2 = twice as close.",
			Inputs = new[] { Uv(), Fl("Zoom", 1.05f) }, Outputs = new[] { ("UV", PortKind.Vec2) },
			Gen = (i, _) => new[] { $"(({i[0]} - 0.5) / max({i[1]}, 0.01) + 0.5)" },
		};
		yield return new NodeDef
		{
			Type = "rotate", Title = "Rotate", Category = "Distort", Help = "Angle in radians around the centre.",
			Inputs = new[] { Uv(), Fl("Angle", 0.1f) }, Outputs = new[] { ("UV", PortKind.Vec2) },
			Gen = (i, _) => new[] { $"(mat2(vec2(cos({i[1]}), sin({i[1]})), vec2(-sin({i[1]}), cos({i[1]}))) * ({i[0]} - 0.5) + 0.5)" },
		};
		yield return new NodeDef
		{
			Type = "pixelate", Title = "Pixelate", Category = "Distort", Help = "Blocks across the screen.",
			Inputs = new[] { Uv(), Fl("Blocks", 120) }, Outputs = new[] { ("UV", PortKind.Vec2) },
			Gen = (i, _) => new[] { $"((floor({i[0]} * max({i[1]}, 1.0)) + 0.5) / max({i[1]}, 1.0))" },
		};
		yield return new NodeDef
		{
			Type = "shake", Title = "Shake", Category = "Distort",
			Inputs = new[] { Uv(), Fl("Amount", 0.005f), Fl("Speed", 40) }, Outputs = new[] { ("UV", PortKind.Vec2) },
			Gen = (i, _) => new[] { $"({i[0]} + vec2(sin(song_time * {i[2]} * 1.31), cos(song_time * {i[2]} * 1.73)) * {i[1]})" },
		};
		yield return new NodeDef
		{
			Type = "mirror", Title = "Mirror", Category = "Distort", Help = "Mirrors the left half onto the right.",
			Inputs = new[] { Uv() }, Outputs = new[] { ("UV", PortKind.Vec2) },
			Gen = (i, _) => new[] { $"vec2(0.5 - abs({i[0]}.x - 0.5), {i[0]}.y)" },
		};

		// ---------- colour effects ----------
		yield return new NodeDef
		{
			Type = "grayscale", Title = "Grayscale", Category = "Colour",
			Inputs = new[] { Col(), Fl("Amount", 1) }, Outputs = new[] { ("Colour", PortKind.Color) },
			Gen = (i, _) => new[] { $"mix({i[0]}, vec4(vec3(dot({i[0]}.rgb, vec3(0.299, 0.587, 0.114))), {i[0]}.a), {i[1]})" },
		};
		yield return new NodeDef
		{
			Type = "invert", Title = "Invert", Category = "Colour",
			Inputs = new[] { Col(), Fl("Amount", 1) }, Outputs = new[] { ("Colour", PortKind.Color) },
			Gen = (i, _) => new[] { $"mix({i[0]}, vec4(1.0 - {i[0]}.rgb, {i[0]}.a), {i[1]})" },
		};
		yield return new NodeDef
		{
			Type = "hue", Title = "Hue shift", Category = "Colour", Help = "Shift 1 = all the way round.",
			Inputs = new[] { Col(), Fl("Shift", 0.25f) }, Outputs = new[] { ("Colour", PortKind.Color) },
			Gen = (i, _) => new[] { $"vec4(hue_shift({i[0]}.rgb, {i[1]}), {i[0]}.a)" },
		};
		yield return new NodeDef
		{
			Type = "adjust", Title = "Brightness / contrast", Category = "Colour",
			Inputs = new[] { Col(), Fl("Brightness", 0), Fl("Contrast", 1), Fl("Saturation", 1) }, Outputs = new[] { ("Colour", PortKind.Color) },
			Gen = (i, _) => new[] { $"vec4(adjust({i[0]}.rgb, {i[1]}, {i[2]}, {i[3]}), {i[0]}.a)" },
		};
		yield return new NodeDef
		{
			Type = "tint", Title = "Tint", Category = "Colour",
			Inputs = new[] { Col(), new PortDef("Tint", PortKind.Color, 0, "vec4(1.0, 0.4, 0.8, 1.0)"), Fl("Amount", 0.3f) },
			Outputs = new[] { ("Colour", PortKind.Color) },
			Gen = (i, _) => new[] { $"vec4(mix({i[0]}.rgb, {i[0]}.rgb * {i[1]}.rgb, {i[2]}), {i[0]}.a)" },
		};
		yield return new NodeDef
		{
			Type = "vignette", Title = "Vignette", Category = "Colour", Help = "Darkens the edges.",
			Inputs = new[] { Col(), Uv(), Fl("Strength", 0.6f), Fl("Size", 0.45f) }, Outputs = new[] { ("Colour", PortKind.Color) },
			Gen = (i, _) => new[] { $"vec4({i[0]}.rgb * (1.0 - {i[2]} * smoothstep({i[3]}, {i[3]} + 0.4, distance({i[1]}, vec2(0.5)))), {i[0]}.a)" },
		};
		yield return new NodeDef
		{
			Type = "chroma", Title = "Chromatic split", Category = "Colour", Help = "Pulls red and blue apart.",
			Inputs = new[] { Uv(), Fl("Amount", 0.004f) }, Outputs = new[] { ("Colour", PortKind.Color) },
			Gen = (i, _) => new[] { $"vec4(texture(screen_tex, {i[0]} + vec2({i[1]}, 0.0)).r, texture(screen_tex, {i[0]}).g, texture(screen_tex, {i[0]} - vec2({i[1]}, 0.0)).b, 1.0)" },
		};
		yield return new NodeDef
		{
			Type = "blur", Title = "Blur", Category = "Colour",
			Inputs = new[] { Uv(), Fl("Radius", 0.003f) }, Outputs = new[] { ("Colour", PortKind.Color) },
			Gen = (i, _) => new[] { $"blur9({i[0]}, {i[1]})" },
		};
		yield return new NodeDef
		{
			Type = "flash", Title = "Beat flash", Category = "Colour", Help = "Brightens on every beat (stronger in kiai).",
			Inputs = new[] { Col(), Fl("Amount", 0.25f) }, Outputs = new[] { ("Colour", PortKind.Color) },
			Gen = (i, _) => new[] { $"vec4({i[0]}.rgb + beat_pulse * {i[1]} * (0.5 + 0.5 * kiai), {i[0]}.a)" },
		};
	}

	// =====================================================================
	// Compiling
	// =====================================================================

	private const string Header = @"shader_type canvas_item;
// Generated by the Qlute Create editor from a node graph. Edit the graph, not this code.
uniform sampler2D screen_tex : hint_screen_texture, filter_linear;
uniform float song_time = 0.0;
uniform float beat_phase = 0.0;
uniform float beat_pulse = 0.0;
uniform float kiai = 0.0;
uniform float health = 1.0;
uniform float combo = 0.0;

vec3 hue_shift(vec3 c, float shift) {
	const vec3 k = vec3(0.57735);
	float a = shift * 6.2831853;
	float ca = cos(a);
	return c * ca + cross(k, c) * sin(a) + k * dot(k, c) * (1.0 - ca);
}
vec3 adjust(vec3 c, float b, float con, float sat) {
	c = (c - 0.5) * con + 0.5 + b;
	float l = dot(c, vec3(0.299, 0.587, 0.114));
	return mix(vec3(l), c, sat);
}
vec4 blur9(vec2 p, float r) {
	vec4 s = vec4(0.0);
	for (int x = -1; x <= 1; x++)
		for (int y = -1; y <= 1; y++)
			s += texture(screen_tex, p + vec2(float(x), float(y)) * r);
	return s / 9.0;
}
";

	public static string Num(float v)
	{
		if (!float.IsFinite(v)) v = 0;
		v = Math.Clamp(v, -100000f, 100000f);
		var s = v.ToString("0.0#######", Inv);
		return s;
	}

	private static string GlslType(PortKind k) => k switch
	{
		PortKind.Float => "float",
		PortKind.Vec2 => "vec2",
		_ => "vec4",
	};

	/// <summary>Convert an expression between kinds (floats broadcast, colours give luminance / .rg).</summary>
	public static string Convert(string e, PortKind from, PortKind to)
	{
		if (from == to || to == PortKind.Any) return e;
		return (from, to) switch
		{
			(PortKind.Float, PortKind.Vec2) => $"vec2({e})",
			(PortKind.Float, PortKind.Color) => $"vec4(vec3({e}), 1.0)",
			(PortKind.Vec2, PortKind.Float) => $"({e}).x",
			(PortKind.Vec2, PortKind.Color) => $"vec4({e}, 0.0, 1.0)",
			(PortKind.Color, PortKind.Float) => $"dot(({e}).rgb, vec3(0.299, 0.587, 0.114))",
			(PortKind.Color, PortKind.Vec2) => $"({e}).rg",
			_ => e,
		};
	}

	/// <summary>Turn the graph into canvas_item shader code. Unknown/broken parts fall back to defaults.</summary>
	public string Compile()
	{
		var body = new StringBuilder();
		var done = new Dictionary<int, (string[] vars, PortKind[] kinds)>();
		var visiting = new HashSet<int>();
		var byId = Nodes.Where(n => Defs.ContainsKey(n.Type)).GroupBy(n => n.Id).ToDictionary(g => g.Key, g => g.First());

		(string[] vars, PortKind[] kinds) Emit(ShaderGraphNode node)
		{
			if (done.TryGetValue(node.Id, out var ready)) return ready;
			var def = Defs[node.Type];
			visiting.Add(node.Id);

			// Gather inputs
			var raw = new (string expr, PortKind kind)[def.Inputs.Length];
			for (int i = 0; i < def.Inputs.Length; i++)
			{
				var port = def.Inputs[i];
				var link = Links.LastOrDefault(l => l.To == node.Id && l.ToPort == i);
				if (link != null && byId.TryGetValue(link.From, out var src) && !visiting.Contains(src.Id))
				{
					var (vars, kinds) = Emit(src);
					if (link.FromPort >= 0 && link.FromPort < vars.Length)
					{
						raw[i] = (vars[link.FromPort], kinds[link.FromPort]);
						continue;
					}
				}
				raw[i] = port.DefaultExpr != null
					? (port.DefaultExpr, port.Kind == PortKind.Any ? PortKind.Float : port.Kind)
					: (Num(node.Get("in" + i, port.Default)), PortKind.Float);
			}

			// Resolve Any: widest kind among connected Any inputs
			var anyKind = PortKind.Float;
			for (int i = 0; i < def.Inputs.Length; i++)
				if (def.Inputs[i].Kind == PortKind.Any && raw[i].kind > anyKind && raw[i].kind != PortKind.Any)
					anyKind = raw[i].kind;
			var args = new string[def.Inputs.Length];
			for (int i = 0; i < def.Inputs.Length; i++)
			{
				var want = def.Inputs[i].Kind == PortKind.Any ? anyKind : def.Inputs[i].Kind;
				args[i] = Convert(raw[i].expr, raw[i].kind, want);
			}

			var exprs = def.Gen(args, node);
			var outVars = new string[def.Outputs.Length];
			var outKinds = new PortKind[def.Outputs.Length];
			for (int o = 0; o < def.Outputs.Length; o++)
			{
				var kind = def.Outputs[o].kind == PortKind.Any ? anyKind : def.Outputs[o].kind;
				var name = $"n{node.Id}_{o}";
				body.Append($"\t{GlslType(kind)} {name} = {exprs[o]};\n");
				outVars[o] = name;
				outKinds[o] = kind;
			}
			visiting.Remove(node.Id);
			var result = (outVars, outKinds);
			done[node.Id] = result;
			return result;
		}

		string colour = Screen, mix = "1.0";
		var output = Nodes.FirstOrDefault(n => n.Type == "output");
		if (output != null)
		{
			Emit(output);
			// re-read the output's inputs (Emit converted them, but outputs have no vars)
			var def = Defs["output"];
			string InputOf(int port, PortKind want, string fallback)
			{
				var link = Links.LastOrDefault(l => l.To == output.Id && l.ToPort == port);
				if (link != null && done.TryGetValue(link.From, out var src) && link.FromPort >= 0 && link.FromPort < src.vars.Length)
					return Convert(src.vars[link.FromPort], src.kinds[link.FromPort], want);
				return fallback;
			}
			colour = InputOf(0, PortKind.Color, Screen);
			mix = InputOf(1, PortKind.Float, Num(output.Get("in1", def.Inputs[1].Default)));
		}

		var sb = new StringBuilder(Header);
		sb.Append("\nvoid fragment() {\n\tvec2 uv = SCREEN_UV;\n");
		sb.Append(body);
		sb.Append($"\tvec4 base_col = {Screen};\n");
		sb.Append($"\tvec4 graph_col = {colour};\n");
		sb.Append($"\tCOLOR = vec4(mix(base_col.rgb, graph_col.rgb, clamp({mix}, 0.0, 1.0)), 1.0);\n");
		sb.Append("}\n");
		return sb.ToString();
	}

	// =====================================================================
	// Files
	// =====================================================================

	public static ShaderGraph CreateDefault()
	{
		var g = new ShaderGraph();
		g.Nodes.Add(new ShaderGraphNode { Id = 1, Type = "screen", X = 40, Y = 80 });
		g.Nodes.Add(new ShaderGraphNode { Id = 2, Type = "output", X = 420, Y = 80 });
		g.Links.Add(new ShaderGraphLink { From = 1, FromPort = 0, To = 2, ToPort = 0 });
		return g;
	}

	public string ToJson() => JsonConvert.SerializeObject(this, Formatting.Indented);

	/// <summary>Parse and clean a graph file: unknown nodes, bad links and odd numbers are dropped.</summary>
	public static ShaderGraph FromJson(string json)
	{
		var g = JsonConvert.DeserializeObject<ShaderGraph>(json, new JsonSerializerSettings
		{
			MaxDepth = 16,
			TypeNameHandling = TypeNameHandling.None,
		}) ?? new ShaderGraph();
		g.Nodes = (g.Nodes ?? new()).Where(n => n != null && n.Type != null && Defs.ContainsKey(n.Type))
			.GroupBy(n => n.Id).Select(x => x.First()).Take(MaxNodes).ToList();
		foreach (var n in g.Nodes)
		{
			n.Values = (n.Values ?? new()).Where(kv => kv.Key != null && kv.Key.Length < 16 && float.IsFinite(kv.Value))
				.Take(16).ToDictionary(kv => kv.Key, kv => kv.Value);
			if (!float.IsFinite(n.X)) n.X = 0;
			if (!float.IsFinite(n.Y)) n.Y = 0;
		}
		// only one output node
		var outputs = g.Nodes.Where(n => n.Type == "output").ToList();
		foreach (var extra in outputs.Skip(1)) g.Nodes.Remove(extra);
		var ids = g.Nodes.ToDictionary(n => n.Id);
		g.Links = (g.Links ?? new()).Where(l => l != null && ids.ContainsKey(l.From) && ids.ContainsKey(l.To) && l.From != l.To
			&& l.FromPort >= 0 && l.FromPort < Defs[ids[l.From].Type].Outputs.Length
			&& l.ToPort >= 0 && l.ToPort < Defs[ids[l.To].Type].Inputs.Length).Take(MaxNodes * 4).ToList();
		if (!Targets.Contains(g.Target)) g.Target = "background";
		return g;
	}

	/// <summary>The set's shader graph, or null when it has none (or it can't be read).</summary>
	public static ShaderGraph LoadFromFolder(string folder)
	{
		if (string.IsNullOrEmpty(folder)) return null;
		var path = Path.Combine(folder, FileName);
		if (!File.Exists(path)) return null;
		try
		{
			if (new FileInfo(path).Length > 512 * 1024) return null;
			return FromJson(File.ReadAllText(path));
		}
		catch (Exception e)
		{
			Godot.GD.PrintErr("[BeatmapShader] couldn't read " + path + ": " + e.Message);
			return null;
		}
	}

	/// <summary>True when the graph changes the picture at all (an Output fed straight from Screen doesn't).</summary>
	public bool DoesSomething()
	{
		var output = Nodes.FirstOrDefault(n => n.Type == "output");
		if (output == null) return false;
		if (output.Get("in1", 1) <= 0 && !Links.Any(l => l.To == output.Id && l.ToPort == 1)) return false;
		var colourLink = Links.LastOrDefault(l => l.To == output.Id && l.ToPort == 0);
		if (colourLink == null) return Links.Any(l => l.To == output.Id);
		var src = Nodes.FirstOrDefault(n => n.Id == colourLink.From);
		return !(src?.Type == "screen" && !Links.Any(l => l.To == src.Id));
	}
}

/// <summary>Beat / kiai lookup from a map's timing points, for the shader's beat inputs.</summary>
public class BeatClock
{
	private readonly List<(double offset, double beatLength, bool kiai)> _red = new();
	private readonly List<(double offset, bool kiai)> _kiai = new();

	public static BeatClock FromOsu(string path)
	{
		var clock = new BeatClock();
		if (string.IsNullOrEmpty(path) || !File.Exists(path)) return clock;
		bool inTiming = false;
		foreach (var raw in File.ReadLines(path))
		{
			var line = raw.Trim();
			if (line.StartsWith("[")) { inTiming = line == "[TimingPoints]"; continue; }
			if (!inTiming || line.Length == 0) continue;
			var p = line.Split(',');
			if (p.Length < 2) continue;
			if (!double.TryParse(p[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var t)) continue;
			if (!double.TryParse(p[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var bl)) continue;
			bool kiai = p.Length > 7 && int.TryParse(p[7], out var fx) && (fx & 1) == 1;
			bool red = p.Length < 7 || p[6].Trim() == "1";
			clock.Add(t, red && bl > 0 ? bl : -1, kiai);
		}
		clock.Sort();
		return clock;
	}

	/// <summary>Add a timing point; <paramref name="beatLength"/> &lt;= 0 means an inherited (green) line.</summary>
	public void Add(double offset, double beatLength, bool kiai)
	{
		if (beatLength > 0) _red.Add((offset, beatLength, kiai));
		_kiai.Add((offset, kiai));
	}

	public void Sort()
	{
		_red.Sort((a, b) => a.offset.CompareTo(b.offset));
		_kiai.Sort((a, b) => a.offset.CompareTo(b.offset));
	}

	/// <summary>Beat phase (0-1), pulse (1 on the beat, fading) and kiai (0/1) at <paramref name="ms"/>.</summary>
	public (float phase, float pulse, float kiai) At(double ms)
	{
		if (_red.Count == 0) return (0, 0, 0);
		var tp = _red[0];
		foreach (var r in _red) { if (r.offset <= ms) tp = r; else break; }
		double beats = (ms - tp.offset) / tp.beatLength;
		float phase = (float)(beats - Math.Floor(beats));
		float pulse = ms < tp.offset ? 0 : (float)Math.Pow(1 - phase, 4);
		bool kiai = false;
		foreach (var k in _kiai) { if (k.offset <= ms) kiai = k.kiai; else break; }
		return (phase, pulse, kiai ? 1 : 0);
	}
}
