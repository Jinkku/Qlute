using Godot;
using System;
using System.Collections.Generic;

public partial class RankUpdate : Control
{
	// Called when the node enters the scene tree for the first time.

	public Label Performance {get;set;}
	public Label Rank {get;set;}
	public static int RankLost { get; set; }
	public static int PerformanceLost { get; set; }
	public static int RankNow { get; set; }
	public static int PerformanceNow { get; set; }
	public static string prefixRank {get;set;}
	public static string prefixPerformance {get;set;}
	public static int OldRank {get;set;}
	public static int OldPerformance {get;set;}
	public static bool Updated { get; set; }
	public static Color ORank { get; set; }
	public static Color OPerf { get; set; }
	private PanelContainer UserCard { get; set; }
	private Label OperatorPerformance { get; set;}
	private Label OperatorRank { get; set;}
	private Tween tween { get; set; }
	private Tween tweenNum { get; set; }



	
	private int RankDisplay { get; set; }
	private int PerfDisplay { get; set; }
	private int ORankDisplay { get; set; }
	private int OPerfDisplay { get; set; }



	private bool Realtime { get; set; }
	public static List<Color> ColorOperator = new List<Color>([new Color("#3fff5fff"), new Color("#FFFFFF"), new Color("#ff4154ff"),]);

	public static  Color GetOperatorColour(int num, int oldnum, bool opo= false)
	{
		var numres = num - oldnum;
		var index = 0;
		if (opo)
		{
			if (numres < 0) index = 0;
			else if (numres == 0) index = 1;
			else if (numres > 0) index = 2;
		}
		else
		{
			if (numres < 0) index = 2;
			else if (numres == 0) index = 1;
			else if (numres > 0) index = 0;
		}


		return ColorOperator[index];
	}
	public override void _Ready()
	{
		UserCard = GD.Load<PackedScene>("res://Panels/UserCard/UserCard.tscn").Instantiate().GetNode<PanelContainer>(".");
		UserCard.Set("PauseRealTime", true);
		AddChild(UserCard);
		UserCard.Position = new Vector2(0, 60);
		Rank = UserCard.GetNode<Label>("SplitV/BottomPart/Sections/Rank/Value");
		Performance = UserCard.GetNode<Label>("SplitV/BottomPart/Sections/Performance/Value");
		OperatorRank = UserCard.GetNode<Label>("SplitV/BottomPart/Sections/Rank/Operation");
		OperatorPerformance = UserCard.GetNode<Label>("SplitV/BottomPart/Sections/Performance/Operation");
		OperatorRank.Visible = true;
		OperatorPerformance.Visible = true;
		Modulate = new Color(1f, 1f, 1f, 0f);

	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		if (Updated)
		{
			Modulate = new Color(1f, 1f, 1f, 0f);
			Realtime = true;
			Updated = false;
			var tmppos = new Vector2(10, Position.Y);
			var backpos = new Vector2(Position.X - 10, Position.Y);
			Position = backpos;


			//New Card
			OperatorPerformance.SelfModulate = OPerf;
			OperatorRank.SelfModulate = ORank;

			RankDisplay = OldRank;
			PerfDisplay = OldPerformance;
			ORankDisplay = RankLost;
			OPerfDisplay = PerformanceLost;




			tweenNum?.Kill();
			tweenNum = CreateTween();
			tweenNum.Parallel().TweenProperty(this, "RankDisplay", RankNow, 1f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out).SetDelay(1);
			tweenNum.Parallel().TweenProperty(this, "PerfDisplay", PerformanceNow, 1f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out).SetDelay(1);
			tweenNum.Parallel().TweenProperty(this, "ORankDisplay", 0, 1f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out).SetDelay(1);
			tweenNum.Parallel().TweenProperty(this, "OPerfDisplay", 0, 1f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out).SetDelay(1);

			tween?.Kill();
			tween = CreateTween();
			tween.SetParallel(true);
			tween.TweenProperty(this, "position", tmppos, 0.3f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
			tween.TweenProperty(this, "modulate", new Color(1f, 1f, 1f, 1f), 0.3f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
			tween.TweenProperty(this, "position", backpos, 0.3f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out).SetDelay(5);
			tween.TweenProperty(this, "modulate", new Color(1f, 1f, 1f, 0f), 0.3f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out).SetDelay(5);
			tween.Play();
		}

		if (Realtime)
		{
			if (RankDisplay == 0 && PerfDisplay == 0)
			{
				Realtime = false;
			}


			Rank.Text = $"#{RankDisplay.ToString("N0")}";
			OperatorRank.Text = $"{prefixRank}#{ORankDisplay.ToString("N0")}";

			SettingsOperator.Sessioncfg["ranknumber"] = RankDisplay;
			SettingsOperator.ranked_points = PerfDisplay;


			Performance.Text = $"{(PerfDisplay).ToString("N0")}pp";
			OperatorPerformance.Text = $"{prefixPerformance} {OPerfDisplay.ToString("N0")}pp";
		}
	}
	public static void Update(int rank,int pp){
		prefixRank = "";
		prefixPerformance = "";
		OldPerformance = SettingsOperator.ranked_points;
		OldRank = (int)SettingsOperator.Sessioncfg["ranknumber"];
		RankLost = rank - OldRank;
		PerformanceLost = pp - SettingsOperator.ranked_points;
		if (RankLost > 0)
		{
			RankLost = RankLost;
			prefixRank = "-";
		}
		else if (RankLost == 0)
		{
			prefixRank = "";
		}
		else if (RankLost < 0)
		{
			RankLost = -RankLost;
			prefixRank = "+";
		}
		
		if (PerformanceLost > 0)
		{
			prefixPerformance = "+";
		}
		else if (PerformanceLost == 0)
		{
			prefixPerformance = "";
		}
		else if (PerformanceLost < 0)
		{
			PerformanceLost = -PerformanceLost;
			prefixPerformance = "-";
		}
		OPerf = GetOperatorColour(pp, SettingsOperator.ranked_points);
		ORank = GetOperatorColour(rank, OldRank, opo: true);

		SettingsOperator.UpdatedRank = prefixRank + RankLost.ToString("N0");
		SettingsOperator.Updatedpp = prefixRank + PerformanceLost.ToString("N0") + "pp";
		RankNow = rank;
		PerformanceNow = pp;
		Updated = true;
	}
}
