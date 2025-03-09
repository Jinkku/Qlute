using Godot;
using System;

public partial class RankUpdate : Label
{
	// Called when the node enters the scene tree for the first time.
	public static Label PointsUpdate {get;set;}
	public static Label RankUpdateT {get;set;}
	public static bool Updated {get;set;}
	public override void _Ready()
	{
		PointsUpdate = GetNode<Label>("PerformanceUpdate");
		RankUpdateT = GetNode<Label>(".");
		Modulate = new Color(1f,1f,1f,0f);
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		if (Updated){
			Updated = false;
			var tmppos = Position;
			var backpos = new Vector2(Position.X-10,Position.Y);
			Position = backpos;
			var tween = RankUpdateT.CreateTween();
			tween.Parallel().TweenProperty(this, "position", tmppos, 0.3f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
			tween.Parallel().TweenProperty(this, "modulate", new Color(1f,1f,1f,1f), 0.3f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
			tween.TweenProperty(this, "modulate", new Color(1f,1f,1f,1f), 2).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
			tween.TweenProperty(this, "modulate", new Color(1f,1f,1f,0f), 0.3f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
			tween.Play();
		}
	}
	public static void Update(int rank,int pp){
		
		PointsUpdate.Text = "Performance\n+" +pp.ToString("N0") + "pp";
		RankUpdateT.Text = "Global Rank\n#" + rank.ToString("N0");
		Updated = true;
	}
}
