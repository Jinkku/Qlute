using Godot;
using System;

public partial class TimeKeeper : Node
{
    private static long TimePaused {get;set;}
    private long TimeUnPaused {get;set;}
    private bool pausetick = false;
    public override void _Process(double delta)
    {
        if (GetTree().Paused){
            TimePaused = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - TimeUnPaused;
            pausetick = true;
        } else if (!GetTree().Paused){
            TimeUnPaused = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }
        if (!GetTree().Paused && pausetick){
            pausetick = false;
            Gameplay.PClock += TimePaused;
        }
    }
}
