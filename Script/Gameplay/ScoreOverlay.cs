using Godot;
using System;

public partial class ScoreOverlay : Control
{
	// Called when the node enters the scene tree for the first time.
	private SettingsOperator SettingsOperator { get; set; }
	public static Label pp { get; set; }
	public static Label score { get; set; }
	public static Label Accuracy { get; set; }
	private Tween scoretween {get;set;}
	private int newscore {get;set;}
	private int oldscore {get;set;}
	private int scorev {get;set;}
	public override void _Ready()
	{
		SettingsOperator = GetNode<SettingsOperator>("/root/SettingsOperator");
		score = GetNode<Label>("ScoreBarBack/ScoreInfo/Score");
		pp = GetNode<Label>("ScoreBarBack/ScoreInfo/InfoStatus/pp");
		Accuracy = GetNode<Label>("ScoreBarBack/ScoreInfo/InfoStatus/Accuracy");
		_Process(0);
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double _delta)
	{
		if (oldscore != newscore)
		{
			oldscore = newscore;
			scoretween?.Kill();
			scoretween = CreateTween();
			scoretween.TweenProperty(this, "scorev", newscore, 0.3f);
			scoretween.Play();
		}else newscore = SettingsOperator.Gameplaycfg.Score;
		
		score.Text = scorev.ToString("N0");
		pp.Text = ((int)SettingsOperator.Gameplaycfg.pp).ToString("N0") + "pp";
		var Acc = SettingsOperator.Gameplaycfg.Accuracy.ToString("P2");
		if (SettingsOperator.Gameplaycfg.Max + SettingsOperator.Gameplaycfg.Great + SettingsOperator.Gameplaycfg.Meh + SettingsOperator.Gameplaycfg.Bad == 0){
			Acc = "100.00%";
		}Accuracy.Text = Acc;
	}
}
