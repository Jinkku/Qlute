using Godot;
using System;

public partial class SongSelect : Control
{
	// Called when the node enters the scene tree for the first time.
	Label debugsong { get; set; }
	public static SettingsOperator SettingsOperator { get; set; }
	public void AddSongList(string song,Vector2 pos)
	{
		debugsong.Text += song + "\n";
	}
	public override void _Ready()
	{
		SettingsOperator = GetNode<SettingsOperator>("/root/SettingsOperator");
		debugsong = new Label();
		AddChild(debugsong);
		debugsong.Text = "SongSelectlkjadfsjldjladfjldaadadkjladkjladskjadskjldsa";
		debugsong.SetPosition((new Vector2(10, 180)));
		debugsong.Text = "";
		debugsong.Name = "debugsong";
		foreach (string song in SettingsOperator.Beatmaps)
		{
			debugsong.Text += song + "\n";
		}
		//AnimationPlayer AniPlayer = GetNode<AnimationPlayer>("AnimationPlayer");
		//AniPlayer.Play("RESET");
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}
	
	private void _on_animation_player_animation_finished(){

	}
	private void _Start(){
		//AnimationPlayer AniPlayer = GetNode<AnimationPlayer>("AnimationPlayer");
		//AniPlayer.Play("SongSelect-Start");
	}
	private void _on_back_pressed(){
		GetTree().ChangeSceneToFile("res://Panels/Screens/home_screen.tscn");
	}
}
