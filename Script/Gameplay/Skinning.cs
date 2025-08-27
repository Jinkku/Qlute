using Godot;
using System;
using System.Collections.Generic;

public partial class Skinning : Resource
{
    [Export] public string Name { get; set; } = "Untitled";
    [Export] public string SkinPath { get; set; }
    [Export] public Texture2D NoteBack { get; set; }
    [Export] public Texture2D NoteFore { get; set; }
}
public static class Skin
{
    public static Skinning Element = new Skinning();
    public static List<Skinning> List = new List<Skinning>();
}