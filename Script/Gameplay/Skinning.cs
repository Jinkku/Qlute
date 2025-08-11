using Godot;
using System;
using System.Collections.Generic;

public class Skinning
{
    public string Name { get; set; } = "Untitled";
    public string SkinPath { get; set; }
    public Texture2D Note { get; set; }
}
public static class Skin
{
    public static Skinning Element = new Skinning();
    public static List<Skinning> List = new List<Skinning>();
}