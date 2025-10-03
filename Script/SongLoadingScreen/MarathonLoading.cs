using Godot;
using System;

public partial class MarathonLoading : Control
{
    public AnimationPlayer Animation { get; set; }
    public SettingsOperator SettingsOperator { get; set; }
	public Timer ArtificialLoad {get;set;}
	int anistate = 0;
    public override void _Ready()
    {
        SettingsOperator.Marathon = true;
		SettingsOperator = GetNode<SettingsOperator>("/root/SettingsOperator");
		ArtificialLoad=GetNode<Timer>("./Timer");
		Animation=GetNode<AnimationPlayer>("./Wafuk");
		Animation.Play("AnimationSongTick");
		SettingsOperator.toppaneltoggle(true);
    }
	private void _Animationf(string ani){
		ArtificialLoad.Start();
		if (anistate <1){
			anistate++;
		}else{
			GetNode<SceneTransition>("/root/Transition").Switch("res://Panels/Screens/Gameplay.tscn");
		}
	}
	private void _on_back(){
		GetNode<SceneTransition>("/root/Transition").Switch("res://Panels/Screens/song_select.tscn");
	}
	private void _Timer_load(){
		Animation.PlayBackwards("AnimationSongTick");
		ArtificialLoad.Stop();
	}
}
