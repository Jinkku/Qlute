using Godot;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using QluteShader;
using QluteStoryboard;
using CreateEditor;

/// <summary>
/// Dev-only check for storyboards and beatmap shaders. Use an XDG_DATA_HOME sandbox (it writes into the map's folder).
///   -- --visuals-test=/path/map.osu [--visuals-test-shot=DIR] [--visuals-test-at=MS,MS,...]
///      [--visuals-test-graph] (write a shader graph using every node type into the set first)
///      [--visuals-test-editor] (open the map in Create: Storyboard + Shader tabs, add a sprite, save, reload)
/// Always parses every .osu/.osb in the beatmaps folder first. Grep [VISUALSTEST].
/// </summary>
public partial class VisualsSelfTest : Node
{
	private static readonly string[] Args = OS.GetCmdlineUserArgs();
	private static string Value(string prefix) => Args.FirstOrDefault(a => a.StartsWith(prefix))?[prefix.Length..];
	public static bool Enabled => Value("--visuals-test=") != null;
	private static void Log(string s) => GD.Print("[VISUALSTEST] " + s);

	private double _t;
	private int _step;
	private int[] _times = Array.Empty<int>();
	private int _shot;
	private string ShotDir => Value("--visuals-test-shot=");
	private string OsuPath => Value("--visuals-test=");

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
		_times = (Value("--visuals-test-at=") ?? "15000,30000").Split(',').Select(int.Parse).ToArray();
	}

	public override void _Process(double delta)
	{
		_t += delta;
		if (_t > 400) { Log("timeout"); GetTree().Quit(1); return; }
		switch (_step)
		{
			case 0 when _t > 3 && SettingsOperator.Beatmaps.Count > 0:
				_step = 1;
				ParseAll();
				if (Args.Contains("--visuals-test-graph")) WriteGraph();
				if (Args.Contains("--visuals-test-menu")) StartMenu();
				else if (Args.Contains("--visuals-test-editor")) StartEditor();
				else StartPlay();
				break;
			case 10: // playing: capture at each time
				double now = SettingsOperator.Gameplaycfg.Time * 1000;
				if (Value("--visuals-test-bench=") is { } benchSecs && GetTree().CurrentScene is Gameplay { songstarted: true } && now >= _times[0])
				{
					Bench(delta, double.Parse(benchSecs));
					break;
				}
				if (GetTree().CurrentScene is Gameplay { songstarted: true } && _shot < _times.Length && now >= _times[_shot])
				{
					var sb = GetTree().CurrentScene?.FindChildren("*", "", true, false).OfType<StoryboardPlayer>().FirstOrDefault();
					var sh = GetTree().CurrentScene?.FindChildren("*", "", true, false).OfType<BeatmapShaderLayer>().FirstOrDefault();
					Log($"t={now:0} score={Gameplay.score} scene={GetTree().CurrentScene?.Name} storyboard={(sb == null ? "none" : $"ready={sb.Ready} replacesBg={sb.ReplacesBackground}")} shader={(sh == null ? "none" : $"visible={sh.Visible}")}");
					Shot($"play_{_times[_shot]}");
					_shot++;
					if (_shot >= _times.Length) { Log("done"); GetTree().Quit(); }
				}
				break;
			case 20 when _t > _stepAt + 3: EditorSteps(); break;
			case 30 when _t > _stepAt + 6: MenuSteps(); break;
		}
	}

	private double _stepAt;
	private double _benchTime, _cpu, _gpu;
	private int _benchFrames;
	private long _benchStartTicks;
	private readonly System.Collections.Generic.List<double> _frameMs = new();

	/// <summary>Uncapped frame stats for a few seconds of gameplay (run with --uncapped).</summary>
	private void Bench(double delta, double seconds)
	{
		if (_benchFrames == 0)
		{
			_benchStartTicks = StoryboardPlayer.UpdateTicks;
			RenderingServer.ViewportSetMeasureRenderTime(GetViewport().GetViewportRid(), true);
		}
		_benchFrames++;
		_benchTime += delta;
		_frameMs.Add(delta * 1000);
		_cpu += Performance.GetMonitor(Performance.Monitor.TimeProcess) * 1000;
		_gpu += RenderingServer.ViewportGetMeasuredRenderTimeGpu(GetViewport().GetViewportRid());
		if (_benchTime < seconds) return;
		_frameMs.Sort();
		double sbMs = (StoryboardPlayer.UpdateTicks - _benchStartTicks) * 1000.0 / Stopwatch.Frequency / _benchFrames;
		Log(string.Create(System.Globalization.CultureInfo.InvariantCulture,
			$"bench storyboard={SettingsOperator.GetSetting("storyboard")} shader={SettingsOperator.GetSetting("beatmapshader")}: {_benchFrames / _benchTime:0} fps avg, " +
			$"p99 frame {_frameMs[(int)(_frameMs.Count * 0.99)]:0.00} ms, process {_cpu / _benchFrames:0.00} ms, gpu {_gpu / _benchFrames:0.00} ms, storyboard update {sbMs:0.000} ms, " +
			$"active objects {StoryboardPlayer.LastActive}, draw calls {Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame)}, nodes {Performance.GetMonitor(Performance.Monitor.ObjectNodeCount)}"));
		GetTree().Quit();
	}

	private void Shot(string name)
	{
		if (ShotDir == null) return;
		Directory.CreateDirectory(ShotDir);
		var img = GetViewport().GetTexture().GetImage();
		var path = Path.Combine(ShotDir, name + ".png");
		img.SavePng(path);
		Log("shot " + path);
	}

	private static void ParseAll()
	{
		var sw = Stopwatch.StartNew();
		int files = 0, objects = 0, failed = 0, evals = 0;
		foreach (var f in Directory.EnumerateFiles(SettingsOperator.beatmapsdir, "*.*", SearchOption.AllDirectories)
			         .Where(f => f.EndsWith(".osu") || f.EndsWith(".osb")))
		{
			files++;
			try
			{
				var doc = new StoryboardDoc();
				doc.ParseFile(File.ReadAllLines(f), fromOsb: f.EndsWith(".osb"));
				doc.CompileAll();
				objects += doc.Objects.Count;
				foreach (var o in doc.Objects)
					for (double t = o.StartTime - 100; t <= o.EndTime + 100; t += Math.Max(1, (o.EndTime - o.StartTime + 200) / 20))
					{
						var st = o.StateAt(t);
						if (!float.IsFinite(st.X + st.Y + st.Alpha + st.ScaleX + st.ScaleY + st.Rotation)) throw new Exception($"non-finite state in {o.Path} at {t}");
						o.FrameAt(t);
						evals++;
					}
				// round trip: write the difficulty's objects and parse them again
				var lines = doc.ToEventLines();
				var again = new StoryboardDoc();
				again.ParseEvents(lines, false);
				int own = doc.Objects.Count(o => !o.FromOsb);
				if (again.Objects.Count != own) throw new Exception($"round trip {own} -> {again.Objects.Count} objects");
				for (int i = 0; i < own; i++)
					if (again.Objects[i].Commands.Count != doc.Objects[i].Commands.Count) throw new Exception($"round trip commands differ on object {i}");
			}
			catch (Exception e)
			{
				failed++;
				Log($"FAIL {f}: {e.Message}");
			}
		}
		Log($"parsed {files} files, {objects} objects, {evals} evaluations, {failed} failed, {sw.ElapsedMilliseconds} ms");
	}

	private void WriteGraph()
	{
		var folder = Path.GetDirectoryName(OsuPath);
		var g = new ShaderGraph { Target = "screen" };
		int id = 1;
		// chain every UV node, then every colour node, into the output
		ShaderGraphNode Add(string type) { var n = new ShaderGraphNode { Id = id++, Type = type, X = (id % 8) * 220, Y = (id / 8) * 170 }; g.Nodes.Add(n); return n; }
		var uv = Add("uv");
		int lastUv = uv.Id;
		foreach (var t in new[] { "wave", "zoom", "rotate", "pixelate", "shake", "mirror" })
		{
			var n = Add(t);
			g.Links.Add(new ShaderGraphLink { From = lastUv, FromPort = 0, To = n.Id, ToPort = 0 });
			lastUv = n.Id;
		}
		var screen = Add("screen");
		g.Links.Add(new ShaderGraphLink { From = lastUv, FromPort = 0, To = screen.Id, ToPort = 0 });
		int lastCol = screen.Id;
		foreach (var t in new[] { "grayscale", "invert", "hue", "adjust", "tint", "vignette", "flash" })
		{
			var n = Add(t);
			n.Values["in1"] = t is "grayscale" or "invert" ? 0.2f : n.Get("in1", 0.1f);
			g.Links.Add(new ShaderGraphLink { From = lastCol, FromPort = 0, To = n.Id, ToPort = 0 });
			lastCol = n.Id;
		}
		// math on the beat: mix the chain with a chromatic split by the pulse
		var beat = Add("beat");
		var chroma = Add("chroma");
		var blur = Add("blur");
		var mix = Add("mix");
		var colour = Add("color");
		var mul = Add("mul");
		var sin = Add("sin");
		var time = Add("time");
		foreach (var t in new[] { "add", "sub", "div", "cos", "abs", "fract", "oneminus", "clamp", "smoothstep", "pow", "distance", "float", "vec2", "split", "player" })
			Add(t); // unconnected nodes must compile too
		g.Links.Add(new ShaderGraphLink { From = time.Id, FromPort = 0, To = sin.Id, ToPort = 0 });
		g.Links.Add(new ShaderGraphLink { From = lastCol, FromPort = 0, To = mix.Id, ToPort = 0 });
		g.Links.Add(new ShaderGraphLink { From = chroma.Id, FromPort = 0, To = mix.Id, ToPort = 1 });
		g.Links.Add(new ShaderGraphLink { From = beat.Id, FromPort = 1, To = mix.Id, ToPort = 2 });
		g.Links.Add(new ShaderGraphLink { From = mix.Id, FromPort = 0, To = mul.Id, ToPort = 0 });
		g.Links.Add(new ShaderGraphLink { From = colour.Id, FromPort = 0, To = mul.Id, ToPort = 1 });
		var output = Add("output");
		g.Links.Add(new ShaderGraphLink { From = mul.Id, FromPort = 0, To = output.Id, ToPort = 0 });
		g.Links.Add(new ShaderGraphLink { From = blur.Id, FromPort = 0, To = output.Id, ToPort = 1 }); // colour -> float mix
		File.WriteAllText(Path.Combine(folder!, ShaderGraph.FileName), g.ToJson());
		var back = ShaderGraph.FromJson(g.ToJson());
		Log($"graph written: {g.Nodes.Count} nodes, {g.Links.Count} links, reload {back.Nodes.Count}/{back.Links.Count}, code {back.Compile().Length} chars");
		// the shader must compile: Godot prints the errors and leaves the code's uniform list empty when it doesn't
		var shader = new Shader { Code = back.Compile() };
		var uniforms = RenderingServer.GetShaderParameterList(shader.GetRid());
		Log($"shader uniforms after compile: {uniforms.Count} (expect 6)");
	}

	private int FindMap() => SettingsOperator.Beatmaps.FindIndex(b => b.Rawurl != null && Path.GetFullPath(b.Rawurl) == Path.GetFullPath(OsuPath));

	private void StartPlay()
	{
		int index = FindMap();
		if (index < 0) { Log("map not found " + OsuPath); GetTree().Quit(1); return; }
		ModsOperator.Mods["auto"] = true;
		ModsOperator.Mods["no-fail"] = true;
		SettingsOperator.Marathon = false;
		GetNode<SettingsOperator>("/root/SettingsOperator").SelectSongID(index);
		SettingsOperator.SessionConfig.GameMode = 0;
		Log("playing " + SettingsOperator.Beatmaps[index].Title);
		GetNode<SceneTransition>("/root/Transition").Switch("res://Panels/Screens/SongLoadingScreen.tscn");
		_step = 10;
	}

	private int _menuPhase;

	/// <summary>Song select, then the home screen, with the map selected (its storyboard behind the menus).</summary>
	private void StartMenu()
	{
		int index = FindMap();
		if (index < 0) { Log("map not found " + OsuPath); GetTree().Quit(1); return; }
		GetNode<SettingsOperator>("/root/SettingsOperator").SelectSongID(index);
		GetNode<SceneTransition>("/root/Transition").Switch("res://Panels/Screens/song_select.tscn");
		_step = 30;
		_stepAt = _t;
	}

	private void MenuSteps()
	{
		_stepAt = _t;
		var menu = GetTree().CurrentScene?.FindChildren("*", "", true, false).OfType<MenuStoryboard>().FirstOrDefault();
		var player = menu?.FindChildren("*", "", true, false).OfType<StoryboardPlayer>().FirstOrDefault();
		Log($"menu {GetTree().CurrentScene?.Name}: storyboard={(player == null ? "none" : $"ready={player.Ready} objects={player.Doc?.Objects.Count} active={player.ActiveCount}")} music={AudioPlayer.Instance.GetPlaybackPosition():0.0}s playing={AudioPlayer.Instance.Playing}");
		switch (_menuPhase++)
		{
			case 0:
				Shot("menu_song_select");
				GetNode<SceneTransition>("/root/Transition").Switch("res://Panels/Screens/home_screen.tscn");
				break;
			default:
				Shot("menu_home");
				Log("done");
				GetTree().Quit();
				break;
		}
	}

	private void StartEditor()
	{
		EditorSongInfo.FilePath = OsuPath;
		GetNode<SceneTransition>("/root/Transition").Switch("res://Panels/Screens/Create.tscn");
		_step = 20;
		_stepAt = _t;
	}

	private int _editorPhase;

	private void EditorSteps()
	{
		var create = GetTree().CurrentScene as Create;
		if (create == null) { Log("editor not open: " + GetTree().CurrentScene?.Name); GetTree().Quit(1); return; }
		void Call(string method, params object[] args) =>
			typeof(Create).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)!.Invoke(create, args);
		_stepAt = _t;
		switch (_editorPhase++)
		{
			case 0:
				Log($"editor storyboard: {create.Storyboard.Objects.Count} objects ({create.Storyboard.Objects.Count(o => o.FromOsb)} from .osb), shader nodes {create.ShaderGraph.Nodes.Count}");
				create.Seek(_times[0]);
				Call("Menu", 4, true);
				break;
			case 1:
				Shot("editor_storyboard");
				// add a sprite from the background image, then save and reload
				var bg = create.Map.BackgroundFilename;
				var sbEditor = create.FindChildren("*", "", true, false).OfType<CreateEditor.StoryboardEditor>().First();
				int before = create.Storyboard.Objects.Count(o => !o.FromOsb);
				sbEditor.ImportImage(Path.Combine(create.SetFolderPath, bg));
				Log($"added sprite: own objects {before} -> {create.Storyboard.Objects.Count(o => !o.FromOsb)}");
				Call("Save", false);
				var reloaded = StoryboardDoc.LoadForBeatmap(OsuPath);
				Log($"after save: file has {reloaded.Objects.Count(o => !o.FromOsb)} own objects, {reloaded.Objects.Count(o => o.FromOsb)} .osb objects");
				break;
			case 2:
				Shot("editor_storyboard_added");
				Call("Menu", 5, true);
				break;
			case 3:
			{
				Shot("editor_shader");
				var sh = create.FindChildren("*", "", true, false).OfType<CreateEditor.ShaderEditor>().First();
				object Inv(string m, params object[] a) => typeof(CreateEditor.ShaderEditor).GetMethod(m, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(sh, a);
				int n0 = create.ShaderGraph.Nodes.Count, l0 = create.ShaderGraph.Links.Count;
				Inv("AddNode", ShaderGraph.Defs["invert"], new Vector2(100, 100));
				var added = create.ShaderGraph.Nodes.Last();
				var output = create.ShaderGraph.Nodes.First(n => n.Type == "output");
				Inv("OnConnect", (StringName)("n" + added.Id), 0L, (StringName)("n" + output.Id), 0L);
				var colourLink = create.ShaderGraph.Links.Last(l => l.To == output.Id && l.ToPort == 0);
				Log($"shader edit: nodes {n0}->{create.ShaderGraph.Nodes.Count}, links {l0}->{create.ShaderGraph.Links.Count}, output colour from new node={colourLink.From == added.Id}");
				// a loop must be refused: output of the new node back into its own input chain
				Inv("OnConnect", (StringName)("n" + added.Id), 0L, (StringName)("n" + added.Id), 0L);
				Call("Undo");
				Call("Undo");
				Log($"after 2 undos: nodes {create.ShaderGraph.Nodes.Count} (want {n0}), links {create.ShaderGraph.Links.Count} (want {l0})");
				create.Seek(30500);
				Call("Menu", 4, true);
				break;
			}
			case 4:
			{
				var sbe = create.FindChildren("*", "", true, false).OfType<CreateEditor.StoryboardEditor>().First();
				var own = create.Storyboard.Objects.First(o => !o.FromOsb);
				var corners = sbe._player.CornersOf(own);
				if (corners == null) { Log("drag: sprite not drawn at 30500"); GetTree().Quit(1); return; }
				var centre = (corners[0] + corners[2]) / 2;
				float x0 = own.X;
				sbe.PreviewPressed(centre);
				sbe.PreviewDragged(centre + new Vector2(60, 0));
				sbe.PreviewDragged(centre + new Vector2(120, 0));
				sbe.PreviewReleased();
				float x1 = own.X;
				Shot("editor_storyboard_dragged");
				Call("Undo");
				var after = create.Storyboard.Objects.First(o => !o.FromOsb);
				Log($"drag: x {x0} -> {x1} (scale {sbe._player.PixelScale:0.00}), after undo {after.X}");
				Call("Menu", 1, true);
				break;
			}
			case 5:
			{
				// note tools: live keys, mirror, hitsounds, hold drawn with the mouse, undo
				var map = create.Map;
				double beat = map.TimingAt(10000)?.BeatLength ?? 500;
				// first snapped time from `from` where `lane` is free for 4 beats
				int Free(int lane, int from)
				{
					for (int t = create.SnapTime(from); t < 600000; t = create.SnapTime(t + beat))
						if (!map.Notes.Any(n => n.Lane == lane && n.Time >= t - beat && Math.Max(n.Time, n.EndTime) <= t + beat * 4 + 1 || n.Lane == lane && n.Time < t && n.EndTime >= t))
							return t;
					return -1;
				}
				int liveAt = Free(0, 10000), holdAt = Free(1, 20000);
				create.Seek(liveAt);
				int count0 = map.Notes.Count;
				Call("LiveKeyDown", 0);
				create.Seek(liveAt + beat * 2);
				Call("LiveKeyUp", 0);
				var live = map.Notes.First(n => n.Lane == 0 && n.Time == liveAt);
				Log($"live key: notes {count0}->{map.Notes.Count}, hold={live.IsHold} {live.Time}-{live.EndTime} (beat {beat:0.#})");
				create.Selection.Clear();
				create.Selection.Add(live);
				Call("MirrorSelection");
				Call("ToggleHitsound", 2);
				Log($"mirror+whistle: lane {live.Lane} hitsound {live.HitSound}");

				var field = create.FindChildren("*", "", true, false).OfType<CreateEditor.ManiaEditorField>().First();
				float P(string name) => (float)typeof(CreateEditor.ManiaEditorField).GetProperty(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(field)!;
				Call("SetTool", CreateEditor.EditorTool.Hold);
				create.Seek(holdAt);
				float x = P("FieldLeft") + 1.5f * P("LaneWidth"), y = P("JudgeY"), up = (float)(beat * 3 * P("PxPerMs"));
				field._GuiInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = new Vector2(x, y) });
				field._GuiInput(new InputEventMouseMotion { Position = new Vector2(x, y - up), ButtonMask = MouseButtonMask.Left });
				field._GuiInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = new Vector2(x, y - up) });
				var drawn = map.Notes.FirstOrDefault(n => n.Lane == 1 && n.Time == holdAt);
				Log($"hold tool: {(drawn == null ? "no note" : $"{drawn.Time}-{drawn.EndTime} hold={drawn.IsHold}")}");
				Shot("editor_compose");
				for (int i = 0; i < 4; i++) Call("Undo");
				Log($"after 4 undos: notes {map.Notes.Count} (want {count0})");
				Log("done");
				GetTree().Quit();
				break;
			}
		}
	}
}
