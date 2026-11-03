using Godot;
using System.Linq;

/// <summary>
/// Left column of the lobby: one <see cref="LobbyPlayerRow"/> per player, then the last match's results.
/// Rows are reused between refreshes so the list doesn't flicker while the countdown ticks.
/// </summary>
public partial class LobbyPlayerList : PanelContainer
{
	/// <summary>The host clicked "Make host" on a player.</summary>
	[Signal] public delegate void TransferHostRequestedEventHandler(long playerId);

	[ExportGroup("Scenes")]
	/// <summary>Scene instanced for every player (must have a <see cref="LobbyPlayerRow"/> root).</summary>
	[Export] public PackedScene RowScene { get; set; }

	[ExportGroup("Nodes")]
	[Export] public Label CountLabel { get; set; }
	[Export] public VBoxContainer Rows { get; set; }
	[Export] public Control ResultsSection { get; set; }
	[Export] public VBoxContainer Results { get; set; }

	public void Refresh(LanRoomState s, long localId, bool localIsHost)
	{
		CountLabel.Text = s.Players.Count.ToString();

		// During a match: live score order. Otherwise: host first, then ready players, then by name.
		var ordered = (s.Phase == LanPhase.Playing
			? s.Players.OrderByDescending(p => p.InMatch).ThenByDescending(p => p.Score)
			: s.Players.OrderByDescending(p => p.Id == s.HostId).ThenByDescending(p => p.Ready).ThenBy(p => p.Name)).ToList();

		while (Rows.GetChildCount() < ordered.Count)
		{
			var row = RowScene.Instantiate<LobbyPlayerRow>();
			row.MakeHostPressed += id => EmitSignal(SignalName.TransferHostRequested, id);
			Rows.AddChild(row);
		}
		for (int i = 0; i < Rows.GetChildCount(); i++)
		{
			var row = Rows.GetChild<LobbyPlayerRow>(i);
			row.Visible = i < ordered.Count;
			if (row.Visible) row.Setup(ordered[i], s, ordered[i].Id == localId, localIsHost);
		}

		RefreshResults(s);
	}

	private int _resultsMatchId = -1;

	private void RefreshResults(LanRoomState s)
	{
		ResultsSection.Visible = s.LastResults.Count > 0;
		if (_resultsMatchId == s.MatchId && Results.GetChildCount() == s.LastResults.Count) return; // unchanged
		_resultsMatchId = s.MatchId;
		foreach (var child in Results.GetChildren()) child.QueueFree();
		foreach (var r in s.LastResults)
		{
			Results.AddChild(new Label
			{
				ThemeTypeVariation = "LobbyResult",
				Text = r.Quit ? $"#{r.Rank}  {r.Name}  -  quit" : $"#{r.Rank}  {r.Name}  -  {r.Score:N0}  ({r.Accuracy:P2})",
				ClipText = true,
				TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
			});
		}
	}
}
