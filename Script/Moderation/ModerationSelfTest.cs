using Godot;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// Dev self-test for reports / bans / the admin panel against a (local) Chikara server. Started by OnlineChat when the
/// game is run with --online-test-user=NAME --online-test-pass=SHA256 and --mod-test=STEPS (QLUTE_API_BASE=http://host:port).
/// STEPS (comma separated): api (calls every endpoint the panel uses), admin, user=NAME, report=NAME, profile=NAME.
/// --mod-test-shot=DIR saves a screenshot after each UI step (windowed only). Grep [MODTEST].
/// </summary>
public partial class ModerationSelfTest : Node
{
	private static void Log(string s) => GD.Print("[MODTEST] " + s);

	public override async void _Ready()
	{
		var args = OS.GetCmdlineUserArgs();
		string Value(string key) => args.FirstOrDefault(a => a.StartsWith(key))?[key.Length..];
		var steps = (Value("--mod-test=") ?? "").Split(',', System.StringSplitOptions.RemoveEmptyEntries);
		var shots = Value("--mod-test-shot=");

		await Wait(2);
		ModerationApi.RefreshMe();
		await Wait(2);
		Log($"user={ApiOperator.Username} staff={ModerationApi.IsStaff} role={ModerationApi.Role} open={ModerationApi.OpenReports}");

		var n = 0;
		foreach (var step in steps)
		{
			var (name, arg) = step.Contains('=') ? (step[..step.IndexOf('=')], step[(step.IndexOf('=') + 1)..]) : (step, "");
			switch (name)
			{
				case "api": await Api(); continue;
				case "admin": PlayerOverlays.OpenAdmin(); break;
				case "user": PlayerOverlays.OpenAdmin(arg); break;
				case "report": PlayerOverlays.OpenReport(arg, "score 1234"); break;
				case "profile": PlayerOverlays.OpenProfile(arg); break;
				case "list": PlayerOverlays.ToggleList(); break;
				case "tab": PlayerOverlays.OpenAdminTab(arg); break;
				case "content": await Content(arg); continue;
				case "beatmap": await OpenBeatmap(arg); break;
				case "beatmapscroll": ScrollOverlay(int.Parse(arg)); break;
				default: Log("unknown step " + step); continue;
			}
			await Wait(3);
			if (shots != null)
			{
				var path = $"{shots}/{++n:00}-{name}{(arg == "" ? "" : "-" + arg)}.png";
				GetViewport().GetTexture().GetImage().SavePng(path);
				Log("shot " + path);
			}
		}
		Log("done");
		GetTree().Quit();
	}

	/// <summary>beatmap=SETID: the set's info overlay (looked up with apiv2/search).</summary>
	private static async Task OpenBeatmap(string setId)
	{
		using var http = new System.Net.Http.HttpClient();
		var json = await http.GetStringAsync($"{PlayerApi.Base}apiv2/search?query={setId}&status=1");
		var set = System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.List<BrowseCatalogLegend>>(json)?.FirstOrDefault();
		Log($"beatmap {setId} -> {set?.title ?? "not found"}");
		PlayerOverlays.OpenBeatmap(set);
	}

	/// <summary>beatmapscroll=PX: scrolls the topmost overlay page.</summary>
	private void ScrollOverlay(int px)
	{
		var layer = GetTree().Root.GetNodeOrNull("PlayerOverlays");
		var top = layer?.GetChild(layer.GetChildCount() - 1);
		var scroll = top?.FindChildren("*", "ScrollContainer", true, false).OfType<ScrollContainer>().FirstOrDefault();
		if (scroll != null) scroll.ScrollVertical = px;
	}

	private async Task Wait(double s) => await ToSignal(GetTree().CreateTimer(s), SceneTreeTimer.SignalName.Timeout);

	private static async Task Api()
	{
		var (reports, open, e1) = await ModerationApi.Reports("open");
		Log($"reports open={open} got={reports?.Count} error={e1}");
		foreach (var r in reports ?? new()) Log($"  #{r.Id} {r.Reporter} -> {r.Target} [{r.CategoryText}] {r.Details}");
		var (users, e2) = await ModerationApi.Users("q", false);
		Log($"users(q)={string.Join(",", users?.Select(u => u.Username + (u.Restricted ? "(R)" : "")) ?? new string[0])} error={e2}");
		var (u, e3) = await ModerationApi.User("Kupachan");
		Log($"user Kupachan restricted={u?.Restricted} canBan={u?.CanBan} bans={u?.Bans.Count} reports={u?.ReportsAgainst.Count} error={e3}");
		Log("ban Kupachan 1h -> " + (await ModerationApi.BanUser("Kupachan", "Self-test ban", 1, 0, false) ?? "ok"));
		(u, _) = await ModerationApi.User("Kupachan");
		var b = u?.Bans.FirstOrDefault();
		Log($"  now restricted={u?.Restricted} ban='{b?.Reason}' active={b?.Active} left={b?.TimeLeft}");
		Log("unban Kupachan -> " + (await ModerationApi.Unban("Kupachan", "self-test") ?? "ok"));
		(u, _) = await ModerationApi.User("Kupachan");
		Log($"  now restricted={u?.Restricted}");
		var (bans, e4) = await ModerationApi.Bans(true);
		Log($"active bans={string.Join(",", bans?.Select(x => x.Username) ?? new string[0])} error={e4}");
		var (staff, canEdit, e5) = await ModerationApi.Staff();
		Log($"staff={string.Join(",", staff?.Select(s => s.Username + ":" + s.Role) ?? new string[0])} canEdit={canEdit} error={e5}");
		Log("add staff as mod -> " + (await ModerationApi.SetStaff("qtest_bob", "mod") ?? "ok"));
		var (msg, e6) = await ModerationApi.SendReport("Kupachan", "spam", "Self-test report from the game", "score 99");
		Log($"report -> {msg ?? e6}");
		(msg, e6) = await ModerationApi.SendReport(ApiOperator.Username, "spam", "reporting myself", "");
		Log($"report self -> {msg ?? e6}");
	}

	private static async Task Content(string dir)
	{
		Log("add notice_big -> " + (await ModerationApi.AddNotice(dir + "/notice_big.png", "Big game notice", "example.com") ?? "ok"));
		var (notices, e2) = await ModerationApi.Notices();
		Log($"notices={string.Join(" | ", notices?.Select(n => $"{n.Text} [{n.Redirect}] {n.Image}") ?? new string[0])} error={e2}");
		var last = notices?.LastOrDefault();
		if (last != null)
		{
			Log("move last up -> " + (await ModerationApi.MoveNotice(last.Id, -1) ?? "ok"));
			Log("edit last -> " + (await ModerationApi.EditNotice(last.Id, "Renamed in game", "", null) ?? "ok"));
		}
	}
}
