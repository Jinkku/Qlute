using Godot;
using System;
using System.IO;
using System.Linq;
public partial class ResultScreenv2 : Control
{
	private Label Title { get; set; }
	private Label Artist { get; set; }
	private Label Mapper { get; set; }
	private Label Difficulty { get; set; }
	private Label Level { get; set; }
	private Label Score { get; set; }
	private int ScoreValue { get; set; }
	private Label Perfect { get; set; }
	private int PerfectValue { get; set; }
	private Label Great { get; set; }
	private int GreatValue { get; set; }
	private Label Meh { get; set; }
	private int MehValue { get; set; }
	private Label Miss { get; set; }
	private int MissValue { get; set; }
	private Label Accuracy { get; set; }
	private float AccuracyValue { get; set; }
	private Label Combo { get; set; }
	private int ComboValue { get; set; }
	private Label AvgHit { get; set; }
	private int AvgHitValue { get; set; }
	private Label pp { get; set; }
	private int ppValue { get; set; }
	private Label RankGain { get; set; }
	private PanelContainer RankGainPanel { get; set; }
	private PanelContainer Details { get; set; }
	private PanelContainer ScoreCount { get; set; }
	private HBoxContainer HitCount { get; set; }
	private HBoxContainer Additional { get; set; }
	private HBoxContainer Additional2 { get; set; }
	private PanelContainer RankEmblem { get; set; }
	private TextureProgressBar AccuracyProgress { get; set; }
	private double AccuracyProgressTick { get; set; }
	private TextureRect Rank { get; set; }
	private TextureRect PerfectEmblem { get; set; }
	private Tween Tween { get; set; }
	private Tween Tween2 { get; set; }
	private bool PerfectPlay { get; set; }
	private Button WatchReplayButton { get; set; }
	private float HitCountPos { get; set; }
	private float AdditionalPos { get; set; }
	private float Additional2Pos { get; set; }
	private float RankEmblemPos { get; set; }
	private float AnimationSpeed = 0.3f;
	private Control RankingP { get; set; }
	private Control MoreInfoP { get; set; }
	private TextureRect Banner { get; set; }
	private Label RTitle { get; set; }
	private Label RArtist { get; set; }
	private Label RMapper { get; set; }
	private Label RankLead { get; set; }
	private Label Username { get; set; }
	private Label Achieved { get; set; }
	private Label RankedScore { get; set; }
	private Label RankedPoints { get; set; }
	private Label RankPlayer { get; set; }
	private Label RankPlayerLost { get; set; }
	private Label LevelPlayer { get; set; }
	private Label RankedScoreLost { get; set; }
	private Label RankedPointsLost { get; set; }
	private Label LevelPlayerLost { get; set; }
	private Label MaxCombo { get; set; }
	private Label MaxComboLost { get; set; }
	private Label OAccuracy { get; set; }
	private Label AccuracyLost { get; set; }
	/// <summary>
	/// Rank Colour
	/// </summary>
	private Color RPC { get; set; }
	/// <summary>
	/// Level Colour
	/// </summary>
	private Color LPC { get; set; } 
	/// <summary>
	/// Ranked Score Colour
	/// </summary>
	private Color RSC { get; set; } 

	/// <summary>
	/// Ranked Points Colour
	/// </summary>
	private Color RPLC { get; set; } 

	private RankLeaderboard CurrentLead {get;set;}

