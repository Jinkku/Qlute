using Godot;
using System;

public partial class TimeKeeper : Node
{
    private static long TimePaused {get;set;}
    private long TimeUnPaused {get;set;}
    private bool pausetick = false;
    public override void _Process(double delta)
    {
    }
}
