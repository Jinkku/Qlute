using Godot;

/// <summary>Settings > LAN &amp; Online: LAN chat privacy toggle, online chat toggle and the offline name.</summary>
public partial class LanSettings : PanelContainer
{
	private CheckButton _enabled, _online;
	private LineEdit _nickname;
	private Label _hint;

	public override void _Ready()
	{
		_enabled = GetNode<CheckButton>("Rows/ChatEnabled");
		_nickname = GetNode<LineEdit>("Rows/Nickname");
		_hint = GetNode<Label>("Rows/NicknameHint");

		_enabled.ButtonPressed = LanChat.Enabled;
		_enabled.Toggled += on => LanChat.Instance?.SetEnabled(on);
		_online = GetNode<CheckButton>("Rows/OnlineChat");
		_online.ButtonPressed = OnlineChat.Enabled;
		_online.Toggled += on => OnlineChat.Instance?.SetEnabled(on);

		_nickname.Text = LanChat.Nickname;
		_nickname.TextSubmitted += Save;
		_nickname.FocusExited += () => Save(_nickname.Text);
		Refresh();
	}

	public override void _Notification(int what)
	{
		if (what == NotificationVisibilityChanged && _enabled != null && IsVisibleInTree()) Refresh();
	}

	private void Refresh()
	{
		_enabled.SetPressedNoSignal(LanChat.Enabled);
		_online.SetPressedNoSignal(OnlineChat.Enabled);
		_nickname.Editable = true;
		_nickname.TooltipText = "Leave empty to use your account name (or Guest).";
	}

	private void Save(string text)
	{
		if (LanChat.Clean(text, LanChat.MaxNameLength, "") == LanChat.Nickname) return;
		LanChat.Instance?.SetNickname(text);
		_nickname.Text = LanChat.Nickname;
	}
}
