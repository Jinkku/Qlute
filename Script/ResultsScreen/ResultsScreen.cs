using Godot;
using System;

public partial class ResultsScreen : Control
{
	// Called when the node enters the scene tree for the first time.
	public Label SongTitle { get; set; }
	public Label SongArtist { get; set; }
	public Label SongMapper { get; set; }
	public Label SongDiff { get; set; }
	public Label Max { get; set; }
	public Label Great { get; set; }
	public Label Meh { get; set; }
	public Label Bad { get; set; }
	public Label pp { get; set; }
	public Label Score { get; set; }
	public Label Combo { get; set; }
	public Label Accuracy { get; set; }
	public Label Avgms { get; set; }
	public Label AccuracyMedal { get; set; }
	public PanelContainer AccuracyPanel { get; set; }
	public string AccM {get;set;}
	public override void _Ready()
	{
		
		SongTitle = GetNode<Label>("AlertBox/Box/Title");
		SongArtist = GetNode<Label>("AlertBox/Box/Artist");
		SongDiff = GetNode<Label>("AlertBox/Box/Difficulty");
		SongMapper = GetNode<Label>("AlertBox/Box/Creator");
		pp = GetNode<Label>("AlertBox/Box/Info/pp/VC/Text");
		Score = GetNode<Label>("AlertBox/Box/Score");
		Max = GetNode<Label>("AlertBox/Box/Info/MAX/VC/Text");
		Great = GetNode<Label>("AlertBox/Box/Info/GREAT/VC/Text");
		Meh = GetNode<Label>("AlertBox/Box/Info/MEH/VC/Text");
		Bad = GetNode<Label>("AlertBox/Box/Info/BAD/VC/Text");
		Accuracy = GetNode<Label>("AlertBox/Box/Info/Accuracy/VC/Text");
		Combo = GetNode<Label>("AlertBox/Box/Info/Combo/VC/Text");
		Avgms = GetNode<Label>("AlertBox/Box/Info/AvgHit/VC/Text");
		AccuracyPanel = GetNode<PanelContainer>("AlertBox/Box/Rank");
		AccuracyMedal = GetNode<Label>("AlertBox/Box/Rank/Medal");
		SongTitle.Text = SettingsOperator.Sessioncfg["beatmaptitle"]?.ToString() ?? "No Beatmaps Selected";
		SongArtist.Text = SettingsOperator.Sessioncfg["beatmapartist"]?.ToString() ?? "Please select a Beatmap!";
		SongMapper.Text = "Creator: " + SettingsOperator.Sessioncfg["beatmapmapper"]?.ToString();
		SongDiff.Text = SettingsOperator.Sessioncfg["beatmapdiff"]?.ToString();
		Max.Text = SettingsOperator.Gameplaycfg["max"].ToString("N0");
		Great.Text = SettingsOperator.Gameplaycfg["great"].ToString("N0");
		Meh.Text = SettingsOperator.Gameplaycfg["meh"].ToString("N0");
		Bad.Text = SettingsOperator.Gameplaycfg["bad"].ToString("N0");
		pp.Text = SettingsOperator.Gameplaycfg["pp"].ToString("N0")+"/"+(SettingsOperator.Gameplaycfg["maxpp"]*ModsMulti.multiplier).ToString("N0");
		Combo.Text = ((double)SettingsOperator.Gameplaycfg["maxcombo"]).ToString("N0");
		Avgms.Text = ((double)SettingsOperator.Gameplaycfg["ms"]).ToString("N0")+"ms";
		Score.Text = SettingsOperator.Gameplaycfg["score"].ToString("0,000,000");
		var Acc = SettingsOperator.Gameplaycfg["accuracy"];
		Accuracy.Text = Acc.ToString("P0");
		if (Acc > 0.95)
		{
			AccM = "S";
			AccuracyPanel.SelfModulate = new Color(0.23f, 0.47f, 0.83f); // #3b78d3 (Blue)
		}
		else if (Acc > 0.90)
		{
			AccM = "A";
			AccuracyPanel.SelfModulate = new Color(0.40f, 0.73f, 0.19f); // #67b930 (Green)
		}
		else if (Acc > 0.80)
		{
			AccM = "B";
			AccuracyPanel.SelfModulate = new Color(0.77f, 0.76f, 0.13f); // #c5c220 (Gold)
		}
		else if (Acc > 0.70)
		{
			AccM = "C";
			AccuracyPanel.SelfModulate = new Color(0.83f, 0.44f, 0.13f); // #d47037 (Orange)
		}
		else
		{
			AccM = "D";
			AccuracyPanel.SelfModulate = new Color(0.83f, 0.22f, 0.22f); // #d43737 (Red)
		}
		// Set the Rank medal text
		AccuracyMedal.Text = AccM;

	}
	public void _retry(){
		GetNode<SceneTransition>("/root/Scene").Switch("res://Panels/Screens/SongLoadingScreen.tscn");
	}
	public void _continue(){
		GetNode<SceneTransition>("/root/Scene").Switch("res://Panels/Screens/song_select.tscn");
		AudioPlayer.Instance.Play();
	}
	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}
}
