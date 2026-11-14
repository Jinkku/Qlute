using Godot;
using System;
using System.IO;
using QluteShader;

/// <summary>
/// Full-screen rect running a beatmap's shader graph (<see cref="ShaderGraph"/>). It reads the screen
/// behind it, so where it sits decides what it affects: right above the storyboard ("background")
/// or above the playfield ("screen"). The Create editor uses one inside its preview too.
/// </summary>
public partial class BeatmapShaderLayer : ColorRect
{
	public const string SettingKey = "beatmapshader";

	public Func<double> TimeSource { get; set; } = () => 0;
	public BeatClock Beats { get; set; } = new();
	public Func<float> Health { get; set; } = () => 1f;
	public Func<float> Combo { get; set; } = () => 0f;

	private ShaderMaterial _material;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		Color = Colors.White;
	}

	/// <summary>Compile and show <paramref name="graph"/>; hides itself when the graph doesn't change anything.</summary>
	public void SetGraph(ShaderGraph graph)
	{
		if (graph == null || !graph.DoesSomething())
		{
			Visible = false;
			return;
		}
		SetCode(graph.Compile());
	}

	public void SetCode(string code)
	{
		var shader = new Shader { Code = code };
		_material ??= new ShaderMaterial();
		_material.Shader = shader;
		Material = _material;
		Visible = true;
	}

	public override void _Process(double delta)
	{
		if (!Visible || _material == null) return;
		double ms = TimeSource();
		var (phase, pulse, kiai) = Beats.At(ms);
		_material.SetShaderParameter("song_time", (float)(ms / 1000.0));
		_material.SetShaderParameter("beat_phase", phase);
		_material.SetShaderParameter("beat_pulse", pulse);
		_material.SetShaderParameter("kiai", kiai);
		_material.SetShaderParameter("health", Health());
		_material.SetShaderParameter("combo", Combo());
	}

	/// <summary>
	/// Adds the selected beatmap's shader to a game mode scene. <paramref name="above"/> is the node it goes
	/// right after for the "background" target (the storyboard, or the background when there's none).
	/// </summary>
	public static BeatmapShaderLayer AttachToGameplay(Control root, CanvasItem above)
	{
		try
		{
			if (!Check.CheckBoolValue(SettingsOperator.GetSetting(SettingKey)?.ToString() ?? "true")) return null;
			var osu = SettingsOperator.SessionConfig.BeatmapURL;
			if (string.IsNullOrEmpty(osu) || !File.Exists(osu)) return null;
			var graph = ShaderGraph.LoadFromFolder(Path.GetDirectoryName(osu));
			if (graph == null || !graph.DoesSomething()) return null;

			var layer = new BeatmapShaderLayer
			{
				TimeSource = () => SettingsOperator.Gameplaycfg.Time * 1000.0,
				Beats = BeatClock.FromOsu(osu),
				Health = () => HealthBar.MaxHealth > 0 ? (float)HealthBar.Health / HealthBar.MaxHealth : 1f,
				Combo = () => SettingsOperator.Gameplaycfg.Combo,
				Name = "BeatmapShader",
			};
			root.AddChild(layer);
			layer.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			if (graph.Target == "screen")
			{
				// above the playfield, under the HUD
				var hud = root.GetNodeOrNull<CanvasItem>("HUD");
				if (hud != null) root.MoveChild(layer, hud.GetIndex());
			}
			else if (above != null && above.GetParent() == root)
				root.MoveChild(layer, above.GetIndex() + 1);
			layer.SetGraph(graph);
			GD.Print($"[BeatmapShader] {graph.Nodes.Count} nodes, target {graph.Target}");
			return layer;
		}
		catch (Exception e)
		{
			GD.PrintErr("[BeatmapShader] couldn't load: " + e);
			return null;
		}
	}
}
