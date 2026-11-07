using Godot;

/// <summary>"Report player" window, same fields as the website's report dialog. Opened from a player's profile.</summary>
public partial class ReportDialog : Control
{
	private readonly string _target;
	private readonly string _context;
	private OptionButton _category;
	private TextEdit _details;
	private LineEdit _extra;
	private Label _status;
	private Button _send;
	private Node _scene;
	private bool _closing;

	public ReportDialog() : this("", "") { }
	public ReportDialog(string target, string context) { _target = target; _context = context ?? ""; }

	public override void _Ready()
	{
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		MouseFilter = MouseFilterEnum.Stop;
		var dim = new ColorRect { Color = new Color(0, 0, 0, 0.6f), MouseFilter = MouseFilterEnum.Stop };
		dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		dim.GuiInput += e => { if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true }) Close(); };
		AddChild(dim);
		var center = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
		center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(center);

		var panel = new PanelContainer { CustomMinimumSize = new Vector2(Mathf.Min(480, GetViewportRect().Size.X - 40), 0) };
		panel.AddThemeStyleboxOverride("panel", PlayerOverlays.Style(PlayerOverlays.Page, 0, 0, 0));
		center.AddChild(panel);
		var outer = new VBoxContainer();
		outer.AddThemeConstantOverride("separation", 0);
		panel.AddChild(outer);
		var bar = PlayerOverlays.TitleBar(outer, "report player");
		var close = PlayerOverlays.FlatButton("✕", "Close (Esc)");
		close.Pressed += Close;
		bar.AddChild(close);

		var box = PlayerOverlays.Panel(PlayerOverlays.Box, 0, 18, 14);
		outer.AddChild(box);
		var v = ModUi.Column(8);
		box.AddChild(v);
		v.AddChild(PlayerOverlays.Text($"Report {_target}", 20, bold: true, trim: true));
		v.AddChild(ModUi.Para("The Qlute team will look at this. False reports may get you restricted.", 12, PlayerOverlays.Dim));

		v.AddChild(PlayerOverlays.Text("What's wrong?", 13));
		_category = ModUi.Options();
		foreach (var (_, text) in ModerationApi.Categories) _category.AddItem(text);
		v.AddChild(_category);

		v.AddChild(PlayerOverlays.Text("What happened?", 13));
		_details = ModUi.TextArea("Be specific: which map or score, what they said, when...", 110);
		v.AddChild(_details);

		v.AddChild(PlayerOverlays.Text("Link or score (optional)", 13));
		_extra = ModUi.Input("e.g. a replay link or beatmap", 500);
		_extra.Text = _context;
		v.AddChild(_extra);

		_status = ModUi.Para("", 13, ModUi.Accent);
		v.AddChild(_status);

		var buttons = ModUi.Row();
		buttons.Alignment = BoxContainer.AlignmentMode.End;
		var cancel = ModUi.Button("Cancel");
		cancel.Pressed += Close;
		buttons.AddChild(cancel);
		_send = ModUi.Button("Send report", ModUi.Red);
		_send.Pressed += Send;
		buttons.AddChild(_send);
		v.AddChild(buttons);

		_scene = GetTree().CurrentScene;
		_scene?.SetProcessInput(false);
		Modulate = new Color(1, 1, 1, 0);
		CreateTween().TweenProperty(this, "modulate:a", 1f, 0.15f);
		_details.CallDeferred(Control.MethodName.GrabFocus);
	}

	public override void _ExitTree()
	{
		if (IsInstanceValid(_scene)) _scene.SetProcessInput(true);
	}

	public override void _Input(InputEvent e)
	{
		if (e.IsActionPressed("ui_cancel")) { Close(); GetViewport().SetInputAsHandled(); }
	}

	private async void Send()
	{
		var details = _details.Text.Trim();
		if (details.Length < 5) { _status.Text = "Please say what happened (at least a few words)."; return; }
		_send.Disabled = true;
		_status.Text = "Sending...";
		var (message, error) = await ModerationApi.SendReport(_target, ModerationApi.Categories[_category.Selected].id,
			ModUi.Clip(details, 2000), _extra.Text.Trim());
		if (!IsInstanceValid(this)) return;
		if (error != null)
		{
			_status.Text = error;
			_send.Disabled = false;
			return;
		}
		Notify.Post(message);
		Close();
	}

	private void Close()
	{
		if (_closing) return;
		_closing = true;
		var t = CreateTween();
		t.TweenProperty(this, "modulate:a", 0f, 0.12f);
		t.TweenCallback(Callable.From(QueueFree));
	}
}
