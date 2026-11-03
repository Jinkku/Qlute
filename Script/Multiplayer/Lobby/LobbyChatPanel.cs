using Godot;
using System.Collections.Generic;

/// <summary>Right column of the lobby: the room chat log and the message box.</summary>
public partial class LobbyChatPanel : PanelContainer
{
	[Signal] public delegate void MessageSubmittedEventHandler(string text);

	[ExportGroup("Nodes")]
	[Export] public RichTextLabel Log { get; set; }
	[Export] public LineEdit Input { get; set; }

	/// <summary>Replaces the log with these BBCode lines (LanNetwork.ChatLog).</summary>
	public void SetLines(IEnumerable<string> lines)
	{
		Log.Clear();
		Log.AppendText(string.Join("\n", lines));
	}

	public void SetPlayerName(string name) => Input.PlaceholderText = $"Chatting as {name}";

	private void OnInputSubmitted(string text)
	{
		Input.Clear();
		if (!string.IsNullOrWhiteSpace(text)) EmitSignal(SignalName.MessageSubmitted, text);
	}
}