	private void AnimationMode(int mode)
	{
		Tween?.Kill();
		Tween = CreateTween();
		Tween.SetParallel(true);
		if (mode == 0)
		{	
			Details.Position = new Vector2( Details.Position.X - Details.Size.X, Details.Position.Y);
			Details.Modulate = new Color(0f, 0f, 0f, 0f);
			ScoreCount.Position = new Vector2( ScoreCount.Position.X - ScoreCount.Size.X, ScoreCount.Position.Y);
			ScoreCount.Modulate = new Color(0f, 0f, 0f, 0f);
			HitCount.Position = new Vector2( HitCount.Position.X - HitCount.Size.X, HitCount.Position.Y);
			HitCount.Modulate = new Color(0f, 0f, 0f, 0f);
			Additional.Position = new Vector2( Additional.Position.X - Additional.Size.X, Additional.Position.Y);
			Additional.Modulate = new Color(0f, 0f, 0f, 0f);
			Additional2.Position = new Vector2( Additional2.Position.X - Additional2.Size.X, Additional2.Position.Y);
			Additional2.Modulate = new Color(0f, 0f, 0f, 0f);
			RankEmblem.Position = new Vector2( RankEmblem.Position.X + RankEmblem.Size.X, RankEmblem.Position.Y);
			RankEmblem.Modulate = new Color(0f, 0f, 0f, 0f);
			
			AccuracyProgress.Value = 0;
			Rank.SelfModulate = new Color(1f, 1f, 1f, 0f);
			Rank.PivotOffset = Rank.Size / 2;
			Rank.Scale = new Vector2(1.2f, 1.2f);
			Tween.SetTrans(Tween.TransitionType.Cubic);
			Tween.SetEase(Tween.EaseType.Out);
			Tween.TweenProperty(Details, "position:x", 0,AnimationSpeed);
			Tween.TweenProperty(Details, "modulate", new Color(1f,1f,1f,1f),AnimationSpeed);
			Tween.TweenProperty(ScoreCount, "position:x", 0,AnimationSpeed);
			Tween.TweenProperty(ScoreCount, "modulate", new Color(1f,1f,1f,1f),AnimationSpeed);
			Tween.TweenProperty(HitCount, "position:x", HitCountPos,AnimationSpeed);
			Tween.TweenProperty(HitCount, "modulate", new Color(1f,1f,1f,1f),AnimationSpeed);
			Tween.TweenProperty(Additional, "position:x", AdditionalPos,AnimationSpeed);
			Tween.TweenProperty(Additional, "modulate", new Color(1f,1f,1f,1f),AnimationSpeed);
			Tween.TweenProperty(Additional2, "position:x", Additional2Pos,AnimationSpeed);
			Tween.TweenProperty(Additional2, "modulate", new Color(1f,1f,1f,1f),AnimationSpeed);
			Tween.TweenProperty(RankEmblem, "position:x", RankEmblemPos,AnimationSpeed);
			Tween.TweenProperty(RankEmblem, "modulate", new Color(1f,1f,1f,1f),AnimationSpeed);
			
			Tween.TweenProperty(this, "ScoreValue", SettingsOperator.Gameplaycfg.Score,1);
			Tween.TweenProperty(AccuracyProgress, "value", SettingsOperator.Gameplaycfg.Accuracy * 100,1);
			Tween.TweenProperty(this, "AccuracyValue", SettingsOperator.Gameplaycfg.Accuracy,1);
			Tween.TweenProperty(this, "PerfectValue", SettingsOperator.Gameplaycfg.Max,1);
			Tween.TweenProperty(this, "GreatValue", SettingsOperator.Gameplaycfg.Great,1);
			Tween.TweenProperty(this, "MehValue", SettingsOperator.Gameplaycfg.Meh,1);
			Tween.TweenProperty(this, "MissValue", SettingsOperator.Gameplaycfg.Bad,1);
			Tween.TweenProperty(this, "AvgHitValue", SettingsOperator.Gameplaycfg.ms,1);
			Tween.TweenProperty(this, "ComboValue", SettingsOperator.Gameplaycfg.MaxCombo,1);
			Tween.TweenProperty(this, "ppValue", SettingsOperator.Gameplaycfg.pp,1);
			Tween.TweenProperty(Rank, "self_modulate:a", 1f,0.2).SetDelay(1.1);
			Tween.TweenProperty(Rank, "scale", new Vector2(1f, 1f),0.2f).SetDelay(1.1);
			if (PerfectPlay)
			{
				Tween.TweenProperty(PerfectEmblem, "self_modulate", new Color(1f,1f,1f, 1f),0.2f).SetDelay(1.1);
				Tween.TweenCallback(Callable.From(() => Sample.PlaySample("res://SelectableSkins/Slia/Sounds/applause-fc.wav"))).SetDelay(1.1);
			}
			else if (SettingsOperator.Gameplaycfg.Accuracy > 0.80f)
			{
				Tween.TweenCallback(Callable.From(() => Sample.PlaySample("res://SelectableSkins/Slia/Sounds/applause.wav"))).SetDelay(1.1);
			}
				
		}	else if (mode == 1)
		{
			Details.Position = new Vector2(0, Details.Position.Y);
			Details.Modulate = new Color(1f, 1f, 1f, 1f);
			ScoreCount.Position = new Vector2( 0, ScoreCount.Position.Y);
			ScoreCount.Modulate = new Color(1f, 1f, 1f, 1f);
			HitCount.Position = new Vector2( 0, HitCount.Position.Y);
			HitCount.Modulate = new Color(1f, 1f, 1f, 1f);
			Additional.Position = new Vector2( AdditionalPos, Additional.Position.Y);
			Additional.Modulate = new Color(1f, 1f, 1f, 1f);
			Additional2.Position = new Vector2( Additional2Pos, Additional2.Position.Y);
			Additional2.Modulate = new Color(1f, 1f, 1f, 1f);
			RankEmblem.Position = new Vector2( RankEmblemPos, RankEmblem.Position.Y);
			RankEmblem.Modulate = new Color(1f, 1f, 1f, 1f);
			
			AccuracyProgress.Value = 0;
			Rank.SelfModulate = new Color(1f, 1f, 1f, 0f);
			Rank.PivotOffset = Rank.Size / 2;
			Rank.Scale = new Vector2(1.2f, 1.2f);
			Tween.SetTrans(Tween.TransitionType.Cubic);
			Tween.SetEase(Tween.EaseType.Out);
			Tween.TweenProperty(Details, "position:x", Details.Position.X - Details.Size.X,AnimationSpeed);
			Tween.TweenProperty(Details, "modulate", new Color(1f,1f,1f,0f),AnimationSpeed);
			Tween.TweenProperty(ScoreCount, "position:x", ScoreCount.Position.X - ScoreCount.Size.X,AnimationSpeed);
			Tween.TweenProperty(ScoreCount, "modulate", new Color(1f,1f,1f,0f),AnimationSpeed);
			Tween.TweenProperty(HitCount, "position:x", HitCountPos - HitCount.Size.X,AnimationSpeed);
			Tween.TweenProperty(HitCount, "modulate", new Color(1f,1f,1f,0f),AnimationSpeed);
			Tween.TweenProperty(Additional, "position:x", AdditionalPos - Additional.Size.X,AnimationSpeed);
			Tween.TweenProperty(Additional, "modulate", new Color(1f,1f,1f,0f),AnimationSpeed);
			Tween.TweenProperty(Additional2, "position:x", Additional2Pos - Additional2.Size.X,AnimationSpeed);
			Tween.TweenProperty(Additional2, "modulate", new Color(1f,1f,1f,0f),AnimationSpeed);
			Tween.TweenProperty(RankEmblem, "position:x", RankEmblemPos + RankEmblem.Size.X,AnimationSpeed);
			Tween.TweenProperty(RankEmblem, "modulate", new Color(1f,1f,1f,0f),AnimationSpeed);
			
			Tween.TweenProperty(this, "ScoreValue", SettingsOperator.Gameplaycfg.Score,1);
			Tween.TweenProperty(this, "AccuracyValue", SettingsOperator.Gameplaycfg.Accuracy,1);
			Tween.TweenProperty(this, "PerfectValue", SettingsOperator.Gameplaycfg.Max,1);
			Tween.TweenProperty(this, "GreatValue", SettingsOperator.Gameplaycfg.Great,1);
			Tween.TweenProperty(this, "MehValue", SettingsOperator.Gameplaycfg.Meh,1);
			Tween.TweenProperty(this, "MissValue", SettingsOperator.Gameplaycfg.Bad,1);
			Tween.TweenProperty(this, "AvgHitValue", SettingsOperator.Gameplaycfg.ms,1);
			Tween.TweenProperty(this, "ComboValue", SettingsOperator.Gameplaycfg.MaxCombo,1);
			Tween.TweenProperty(this, "ppValue", SettingsOperator.Gameplaycfg.pp,1);
			Tween.TweenProperty(Rank, "self_modulate:a", 1f,0.2).SetDelay(1.1);
			Tween.TweenProperty(Rank, "scale", new Vector2(1f, 1f),0.2f).SetDelay(1.1);
			Tween.TweenProperty(PerfectEmblem, "self_modulate", new Color(1f,1f,1f, 1f),0.2f).SetDelay(1.1);
			
		} else if (mode == 2)
		{
			Tween.SetTrans(Tween.TransitionType.Cubic);
			Tween.SetEase(Tween.EaseType.Out);
			Tween.TweenProperty(RankingP, "modulate:a", 1f,AnimationSpeed);
			Tween.TweenProperty(MoreInfoP, "modulate:a", 0f,AnimationSpeed);
		} else if (mode == 3)
		{
			Tween.SetTrans(Tween.TransitionType.Cubic);
			Tween.SetEase(Tween.EaseType.Out);
			Tween.TweenProperty(RankingP, "modulate:a", 0f,AnimationSpeed);
		}else if (mode == 4)
		{
			Tween.SetTrans(Tween.TransitionType.Cubic);
			Tween.SetEase(Tween.EaseType.Out);
			Tween.TweenProperty(MoreInfoP, "modulate:a", 1f,AnimationSpeed);
			Tween.TweenProperty(RankingP, "modulate:a", 0f,AnimationSpeed);
		} else if (mode == 5)
		{
			Tween.SetTrans(Tween.TransitionType.Cubic);
			Tween.SetEase(Tween.EaseType.Out);
			Tween.TweenProperty(MoreInfoP, "modulate:a", 0f,AnimationSpeed);
		}
	}
	private bool RankingTick {get;set;} = true;
	private bool MoreInfoTick {get;set;} = true;
	private PanelContainer MInfo {get; set;}
	private Label NA {get; set;}

