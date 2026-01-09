using Godot;
using System;

public partial class AccountButton : Button
{
	public Label Ranking {get;set;}
	public Label PlayerName {get;set;}
	private HttpRequest PfpHttp { get; set; }
	private TextureRect Picture { get; set; }
	private int tickpic { get; set; }
	private string Urltmppfp { get; set; }
	private void ChangePicture(int mode, Texture2D Texture)
	{
		if (mode != tickpic)
		{
			tickpic = mode;
			Picture.Texture = Texture;
		}
	}
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		TopPanel = GetNode<Control>("../");
		PlayerName = GetNode<Label>("UPlayerName");
		Ranking = GetNode<Label>("Ranking");
		Picture = GetNode<TextureRect>("PanelContainer/ProfilePicture");
		SelfModulate = Idlecolour;
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public async override void _Process(double delta)
	{
		string username = SettingsOperator.GetSetting("username")?.ToString();
		if (Urltmppfp != SettingsOperator.ProfilePictureURL && SettingsOperator.ProfilePictureURL != null)
		{
			Urltmppfp = SettingsOperator.ProfilePictureURL;
			await ApiOperator.DownloadImage(Urltmppfp, (ImageTexture texture) =>
			{
				ApiOperator.PictureData = texture;
				ChangePicture(1,texture);
			});
		}
		if (username != null){
			PlayerName.Text = username;
			if (SettingsOperator.Rank != 0){
				Ranking.Visible = true;}
			else
			{
				Ranking.Visible = false;
			}
			
			Ranking.Text = "#" + SettingsOperator.Rank.ToString("N0");
		} else
		{
			ApiOperator.PictureData = null;
			SettingsOperator.ProfilePictureURL = null;
			Urltmppfp = SettingsOperator.ProfilePictureURL;
			ChangePicture(0,GD.Load<CompressedTexture2D>("res://Resources/System/guest.png"));
			PlayerName.Text = "Guest\nLog in here!";
			Ranking.Visible = false;
		}
	}
	
	private void _on_AccountButton_pressed(){
		toggleaccountpanel();
	}
	private Tween _focus_animation;
	private void AnimationButton(Color colour)
	{
		_focus_animation?.Kill();

		_focus_animation = CreateTween();
		_focus_animation.SetParallel(true);
		_focus_animation.TweenProperty(this, "self_modulate", colour, 0.2f)
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.Out);
	}
	private Color Idlecolour = new Color("#4B4B4B");
	private Color Focuscolour = new Color("#686868");
	private Color highlightcolour = new Color("#7573B1");
	private void _highlight() {
		AnimationButton(highlightcolour);
	}
	private void _hover()
	{
		AnimationButton(Focuscolour);
	}
	private void _unhover()
	{
		BeatmapInfo.BeatmapIndexH = -1;
		AnimationButton(Idlecolour);
	}
	private Control TopPanel { get; set; }
	public Control Card;
	public Tween acctween;
	public void toggleaccountpanel(){
		var loggedin = (bool)SettingsOperator.Sessioncfg["loggedin"];
		acctween?.Kill();
		acctween = CreateTween();
		if (!(bool)SettingsOperator.Sessioncfg["showaccountpro"])
		{
			Card = GD.Load<PackedScene>("res://Panels/Overlays/AccountPrompt.tscn").Instantiate().GetNode<Control>(".");
			Card.ZIndex = -1;
			TopPanel.AddChild(Card);
			Card.Position = new Vector2(0, -265);
			acctween.TweenProperty(Card, "position", new Vector2(0,SettingsOperator.TopPanelPosition), 0.2f).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
			acctween.Play();
		}
		else
		{
			acctween.TweenProperty(Card, "position", new Vector2(0,-265), 0.2f).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
			acctween.TweenCallback(Callable.From(Card.QueueFree));
		}
		SettingsOperator.Sessioncfg["showaccountpro"] = !(bool)SettingsOperator.Sessioncfg["showaccountpro"];}
}
