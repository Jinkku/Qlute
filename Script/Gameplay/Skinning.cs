using Godot;
using System;
using System.Collections.Generic;
using System.IO;
public partial class Skinning : Node
{

    public override void _Ready()
    {
        Skin.List.Add(new SkinningLegend
        {
            Name = "Slia (Qlute's Default skin 2025)"
        });

        if (System.IO.Directory.Exists(SettingsOperator.skinsdir))
        {
            GD.Print("Checking for skins...");
            var dirs = System.IO.Directory.GetDirectories(SettingsOperator.skinsdir);
            foreach (var skin in dirs)
            {
                GD.Print($"Found {skin}");
                Skin.LoadSkin(skin);
            }
        }
        Skin.SkinIndex = int.TryParse(SettingsOperator.GetSetting("skin")?.ToString(), out int mode) ? mode : 0;
        Skin.Element = Skin.List[Math.Max(0, Math.Min(Skin.List.Count,Skin.SkinIndex))];
    }
}

public class SkinningLegend
{
    public string Name { get; set; } = "Untitled";
    public string SkinPath { get; set; }
    public Texture2D NoteBack { get; set; } = GD.Load<Texture2D>("res://SelectableSkins/Slia/Backgroundnote.png");
    public Texture2D NoteFore { get; set; } = GD.Load<Texture2D>("res://SelectableSkins/Slia/Foregroundnote.png");
    public Color Lane1Note { get; set; } = new Color(0.20f, 0.0f, 0.20f);
    public Color Lane1NoteIdle => Lane1Note / 2;
    
    public Color Lane2Note { get; set; } = new Color(0.20f, 0.0f, 0.20f);
    public Color Lane3Note { get; set; } = new Color(0.20f, 0.0f, 0.20f);
    public Color Lane4Note { get; set; } = new Color(0.20f, 0.0f, 0.20f);
}
public class Skin
{
    private static string FindFile(string dir, string target)
    {
        try
        {
            foreach (string file in Directory.GetFiles(dir))
            {
                if (Path.GetFileName(file).Equals(target, StringComparison.OrdinalIgnoreCase))
                {
                    return file;
                }
            }

            foreach (string subDir in Directory.GetDirectories(dir))
            {
                string result = FindFile(subDir, target);
                if (result != null)
                    return result;
            }
        }
        catch (UnauthorizedAccessException)
        {
            // skip folders without perms
        }
        catch (PathTooLongException)
        {
            // skip cursed long paths
        }

        return null;
    }
    public static SkinningLegend Element = new SkinningLegend();
    public static List<SkinningLegend> List = new List<SkinningLegend>();
    public static int SkinIndex { get; set; }

    public static void LoadSkin(string path)
    {
        SkinningLegend PreElement = new SkinningLegend();
        PreElement.Name = Path.GetFileName(path);
        PreElement.SkinPath = path;
        PreElement.NoteBack = SettingsOperator.LoadImage(FindFile(path, "Backgroundnote.png")) ?? new SkinningLegend().NoteBack;
        PreElement.NoteFore = SettingsOperator.LoadImage(FindFile(path, "Foregroundnote.png")) ?? new SkinningLegend().NoteFore;
        List.Add(PreElement);
    }
}