    public override void _ExitTree()
    {
        ApiOperator.Submitted = false;
    }

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		
		
		Username = GetNode<Label>("MainScreen/Additional2/Card/Column/Row/Username");
		Achieved = GetNode<Label>("MainScreen/Additional2/Card/Column/Row/Achieved");
		Username.Text = SettingsOperator.Gameplaycfg.Username; 
		
		Title = GetNode<Label>("MainScreen/Details/Details/Title");
		Artist = GetNode<Label>("MainScreen/Details/Details/Artist");
		Mapper = GetNode<Label>("MainScreen/Details/Details/Mapper");
		Difficulty = GetNode<Label>("MainScreen/Details/Misc/Stick/Difficulty");
		Level = GetNode<Label>("MainScreen/Details/Misc/Level/Number");
		Score = GetNode<Label>("MainScreen/ScoreCount/HBoxContainer/Score");
		Perfect = GetNode<Label>("MainScreen/HITCOUNT/PERFECT/VBoxContainer/Count");
		Great = GetNode<Label>("MainScreen/HITCOUNT/GREAT/VBoxContainer/Count");
		Meh = GetNode<Label>("MainScreen/HITCOUNT/MEH/VBoxContainer/Count");
		Miss = GetNode<Label>("MainScreen/HITCOUNT/MISS/VBoxContainer/Count");
		Accuracy = GetNode<Label>("MainScreen/Additional/Accuracy/VBoxContainer/Count");
		Combo = GetNode<Label>("MainScreen/Additional/Combo/VBoxContainer/Count");
		AvgHit = GetNode<Label>("MainScreen/Additional/AvgHit/VBoxContainer/Count");
		pp = GetNode<Label>("MainScreen/Additional/pp/VBoxContainer/Count");
		RankGain = GetNode<Label>("MainScreen/Additional/Rankgain/VBoxContainer/Count");
		AccuracyProgress = GetNode<TextureProgressBar>("MainScreen/Rank/AccuracyProgress");
		Rank = GetNode<TextureRect>("MainScreen/Rank/AccuracyProgress/RankEmblem");
		WatchReplayButton = GetNode<Button>("BottomBar/HBoxContainer/WatchReplay");
		RankGainPanel = GetNode<PanelContainer>("MainScreen/Additional/Rankgain");
		PerfectEmblem = GetNode<TextureRect>("MainScreen/ScoreCount/HBoxContainer/Perfect");
		RankLead = GetNode<Label>("MainScreen/Additional2/Card/Column/Number/RankLead");
		RankLead.Text = $"#{SettingsOperator.Gameplaycfg.Rank:N0}";
		Achieved.Text = $"achieved on {Global.GetFormatTime(SettingsOperator.Gameplaycfg.EpochTime)}";
		// Ranking Panel
		RankingP = GetNode<Control>("Ranking");
		RankingP.Visible = true;
		RankingP.Modulate = new Color(1f,1f,1f,0f);
		// More Info Panel
		MoreInfoP = GetNode<Control>("MoreInfo");
		MoreInfoP.Visible = true;
		MoreInfoP.Modulate = new Color(1f,1f,1f,0f);
		MInfo = GetNode<PanelContainer>("MoreInfo/Moreinfo/VBoxContainer/Moreinfo");
		NA = GetNode<Label>("MoreInfo/Moreinfo/VBoxContainer/NA");
		MInfo.Visible = false;
		NA.Visible = false;
		Banner = GetNode<TextureRect>("Ranking/Ranking/VBoxContainer/Banner/Banner");
		Banner.Texture = (Texture2D)SettingsOperator.Sessioncfg["background"];
		RTitle = GetNode<Label>("Ranking/Ranking/VBoxContainer/Banner/Banner/VBoxContainer/Title");
		RArtist = GetNode<Label>("Ranking/Ranking/VBoxContainer/Banner/Banner/VBoxContainer/Artist");
		RMapper = GetNode<Label>("Ranking/Ranking/VBoxContainer/Banner/Banner/VBoxContainer/mapped");
		RankedScore = GetNode<Label>("MoreInfo/Moreinfo/VBoxContainer/Moreinfo/VBoxContainer/GridContainer/Ranked Score/Value");
		RankedPoints = GetNode<Label>("MoreInfo/Moreinfo/VBoxContainer/Moreinfo/VBoxContainer/GridContainer/Ranked Points/Value");
		LevelPlayer = GetNode<Label>("MoreInfo/Moreinfo/VBoxContainer/Moreinfo/VBoxContainer/GridContainer/Level/Value");
		RankPlayer = GetNode<Label>("MoreInfo/Moreinfo/VBoxContainer/Moreinfo/VBoxContainer/GridContainer/Rank/Value");
		LevelPlayer = GetNode<Label>("MoreInfo/Moreinfo/VBoxContainer/Moreinfo/VBoxContainer/GridContainer/Level/Value");
		RankPlayer = GetNode<Label>("MoreInfo/Moreinfo/VBoxContainer/Moreinfo/VBoxContainer/GridContainer/Rank/Value");
		OAccuracy = GetNode<Label>("MoreInfo/Moreinfo/VBoxContainer/Moreinfo/VBoxContainer/GridContainer/Accuracy/Value");
		MaxCombo = GetNode<Label>("MoreInfo/Moreinfo/VBoxContainer/Moreinfo/VBoxContainer/GridContainer/Combo/Value");
		RankPlayerLost = RankPlayer.GetNode<Label>("Lost");
		RankedScoreLost = RankedScore.GetNode<Label>("Lost");
		RankedPointsLost = RankedPoints.GetNode<Label>("Lost");
		LevelPlayerLost = LevelPlayer.GetNode<Label>("Lost");
		MaxComboLost = MaxCombo.GetNode<Label>("Lost");
		AccuracyLost = OAccuracy.GetNode<Label>("Lost");
		//Leaderboard
		CurrentLead = GetNode<RankLeaderboard>("Ranking/Ranking/VBoxContainer/CurrentLead");

