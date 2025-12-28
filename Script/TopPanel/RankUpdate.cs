using Godot;
using System;
using System.Collections.Generic;

public class LostClass
{
	public string prefix = "-";
	public float output;
	public float rawoutput;
	public Color OperatorColour = new Color(0,0,0,1);
}

public partial class RankUpdate : Control
{

	public Label Performance {get;set;}
	public Label Rank {get;set;}
	public static bool Updated { get; set; }
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
	public static List<Color> ColorOperator = new List<Color>([new Color("#3fff5fff"), new Color("#ffffff00"), new Color("#ff4154ff"),]);
	public static LostClass ReturnLost(float oldput = 0, float oput = 0, bool reverse = false)
	{
		LostClass Opa = new LostClass();
		Opa.OperatorColour = ColorOperator[2];
		Opa.output = oput - oldput;
		Opa.rawoutput = Opa.output;
		if (Opa.output < 0) {
			Opa.output = -Opa.output;
		} 

		if (Opa.output == 0)
		{
			Opa.prefix = "";
			Opa.OperatorColour = ColorOperator[1];
		}else if (!reverse && Opa.rawoutput < 0)
		{
			Opa.prefix = "+";
			Opa.OperatorColour = ColorOperator[0];
		}
		else if (reverse && Opa.rawoutput > 0)
		{
			Opa.prefix = "+";
			Opa.OperatorColour = ColorOperator[0];
		}

		return Opa;
	}
	// Called when the node enters the scene tree for the first time.
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
		UserCard.Visible = false;
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
			OperatorPerformance.SelfModulate = PL.OperatorColour;
			OperatorRank.SelfModulate = RL.OperatorColour;

			RankDisplay = SettingsOperator.OldRank;
			PerfDisplay = SettingsOperator.Oldpp;
			ORankDisplay = (int)RL.output;
			OPerfDisplay = (int)PL.output;

			tweenNum?.Kill();
			tweenNum = CreateTween();
			tweenNum.Parallel().TweenProperty(this, "RankDisplay", SettingsOperator.Rank - RL.rawoutput, 1f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out).SetDelay(1);
			tweenNum.Parallel().TweenProperty(this, "PerfDisplay", SettingsOperator.ranked_points - PL.rawoutput, 1f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out).SetDelay(1);
			tweenNum.Parallel().TweenProperty(this, "ORankDisplay", 0, 1f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out).SetDelay(1);
			tweenNum.Parallel().TweenProperty(this, "OPerfDisplay", 0, 1f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out).SetDelay(1);

			tween?.Kill();
			tween = CreateTween();
			tween.SetParallel(true);
			tween.TweenCallback(Callable.From(UserCard.Show));
			tween.TweenProperty(this, "position", tmppos, 0.3f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
			tween.TweenProperty(this, "modulate", new Color(1f, 1f, 1f, 1f), 0.3f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
			tween.TweenProperty(this, "position", backpos, 0.3f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out).SetDelay(5);
			tween.TweenProperty(this, "modulate", new Color(1f, 1f, 1f, 0f), 0.3f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out).SetDelay(5);
			tween.TweenCallback(Callable.From(UserCard.Hide)).SetDelay(6);
			tween.Play();
		}

		if (Realtime)
		{
			if (RankDisplay == 0 && PerfDisplay == 0)
			{
				Realtime = false;
			}


			Rank.Text = $"#{RankDisplay:N0}";
			OperatorRank.Text = $"{RL.prefix} #{ORankDisplay:N0}";

			SettingsOperator.Rank = RankDisplay;
			SettingsOperator.ranked_points = PerfDisplay;

			Performance.Text = $"{(PerfDisplay):N0}pp";
			OperatorPerformance.Text = $"{PL.prefix} {OPerfDisplay:N0}pp";
		}
	}
	public static LostClass PL {get; set;} = new LostClass();
	public static LostClass RL {get; set;} = new LostClass();
	public static void Update(int rank,int pp){
		SettingsOperator.Oldpp = SettingsOperator.ranked_points;
		SettingsOperator.OldRank = SettingsOperator.Rank;
		PL = ReturnLost(pp, SettingsOperator.ranked_points);
		RL = ReturnLost(rank, SettingsOperator.Rank,reverse:true);
		Updated = true;
	}
}
