using Godot;
using System;

public partial class RatingCard : Label
{
	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Ready(){


	}
	public override void _Process(double delta)
	{
		Text = "Lv. " + ((int)((int)float.Parse(SettingsOperator.Beatmaps[Int32.Parse(GetNode<Button>("../../../../").GetMeta("SongID").ToString())]["levelrating"].ToString()) * ModsMulti.multiplier)) .ToString();
		//(int)float.Parse(song["levelrating"].ToString())
	}
}