		var ranknum = 1;
		var pointold = -1;
		foreach (LeaderboardEntry entry in ApiOperator.LeaderboardList)
		{
			if (entry.points > pointold && entry.username == SettingsOperator.Gameplaycfg.Username)
			{
				CurrentLead.Rank = ranknum;
				CurrentLead.PlayerName = entry.username;
				CurrentLead.Accuracy = entry.Accuracy;
				CurrentLead.Combo = entry.combo;
				CurrentLead.Perfect = entry.MAX;
				CurrentLead.Great = entry.GOOD;
				CurrentLead.Meh = entry.MEH;
				CurrentLead.Miss = entry.BAD;
				CurrentLead.Time = entry.time;
				CurrentLead.Score = entry.score;
				CurrentLead.pp = (int)entry.points;
				pointold = (int)entry.points;
			}
			else
			{
				ranknum++;
			}
			if (entry.Active) 
				break;
		}
		CurrentLead._Ready();
		// Panels

		Details = GetNode<PanelContainer>("MainScreen/Details");
		ScoreCount = GetNode<PanelContainer>("MainScreen/ScoreCount");
		HitCount = GetNode<HBoxContainer>("MainScreen/HITCOUNT");
		Additional = GetNode<HBoxContainer>("MainScreen/Additional");
		Additional2 = GetNode<HBoxContainer>("MainScreen/Additional2");
		RankEmblem = GetNode<PanelContainer>("MainScreen/Rank");
		
		
		// Checks if the replay file is in replay folder (temp until have backend support replay downloading :p)
		if (System.IO.File.Exists(Replay.FilePath) && Replay.FilePath != "")
		{
			WatchReplayButton.Disabled = false;
		}
		else
		{
			WatchReplayButton.Disabled = true;
		}

