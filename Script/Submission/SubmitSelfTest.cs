using Godot;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// Dev self-test for beatmap submission against a (local) Chikara server. Started by OnlineChat when the game is run
/// with --online-test-user=NAME --online-test-pass=SHA256 and --submit-test=FOLDER (QLUTE_API_BASE=http://host:port).
/// Submits FOLDER, prints the ids written into its .osu files, then submits it again (which updates the set). Grep [SUBMITTEST].
/// </summary>
public partial class SubmitSelfTest : Node
{
	private static void Log(string s) => GD.Print("[SUBMITTEST] " + s);

	public override async void _Ready()
	{
		var folder = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--submit-test="))?["--submit-test=".Length..];
		await ToSignal(GetTree().CreateTimer(2), SceneTreeTimer.SignalName.Timeout);
		for (int round = 1; round <= 2; round++)
		{
			var (r, error) = await BeatmapSubmit.Submit(folder);
			Log($"round {round}: error={error} set={r?.SetId} updated={r?.Updated} sent={BeatmapSubmit.Sent}/{BeatmapSubmit.Total} " +
			    $"maps={string.Join(",", r?.Beatmaps.Select(b => $"{b.Version}={b.BeatmapId}") ?? new string[0])} skipped={string.Join(",", r?.Skipped ?? new())}");
			foreach (var f in Directory.GetFiles(folder, "*.osu"))
				Log($"  {Path.GetFileName(f)}: {string.Join(" ", File.ReadAllLines(f).Where(l => l.StartsWith("Beatmap")))} crlf={File.ReadAllText(f).Contains("\r\n")}");
		}
		Log("done");
		GetTree().Quit();
	}
}
