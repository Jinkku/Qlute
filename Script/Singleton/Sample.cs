using Godot;
using System;

public partial class Sample : AudioStreamPlayer
{
    public static AudioStreamPlayer Instance;
    public override void _Ready()
    {
		Instance = this;
		Bus = "Effects";
    }
}
