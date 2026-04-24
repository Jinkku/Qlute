using Godot;
namespace CreateEditor;
/// <summary>
/// Song info of the currently editing beatmap.
/// </summary>
public class EditorSongInfo
{
    public static string SongTitle { get; set; } = "Untitled Title";
    public static string SongArtist { get; set; } = "Untitled Artist";
    public static string SongDifficulty { get; set; } = "Untitled Beatmap";
    public static int SongLvRating { get; set; } = 0;
    public static string SongMapper { get; set; } = "Unknown Mapper";
    public static string FilePath { get; set; } = null;
    public static string ParentPath { get; set; } = null;
    public static Texture2D Background { get; set; } = SettingsOperator.GetNullImage();

    public static void Reset()
    {
        SongTitle = "Untitled Title";
        SongArtist = "Untitled Artist";
        SongDifficulty = "Untitled Beatmap";
        SongLvRating = 0;
        SongMapper = "Unknown Mapper";
        FilePath = null;
        Background = SettingsOperator.GetNullImage();
    }
}
