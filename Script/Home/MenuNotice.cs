using Godot;
using System;
using System.Text;

public partial class MenuNotice : HttpRequest
{
	// Called when the node enters the scene tree for the first time.
	private SettingsOperator SettingsOperator { get; set; }
	private Tween Tween { get; set; }
	private Label NoticeText { get; set; }
	private PanelContainer Panel { get; set; }
	private string NewText { get; set; }
	public override void _Ready()
	{
		SettingsOperator = GetNode<SettingsOperator>("/root/SettingsOperator");
		NoticeText = GetNode<Label>("../NoticeText");
		Panel = GetNode<PanelContainer>("..");
		Panel.Modulate = new Color(1f, 1f, 1f, 0f);
		getnews();
	}
	private void getnews(){
		Request(SettingsOperator.GetSetting("api").ToString()+"apiv2/menunotice");
	}
	private void _on_request_completed(int result, int responseCode, string[] headers, byte[] body)
	{
		if (responseCode == 200)
		{
			NewText = Encoding.UTF8.GetString(body);
		}
		else
		{
			NewText = "The Qlute servers are having some technical difficulties!\nWe are investigating into this issue >n<";
		}
		Tween?.Kill();
		Tween = CreateTween();
		Tween.TweenProperty(Panel, "modulate:a", 0f, 0.3f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		Tween.TweenCallback(Callable.From(() => NoticeText.Text = NewText));
		Tween.TweenProperty(Panel, "modulate:a", 1f, 0.3f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		Tween.Play();
	}
	private void _on_timer_timeout(){
		getnews();
	}
}
