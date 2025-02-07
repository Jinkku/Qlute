using Godot;
using System;

public partial class MusicCard : Button
{
	// Called when the node enters the scene tree for the first time.
	public static SettingsOperator SettingsOperator { get; set; }
	
	public Button self { get ;set; }
	public TextureRect Cover { get; set; }
	public int SongID { get; set; }
	public override void _Ready()
	{
		SettingsOperator = GetNode<SettingsOperator>("/root/SettingsOperator");
		self = GetNode<Button>(".");
		Cover = GetTree().Root.GetNode<TextureRect>("Song Select/BeatmapBackground");
	}

	
	public void _on_pressed(){
		var TextureRect = GetNode<TextureRect>("./SongBackgroundPreview/BackgroundPreview");
		var SongTitle = GetTree().Root.GetNode<Label>("Song Select/PanelContainer/HBoxContainer/Title");
		var SongArtist = GetTree().Root.GetNode<Label>("Song Select/SongDetails/Artist");
		var Songpp = GetTree().Root.GetNode<Label>("Song Select/SongDetails/Points");
		SongTitle.Text = self.GetMeta("Title").ToString();
		SongArtist.Text = self.GetMeta("Artist").ToString();
		Songpp.Text = "+" + self.GetMeta("pp").ToString()+"pp";
		Cover.Texture = TextureRect.Texture;
		SettingsOperator.Sessioncfg["background"] = (Texture2D)TextureRect.Texture;
		SettingsOperator.Sessioncfg["SongID"] = self.GetMeta("SongID");
		SettingsOperator.Sessioncfg["beatmapurl"] = self.GetMeta("beatmapurl");
		SettingsOperator.Sessioncfg["beatmaptitle"] = self.GetMeta("Title").ToString();
		SettingsOperator.Sessioncfg["beatmapartist"] = self.GetMeta("Artist").ToString();
		SettingsOperator.Sessioncfg["beatmapdiff"] = self.GetMeta("Difficulty").ToString();
		SettingsOperator.Sessioncfg["beatmapmapper"] = self.GetMeta("Mapper").ToString();
		SettingsOperator.Sessioncfg["maxpp"] = self.GetMeta("pp");
		GD.Print(self.GetMeta("SongID"));
	}
	public override void _Process(double _delta)
	{
		self.ToggleMode = SettingsOperator.Sessioncfg["SongID"].Equals(self.GetMeta("SongID"));
	}
}
