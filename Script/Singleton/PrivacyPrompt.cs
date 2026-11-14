using Godot;
using System.Linq;

/// <summary>
/// Asked once (setting "privacyprompted"): crash reports, Discord Rich Presence and LAN chat all stay off
/// until the player ticks them here. They can be changed later in Settings.
/// </summary>
public partial class PrivacyPrompt : CanvasLayer
{
	public const string PromptedSetting = "privacyprompted";

	private CheckButton _crash, _discord, _lan;

	public static void ShowIfNeeded(SceneTree tree)
	{
		if (Check.CheckBoolValue(SettingsOperator.GetSetting(PromptedSetting)?.ToString() ?? "false")) return;
		// Not in the updater, headless runs or the self-tests (they'd sit behind the prompt).
		if (DisplayServer.GetName() == "headless") return;
		var args = OS.GetCmdlineArgs().Concat(OS.GetCmdlineUserArgs());
		if (args.Any(a => a == "--update" || a == "--bench" || a.Contains("-test"))) return;
		tree.Root.CallDeferred(Node.MethodName.AddChild, new PrivacyPrompt());
	}

	public override void _Ready()
	{
		Layer = 120;
		ProcessMode = ProcessModeEnum.Always;

		var dim = new ColorRect { Color = new Color(0, 0, 0, 0.6f), MouseFilter = Control.MouseFilterEnum.Stop };
		dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		AddChild(dim);

		var center = new CenterContainer();
		center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		dim.AddChild(center);

		var panel = new PanelContainer { CustomMinimumSize = new Vector2(560, 0) };
		center.AddChild(panel);
		if (panel.GetThemeStylebox("panel") is StyleBoxFlat themed)
		{
			var opaque = (StyleBoxFlat)themed.Duplicate();
			opaque.BgColor = new Color(opaque.BgColor, 1f); // the theme's panel is see-through; the screen behind showed through
			panel.AddThemeStyleboxOverride("panel", opaque);
		}
		var margin = new MarginContainer();
		foreach (var side in new[] { "left", "right", "top", "bottom" }) margin.AddThemeConstantOverride($"margin_{side}", 24);
		panel.AddChild(margin);
		var rows = new VBoxContainer();
		rows.AddThemeConstantOverride("separation", 12);
		margin.AddChild(rows);

		var title = new Label { Text = "Before you play" };
		title.AddThemeFontSizeOverride("font_size", 26);
		rows.AddChild(title);
		rows.AddChild(Text("These are all off unless you turn them on. You can change them any time in Settings."));

		_crash = Option(rows, "Send crash reports",
			"When the game crashes or hits an error, send a report (with basic device info) so it can be fixed. Folder and account names are removed first.");
		_discord = Option(rows, "Discord Rich Presence",
			"Show what you're playing on your Discord profile.");
		_lan = Option(rows, "LAN chat",
			"Let people on the same network (Wi-Fi / LAN) see you while the chat or multiplayer screen is open, and chat with them.");

		var buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
		rows.AddChild(buttons);
		var ok = new Button { Text = "Continue", CustomMinimumSize = new Vector2(140, 40) };
		ok.Pressed += Apply;
		buttons.AddChild(ok);
		ok.CallDeferred(Control.MethodName.GrabFocus);
	}

	private static Label Text(string text) => new()
	{
		Text = text,
		AutowrapMode = TextServer.AutowrapMode.WordSmart,
		CustomMinimumSize = new Vector2(500, 0),
		Modulate = new Color(1, 1, 1, 0.75f),
	};

	private static CheckButton Option(VBoxContainer parent, string name, string description)
	{
		var box = new VBoxContainer();
		box.AddThemeConstantOverride("separation", 2);
		var toggle = new CheckButton { Text = name, ButtonPressed = false };
		box.AddChild(toggle);
		box.AddChild(Text(description));
		parent.AddChild(box);
		return toggle;
	}

	private void Apply()
	{
		CrashReports.SetEnabled(_crash.ButtonPressed);

		bool discord = _discord.ButtonPressed;
		SettingsOperator.SetSetting("discord-rpc", discord);
		if (discord) { ApiOperator.InitDiscord(); }
		else ApiOperator.KillDiscord();

		if (LanChat.Instance != null) LanChat.Instance.SetEnabled(_lan.ButtonPressed);
		else SettingsOperator.SetSetting(LanChat.EnabledSetting, _lan.ButtonPressed);

		SettingsOperator.SetSetting(PromptedSetting, true);
		QueueFree();
	}

	public override void _UnhandledInput(InputEvent e)
	{
		// Modal: keys don't fall through to the screen behind it.
		if (e is InputEventKey) GetViewport().SetInputAsHandled();
	}
}
