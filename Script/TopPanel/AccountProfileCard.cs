using Godot;
using System;

public partial class AccountProfileCard : Control
{
	// Called when the node enters the scene tree for the first time.
	public string Username = "Guest";
	public static SettingsOperator SettingsOperator { get; set; }
	private void _on_logout_pressed(){
		GD.Print("Tried logging out failed....");
	}
	public override void _Ready()
	{
		SettingsOperator = GetNode<SettingsOperator>("/root/SettingsOperator");
		
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		Label Ranking = GetNode<Label>("./Panel/RankingNumber");
		Label PlayerName = GetNode<Label>("./Panel/UsernameSection/Username"); 
		PlayerName.Text = SettingsOperator.GetSetting("username")?.ToString();
		Ranking.Text ="#" + SettingsOperator.Sessioncfg["ranknumber"]?.ToString();
	}
}
