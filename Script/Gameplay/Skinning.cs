using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
public partial class Skinning : Node
{
    public override void _Ready()
    {
        GetWindow().FilesDropped += ImportSkin;
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
                GD.Print($"Queued {skin}");
                Skin.LoadSkin(skin);
            }
        }
        SkinningLegend foundEntry = Skin.List.FirstOrDefault(s => s.SkinPath == SettingsOperator.GetSetting("skin")?.ToString());
        GD.Print(SettingsOperator.GetSetting("skin")?.ToString());
        if (foundEntry != null)
        {
            Skin.SkinIndex = foundEntry.ID;
            Skin.Element = foundEntry;
            SettingsOperator.SetSetting("skin", foundEntry.SkinPath);
            GD.Print($"[Qlute] Found skin: {foundEntry.Name}");
        } else if (SettingsOperator.GetSetting("skin") == null)
        {
            Skin.SkinIndex = 0;
            Skin.Element = Skin.List[Skin.SkinIndex];
            SettingsOperator.SetSetting("skin", null);
        }
        else
        {
            Skin.SkinIndex = 0;
            Skin.Element = Skin.List[Skin.SkinIndex];
            Notify.Post($"Skin disappeared, defaulting to {Skin.List[0].Name}.");
            SettingsOperator.SetSetting("skin", null);
            GD.Print($"[Qlute] Can't find skin, defaulting to default skin");
        }
    }

    private void ImportSkin(string[] files)
    {
        foreach (string file in files)
        {
            if (file.EndsWith(".qsk"))
            {
                ZipFile.ExtractToDirectory(file, SettingsOperator.skinsdir);
                Notify.Post($"Imported skin {file}");
                using (ZipArchive archive = ZipFile.OpenRead(file))
                {
                    // find the first directory at zip root
                    string rootDir = archive.Entries
                        .Select(e => e.FullName.Split('/')[0]) // first segment before slash
                        .Where(s => !string.IsNullOrEmpty(s))
                        .Distinct()
                        .FirstOrDefault();

                    if (rootDir != null)
                        Skin.LoadSkin(SettingsOperator.skinsdir.PathJoin(rootDir));
                    else
                        GD.Print("No directory found in zip root 😬");
                }
            }
        }
    }
}

public class SkinningLegend
{
    public int ID { get; set; }
    public string Name { get; set; } = "Untitled";
    public string SkinPath { get; set; } = "res://SelectableSkins/Slia/";
    public Texture2D NoteBack { get; set; } = GD.Load<Texture2D>("res://SelectableSkins/Slia/Backgroundnote.png");
    public Texture2D NoteFore { get; set; } = GD.Load<Texture2D>("res://SelectableSkins/Slia/Foregroundnote.png");
    public Texture2D Perfect { get; set; } = GD.Load<Texture2D>("res://SelectableSkins/Slia/EndScreen/MAX.png");
    public Texture2D FullCombo { get; set; } = GD.Load<Texture2D>("res://SelectableSkins/Slia/EndScreen/FC.png");
    public Texture2D Good { get; set; } = GD.Load<Texture2D>("res://SelectableSkins/Slia/EndScreen/Good.png");
    public Texture2D Bad { get; set; } = GD.Load<Texture2D>("res://SelectableSkins/Slia/EndScreen/Bad.png");
    public List<Color> LaneNotes { get; set; } = new()
    {
        new Color("8f00ff"),
        new Color("8f00ff"),
        new Color("8f00ff"),
        new Color("8f00ff"),
    };
    public Texture2D Cursor { get; set; } = GD.Load<Texture2D>("res://SelectableSkins/Slia/System/cursor.png");
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
    public static List<string> ImageNames = new List<string>(["Backgroundnote.png","Foregroundnote.png","cursor.png","MAX.png","Good.png","Bad.png","FC.png"]);
    public static SkinningLegend ReloadSkin(string path)
    {
        SkinningLegend PreElement = new SkinningLegend();
        using var saveFile = Godot.FileAccess.Open(path.PathJoin("settings.json"), Godot.FileAccess.ModeFlags.Read) ?? null;
        if (saveFile != null)
        {
            var prejson = saveFile.GetAsText();
            var json = JsonSerializer.Deserialize<Dictionary<string, object>>(prejson);
            PreElement.Name = json["Name"].ToString();
            foreach (int i in Enumerable.Range(0, 4))
            {
                try
                {
                    string raw = json[$"NoteLane{i + 1}"]?.ToString();
                    PreElement.LaneNotes[i] = !string.IsNullOrEmpty(raw)
                        ? new Color(raw)
                        : new SkinningLegend().LaneNotes[i];
                }
                catch
                {
                    PreElement.LaneNotes[i] = new SkinningLegend().LaneNotes[i];
                }
                if (PreElement.LaneNotes[i] == new SkinningLegend().LaneNotes[i])
                {
                    GD.Print($"[Qlute] [skin] Lane{i} is not coloured?");
                }
            }
        }
        PreElement.SkinPath = path;
        PreElement.NoteBack = SettingsOperator.LoadImage(FindFile(path, "Backgroundnote.png")) ?? new SkinningLegend().NoteBack;
        PreElement.NoteFore = SettingsOperator.LoadImage(FindFile(path, "Foregroundnote.png")) ?? new SkinningLegend().NoteFore;
        PreElement.Perfect = SettingsOperator.LoadImage(FindFile(path, "MAX.png")) ?? new SkinningLegend().Perfect;
        PreElement.FullCombo = SettingsOperator.LoadImage(FindFile(path, "FC.png")) ?? new SkinningLegend().FullCombo;
        PreElement.Good = SettingsOperator.LoadImage(FindFile(path, "Good.png")) ?? new SkinningLegend().Good;
        PreElement.Bad = SettingsOperator.LoadImage(FindFile(path, "Bad.png")) ?? new SkinningLegend().Bad;
        PreElement.ID = List.Count;
        return PreElement;
    }
    public static int LoadSkin(string path)
    {
        if (!List.Any(legend => legend.SkinPath == path))
        {
            List.Add(ReloadSkin(path));
            return List.Last().ID;
        }
        return 0;
    }
}