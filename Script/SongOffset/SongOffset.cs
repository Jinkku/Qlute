using Godot;
using System;

public partial class SongOffset : Control
{
	// Called when the node enters the scene tree for the first time.
	public int tick {get;set;}
	public override void _Ready()
	{
		AudioPlayer.Instance.Stop();
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	private void _tick(){
		if (tick+1 > 3){
			tick = 0;
		}else{
			tick++;
		}
	}
	public override void _Process(double delta)
	{
	}
}
