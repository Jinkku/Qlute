using Godot;
using System;

public partial class MusicCard : Button
{
	// Called when the node enters the scene tree for the first time.
	public static SettingsOperator SettingsOperator { get; set; }
	
	public Button self { get ;set; }
	public TextureRect Cover { get; set; }
	public Timer Wait { get; set; }
	public TextureRect Preview { get; set; }
	public int SongID { get; set; }
	public int waitt = 0;
	public override void _Ready()
	{
		SettingsOperator = GetNode<SettingsOperator>("/root/SettingsOperator");
		self = GetNode<Button>(".");
		Cover = GetTree().Root.GetNode<TextureRect>("Song Select/BeatmapBackground");
		Preview = GetNode<TextureRect>("./SongBackgroundPreview/BackgroundPreview");
		Wait = GetNode<Timer>("./Wait");
	}
// Please fix this god damn it-
	public void _on_draw(){
//		if (waitt<1){
//			waitt++;
//			Wait.Start();
//		}
	}

	public void _on_hidden(){
		//TextureRect.Texture = SettingsOperator.LoadImage(background);
	}
	private void _go(){
		Preview.Texture = SettingsOperator.LoadImage(self.GetMeta("background").ToString());
		Wait.Stop();
	}
	public void _on_pressed(){
		int songID = (int)self.GetMeta("SongID");
		SettingsOperator.SelectSongID(songID);
	}
	public override void _Process(double _delta)
	{
		if (Position.Y > -100 && Position.Y < GetViewportRect().Size.Y-200){
			Visible = true;
		}else {
			Visible = false;
		}
		Text = Position.Y.ToString();
		self.ButtonPressed = SettingsOperator.Sessioncfg["SongID"].ToString().Equals(self.GetMeta("SongID").ToString());
	}
}
