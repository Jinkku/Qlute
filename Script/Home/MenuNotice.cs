using Godot;
using System;
using System.Text;

public partial class MenuNotice : HttpRequest
{
	// Called when the node enters the scene tree for the first time.
	private SettingsOperator SettingsOperator { get; set; }
	public override void _Ready()
	{
		SettingsOperator = GetNode<SettingsOperator>("/root/SettingsOperator");
	}
	private void getnews(){
		Request(SettingsOperator.GetSetting("api").ToString()+"apiv2/menunotice");
	}
	private void _on_request_completed(int result, int responseCode, string[] headers, byte[] body)
	{
		if (responseCode == 200){
		GetNode<Label>("../NoticeText").Text = Encoding.UTF8.GetString(body);
		GetNode<PanelContainer>("..").Visible = true;
		}else{
		GetNode<Label>("../NoticeText").Text = "The Qlute servers are having some technical difficulties!\nWe are investigating into this issue >n<";
		GetNode<PanelContainer>("..").Visible = true;			
		}
	}
	private void _on_timer_timeout(){
		getnews();
	}
}
