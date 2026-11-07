using Godot;

/// <summary>A simple pop-up with a title, text and an OK button (account restricted / restriction lifted).</summary>
public partial class MessageDialog : Control
{
	private readonly string _title, _text;
	private readonly Color _accent;
	private bool _closing;

	public MessageDialog() : this("", "", Colors.White) { }
	public MessageDialog(string title, string text, Color accent) { _title = title; _text = text; _accent = accent; }

	public override void _Ready()
	{
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		MouseFilter = MouseFilterEnum.Stop;
		var dim = new ColorRect { Color = new Color(0, 0, 0, 0.6f), MouseFilter = MouseFilterEnum.Stop };
		dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(dim);
		var center = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
		center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(center);

		var panel = new PanelContainer { CustomMinimumSize = new Vector2(Mathf.Min(460, GetViewportRect().Size.X - 40), 0) };
		panel.AddThemeStyleboxOverride("panel", PlayerOverlays.Style(PlayerOverlays.Page, 0, 0, 0));
		center.AddChild(panel);
		var outer = new VBoxContainer();
		outer.AddThemeConstantOverride("separation", 0);
		panel.AddChild(outer);
		outer.AddChild(new ColorRect { Color = _accent, CustomMinimumSize = new Vector2(0, 4) });

		var box = PlayerOverlays.Panel(PlayerOverlays.Box, 0, 20, 16);
		outer.AddChild(box);
		var v = ModUi.Column(10);
		box.AddChild(v);
		v.AddChild(PlayerOverlays.Text(_title, 20, bold: true, wrap: true));
		v.AddChild(PlayerOverlays.Text(_text, 14, wrap: true));
		var ok = PlayerOverlays.FlatButton("OK");
		ok.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
		ok.FocusMode = FocusModeEnum.All;
		ok.Pressed += Close;
		v.AddChild(ok);
		ok.CallDeferred(Control.MethodName.GrabFocus);

		Modulate = new Color(1, 1, 1, 0);
		CreateTween().TweenProperty(this, "modulate:a", 1f, 0.15f);
	}

	public override void _Input(InputEvent e)
	{
		if (e.IsActionPressed("ui_cancel") || e.IsActionPressed("ui_accept")) { Close(); GetViewport().SetInputAsHandled(); }
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
