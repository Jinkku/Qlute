using Godot;

/// <summary>
/// Top bar of the LAN lobby: room name (with inline rename for the host), host / player / phase pills,
/// the "time till play" countdown, host timer controls, and the Ready / Leave buttons.
///
/// This component only displays state and raises signals; <see cref="MultiplayerLobby"/> decides what they do.
/// Layout and styling live in Panels/Multiplayer/Lobby/LobbyHeader.tscn.
/// </summary>
public partial class LobbyHeader : PanelContainer
{
	[Signal] public delegate void ReadyToggledEventHandler(bool ready);
	[Signal] public delegate void LeavePressedEventHandler();
	[Signal] public delegate void RenameSubmittedEventHandler(string newName);
	[Signal] public delegate void TimerAdjustedEventHandler(int deltaSeconds);
	[Signal] public delegate void StartNowPressedEventHandler();

	[ExportGroup("Behaviour")]
	/// <summary>Seconds added/removed by the host's -/+ timer buttons.</summary>
	[Export] public int TimerStep { get; set; } = 15;

	[ExportGroup("Colours")]
	[Export] public Color TimerColor { get; set; } = Colors.White;
	/// <summary>Countdown colour once the match is about to start.</summary>
	[Export] public Color TimerSoonColor { get; set; } = new("#5be37d");

	[ExportGroup("Room info")]
	[Export] public Label RoomTitle { get; set; }
	[Export] public LineEdit RenameEdit { get; set; }
	[Export] public Button RenameButton { get; set; }
	[Export] public Label HostPill { get; set; }
	[Export] public Label PlayersPill { get; set; }
	[Export] public Label PhasePill { get; set; }

	[ExportGroup("Countdown")]
	[Export] public Label TimerCaption { get; set; }
	[Export] public Label TimerLabel { get; set; }
	[Export] public ProgressBar TimerBar { get; set; }

	[ExportGroup("Host controls")]
	[Export] public Control HostControls { get; set; }
	[Export] public Button TimerMinusButton { get; set; }
	[Export] public Label TimerValue { get; set; }
	[Export] public Button TimerPlusButton { get; set; }
	[Export] public Button StartNowButton { get; set; }

	[ExportGroup("Actions")]
	[Export] public Button ReadyButton { get; set; }
	[Export] public Button LeaveButton { get; set; }

	private bool _renaming;
	private string _lastCountdown;

	public override void _Ready()
	{
		RenameEdit.MaxLength = LanNetwork.MaxRoomNameLength;
		TimerMinusButton.Text = $"-{TimerStep}s";
		TimerPlusButton.Text = $"+{TimerStep}s";
		SetRenaming(false);
	}

	/// <summary>Updates everything that only changes when the room state changes.</summary>
	public void Refresh(LanRoomState s, LanNetwork net)
	{
		var me = net.LocalPlayer;
		bool lobby = s.Phase == LanPhase.Lobby;

		RoomTitle.Text = s.Name;
		HostPill.Text = $"HOST  {s.HostName}";
		PlayersPill.Text = $"{s.Players.Count} PLAYER{(s.Players.Count == 1 ? "" : "S")}";
		PhasePill.Text = lobby ? "IN LOBBY" : "MATCH IN PROGRESS";

		if (!net.IsHost && _renaming) SetRenaming(false); // lost host while typing
		RenameButton.Visible = net.IsHost && !_renaming;

		HostControls.Visible = net.IsHost;
		TimerValue.Text = LanUi.FormatTime(s.LobbyTimer);
		TimerMinusButton.Disabled = s.LobbyTimer <= LanNetwork.MinLobbyTimer;
		TimerPlusButton.Disabled = s.LobbyTimer >= LanNetwork.MaxLobbyTimer;
		StartNowButton.Disabled = !lobby || !s.CountdownActive;

		ReadyButton.Disabled = me == null || !lobby || !me.HasMap;
		ReadyButton.SetPressedNoSignal(me?.Ready ?? false);
		ReadyButton.Text = !lobby ? "In progress"
			: me != null && !me.HasMap ? "Need beatmap"
			: me?.Ready == true ? "Ready!" : "Ready up";

		LeaveButton.Text = net.IsServer ? "Close room" : "Leave";
		LeaveButton.TooltipText = net.IsServer
			? "The room runs on your game, so leaving closes it for everyone."
			: "Go back to the room list.";
	}

	/// <summary>Called every frame: the countdown ticks locally between state updates.</summary>
	public void UpdateCountdown(LanRoomState s, double timeLeft)
	{
		string caption, time;
		float fraction;
		if (s.Phase == LanPhase.Playing) { caption = "MATCH IN PROGRESS"; time = "LIVE"; fraction = 1; }
		else if (s.CountdownActive) { caption = "TIME TILL PLAY"; time = LanUi.FormatTime(timeLeft); fraction = (float)(timeLeft / Mathf.Max(1, s.LobbyTimer)); }
		else { caption = "WAITING FOR A BEATMAP"; time = "-:--"; fraction = 0; }

		TimerBar.Value = Mathf.Clamp(fraction, 0, 1) * 100;
		if (caption + time == _lastCountdown) return;
		_lastCountdown = caption + time;

		TimerCaption.Text = caption;
		TimerLabel.Text = time;
		bool soon = s.CountdownActive && timeLeft <= LanNetwork.AllReadyCountdown;
		TimerLabel.AddThemeColorOverride("font_color", soon ? TimerSoonColor : TimerColor);
	}

	// ---- Rename ----

	private void SetRenaming(bool on)
	{
		_renaming = on;
		RoomTitle.Visible = !on;
		RenameEdit.Visible = on;
		RenameButton.Visible = !on;
		if (on)
		{
			RenameEdit.Text = RoomTitle.Text;
			RenameEdit.GrabFocus();
			RenameEdit.SelectAll();
		}
	}

	private void OnRenamePressed() => SetRenaming(true);

	private void OnRenameTextSubmitted(string text)
	{
		SetRenaming(false);
		text = text.Trim();
		if (text.Length > 0 && text != RoomTitle.Text) EmitSignal(SignalName.RenameSubmitted, text);
	}

	private void OnRenameFocusExited()
	{
		if (_renaming) OnRenameTextSubmitted(RenameEdit.Text);
	}

	private void OnRenameGuiInput(InputEvent e)
	{
		if (!_renaming || !e.IsActionPressed("ui_cancel")) return;
		RenameEdit.Text = RoomTitle.Text; // Esc: keep the old name
		RenameEdit.ReleaseFocus();
		AcceptEvent();
	}

	// ---- Buttons (connected in the .tscn) ----

	private void OnReadyToggled(bool pressed) => EmitSignal(SignalName.ReadyToggled, pressed);
	private void OnLeavePressed() => EmitSignal(SignalName.LeavePressed);
	private void OnTimerMinusPressed() => EmitSignal(SignalName.TimerAdjusted, -TimerStep);
	private void OnTimerPlusPressed() => EmitSignal(SignalName.TimerAdjusted, TimerStep);
	private void OnStartNowPressed() => EmitSignal(SignalName.StartNowPressed);
}
