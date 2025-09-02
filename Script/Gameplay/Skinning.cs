using Godot;
using System;
using System.Collections.Generic;

public partial class Skinning
{
    public string Name { get; set; } = "Untitled";
    public string SkinPath { get; set; }
    public Texture2D NoteBack { get; set; }
    public Texture2D NoteFore { get; set; }
    public Color NoteColour { get; set; }
    public Color Lane1Note { get; set; }
    public Color Lane2Note { get; set; }
    public Color Lane3Note { get; set; }
    public Color Lane4Note { get; set; }
}
public static class Skin
{
    public static Skinning Element = new Skinning();
    public static List<Skinning> List = new List<Skinning>();

    public static void LoadSkin(string path)
    {
        Texture2D NoteBack = null;
        Texture2D NoteFore = null;
        Color NoteColour = new Color(1f,1f,1f);
        Color Lane1Note = new Color(1f,1f,1f);
        Color Lane2Note = new Color(1f,1f,1f);
        Color Lane3Note = new Color(1f,1f,1f);
        Color Lane4Note = new Color(1f,1f,1f);
        if (path.StartsWith("res://"))
        {
        }
    }
}