		PerfectEmblem.SelfModulate = new Color(0f, 0f, 0f, 0f);
		if (SettingsOperator.Gameplaycfg.Bad == 0)
		{
			PerfectPlay = true;
		}
		
		// Panel Position
		
		HitCountPos = HitCount.Position.X;
		AdditionalPos = Additional.Position.X;
		Additional2Pos = Additional2.Position.X;
		RankEmblemPos = RankEmblem.Position.X;
		AnimationMode(0);
		if (SettingsOperator.Gameplaycfg.Accuracy == 1)
		{
			Rank.Texture = GD.Load<CompressedTexture2D>("res://Resources/System/ResultsScreen/Ranks/SS.png");
		}
		else if (SettingsOperator.Gameplaycfg.Accuracy > 0.95)
		{
			Rank.Texture = GD.Load<CompressedTexture2D>("res://Resources/System/ResultsScreen/Ranks/S.png");
		}
		else if (SettingsOperator.Gameplaycfg.Accuracy > 0.90)
		{
			Rank.Texture = GD.Load<CompressedTexture2D>("res://Resources/System/ResultsScreen/Ranks/A.png");
		}
		else if (SettingsOperator.Gameplaycfg.Accuracy > 0.80)
		{
			Rank.Texture = GD.Load<CompressedTexture2D>("res://Resources/System/ResultsScreen/Ranks/B.png");
		}
		else if (SettingsOperator.Gameplaycfg.Accuracy > 0.70)
		{
			Rank.Texture = GD.Load<CompressedTexture2D>("res://Resources/System/ResultsScreen/Ranks/C.png");
		}
		else
		{
			Rank.Texture = GD.Load<CompressedTexture2D>("res://Resources/System/ResultsScreen/Ranks/D.png");
		}
	}

	private void Ranking()
	{
		if (RankingTick)
			AnimationMode(2);
		else 
			AnimationMode(3);
		RankingTick = !RankingTick;
		MoreInfoTick = true;
	}
	private void MoreInfo()
	{
		if (MoreInfoTick)
			AnimationMode(4);
		else 
			AnimationMode(5);
		MoreInfoTick = !MoreInfoTick;
		RankingTick = true;
	}
	private void _up()
	{
		Sample.PlaySample("res://SelectableSkins/Slia/Sounds/selected.wav");
	}
	private void highlight()
	{
		Sample.PlaySample("res://SelectableSkins/Slia/Sounds/hover.wav");
	}

	private void _uppa()
	{
		Sample.PlaySample("res://SelectableSkins/Slia/Sounds/play.wav");
	}
	public void Back()
	{
		if (!MoreInfoTick)
		{
			AnimationMode(5);
			MoreInfoTick = true;
			RankingTick = true;
		}
		else if (!RankingTick)
		{
			AnimationMode(3);
			MoreInfoTick = true;
			RankingTick = true;
		}
		else
		{
			SettingsOperator.OldRank = SettingsOperator.Rank;
			AnimationMode(1);
			if (!AudioPlayer.Instance.IsPlaying())
				AudioPlayer.Instance.Play();
			Replay.FilePath = "";
			GetNode<SceneTransition>("/root/Transition").Switch("res://Panels/Screens/song_select.tscn");
		}
	}

	private void Screenshot()
	{
		var filename = "/screenshot_" + ((int)Directory.GetFiles(SettingsOperator.screenshotdir).Count() + 1) + ".jpg";
		var image = GetViewport().GetTexture().GetImage();
		var scalecrop = 50 * (GetViewportRect().Size.Y / 720);
		int screenWidth = image.GetWidth();
		int screenHeight = image.GetHeight();
		int croppedHeight = screenHeight - (int)(scalecrop * 2);

		// Define the crop area (x, y, width, height)
		// y-coordinate starts after the top cropped area
		Rect2I cropRegion = new Rect2I(0, (int)scalecrop, screenWidth, croppedHeight);

		// Crop the image to the specified region
		Image croppedImage = image.GetRegion(cropRegion);
		croppedImage.SaveJpg(SettingsOperator.screenshotdir + filename);
		Notify.Post($"saved as screenshot_{(int)Directory.GetFiles(SettingsOperator.screenshotdir).Count() + 1}", SettingsOperator.screenshotdir);
		GD.Print(filename);
	}
	private void WatchReplay()
	{
		Replay.ReloadReplay(Replay.FilePath);
		Retry();
	}
	private void _resetreplay()
	{
		SettingsOperator.SpectatorMode = false;
	}
	public void Retry()
	{
		SettingsOperator.OldRank = SettingsOperator.Rank;
		AnimationMode(1);
		GetNode<SceneTransition>("/root/Transition").Switch("res://Panels/Screens/SongLoadingScreen.tscn");
	}

	private LostClass RS {get; set;}
	private LostClass RPP {get; set;}
	private LostClass LP {get; set;}
	private LostClass RP {get; set;}
	private LostClass OA {get; set;}
	private LostClass MC {get; set;}
	private bool ChangedStats { get; set; }
	public override void _PhysicsProcess(double delta)
	{
		Title.Text = SettingsOperator.Sessioncfg["beatmaptitle"]?.ToString() ?? "No Beatmaps Selected";
		Artist.Text = SettingsOperator.Sessioncfg["beatmapartist"]?.ToString() ?? "Please select a Beatmap!";
		Mapper.Text = "mapped by " + SettingsOperator.Sessioncfg["beatmapmapper"]?.ToString();
		Difficulty.Text = SettingsOperator.Sessioncfg["beatmapdiff"]?.ToString();
		Level.Text = $"Lv. {SettingsOperator.LevelRating:N0}";
		RTitle.Text = Title.Text;
		RArtist.Text = Artist.Text;
		RMapper.Text = Mapper.Text;
		if (AccuracyProgressTick != AccuracyProgress.Value)
		{
			AccuracyProgressTick = AccuracyProgress.Value;
			Sample.PlaySample("res://SelectableSkins/Slia/Sounds/score-tick.wav", audiopitch: (float)AccuracyProgress.Value * 0.01f);
		}
		if (ApiOperator.Submitted)
		{
			ChangedStats = true;
			MInfo.Visible = true;
			NA.Visible = false;
			RS = RankUpdate.ReturnLost(SettingsOperator.RankScore, SettingsOperator.OldScore);
			RPP = RankUpdate.ReturnLost(SettingsOperator.ranked_points, SettingsOperator.Oldpp);
			LP = RankUpdate.ReturnLost(SettingsOperator.Level, SettingsOperator.OldLevel);
			OA = RankUpdate.ReturnLost(SettingsOperator.OAccuracy, SettingsOperator.OldAccuracy);
			MC = RankUpdate.ReturnLost(SettingsOperator.OCombo, SettingsOperator.OldCombo);
			RP = RankUpdate.ReturnLost(SettingsOperator.Rank, SettingsOperator.OldRank,reverse: true);
			ApiOperator.Submitted = false;
			RankedScore.Text = $"{SettingsOperator.RankScore:N0}";
			RankedPoints.Text = $"{SettingsOperator.ranked_points:N0}pp";
			LevelPlayer.Text = $"{SettingsOperator.Level:N0}";
			RankPlayer.Text = $"#{Math.Max(1,SettingsOperator.Rank):N0}";
			OAccuracy.Text = $"{SettingsOperator.OAccuracy:P2}";
			MaxCombo.Text = $"{SettingsOperator.OCombo:N0}x";
			// Losses
			RankedScoreLost.Text = $"{RS.prefix} {RS.output:N0}";
			RankedPointsLost.Text = $"{RPP.prefix} {RPP.output:N0}pp";
			LevelPlayerLost.Text = $"{LP.prefix} {LP.output:N0}";
			RankPlayerLost.Text = $"{RP.prefix} #{RP.output:N0}";
			AccuracyLost.Text = $"{OA.prefix} {OA.output:P2}";
			MaxComboLost.Text = $"{MC.prefix} {MC.output:N0}";
			RankedScoreLost.SelfModulate = RS.OperatorColour;
			RankedPointsLost.SelfModulate = RPP.OperatorColour;
			LevelPlayerLost.SelfModulate = LP.OperatorColour;
			RankPlayerLost.SelfModulate = RP.OperatorColour;
			AccuracyLost.SelfModulate = OA.OperatorColour;
			MaxComboLost.SelfModulate = MC.OperatorColour;
		} else if (!ChangedStats)
		{
			NA.Visible = true;	
		}
		
		
		if (Tween != null && Tween.IsRunning())
		{
			pp.Text = $"{ppValue:N0}pp";
			Score.Text = $"{ScoreValue:N0}";
			Perfect.Text = $"{PerfectValue:N0}";
			Great.Text = $"{GreatValue:N0}";
			Meh.Text = $"{MehValue:N0}";
			Miss.Text = $"{MissValue:N0}";
			AvgHit.Text = $"{AvgHitValue:N0}ms";
			Accuracy.Text = $"{AccuracyValue:P2}";
			Combo.Text = $"{ComboValue:N0}x";
			Combo.Text = $"{ComboValue:N0}x";
		}
		if (SettingsOperator.Rank - SettingsOperator.OldRank != 0)
		{
			RankGainPanel.Visible = true;
			var Ranktick = SettingsOperator.Rank - SettingsOperator.OldRank;
			var Rankp = "";
			if (Ranktick < 0)
			{
				Ranktick = -Ranktick;
				Rankp = "+";
			}
			else
			{
				Rankp = "-";
			}
			RankGain.Text = $"{Rankp} #{Ranktick:N0}";
		}
		else
		{
			RankGainPanel.Visible = false;
		}
	}
}
