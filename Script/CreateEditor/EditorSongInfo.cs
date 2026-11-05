using Godot;
namespace CreateEditor;
/// <summary>
/// Hand-off state for the beatmap editor (Create.tscn): which file to open, and
/// where to resume after a test play.
/// </summary>
public class EditorSongInfo
{
    public static string SongTitle { get; set; } = "Untitled Title";
    public static string SongArtist { get; set; } = "Untitled Artist";
    public static string SongDifficulty { get; set; } = "Untitled Beatmap";
    public static int SongLvRating { get; set; } = 0;
    public static string SongMapper { get; set; } = "Unknown Mapper";
    /// <summary>The .osu file to open. Null = start a new beatmap.</summary>
    public static string FilePath { get; set; } = null;
    public static Texture2D Background { get; set; } = SettingsOperator.GetNullImage();

    /// <summary>Set while test-playing from the editor so leaving gameplay returns to the editor.</summary>
    public static bool ReturnToEditor { get; set; } = false;
    /// <summary>Editor time (ms) to restore when the editor opens again.</summary>
    public static double ResumeTimeMs { get; set; } = 0;

    public static void Reset()
    {
        SongTitle = "Untitled Title";
        SongArtist = "Untitled Artist";
        SongDifficulty = "Untitled Beatmap";
        SongLvRating = 0;
        SongMapper = "Unknown Mapper";
        FilePath = null;
        Background = SettingsOperator.GetNullImage();
        ResumeTimeMs = 0;
    }

    /// <summary>Point the editor at an existing beatmap from the song list.</summary>
    public static void LoadFromBeatmap(BeatmapLegend beatmap)
    {
        Reset();
        if (beatmap == null) return;
        SongTitle = beatmap.Title;
        SongArtist = beatmap.Artist;
        SongDifficulty = beatmap.Version;
        SongMapper = beatmap.Mapper;
        FilePath = beatmap.Rawurl;
    }
}
