using Godot;
using System;

public partial class MusicDisc : TextureButton
{
    private Label SongTitle { get; set; }
    private Label SongArtist { get; set; }
    private Label SongMapper { get; set; }
    private TextureRect SongArt { get; set; }
    public override void _Ready()
    {
        SongTitle = GetNode<Label>("SongInfoDisc/SongTitle");
        SongArtist = GetNode<Label>("SongInfoDisc/SongArtist");
        SongMapper = GetNode<Label>("SongInfoDisc/SongMapper");
        SongArt = GetNode<TextureRect>("AlbumArt");

        string metadataTitle = GetMeta("songtitle").ToString();   // song title metadata
        string metadataArtist = GetMeta("songartist").ToString();  // artist metadata
        string metadataMapper = GetMeta("songmapper").ToString();  // song mapper metadata
        string metadatabackground = GetMeta("image").ToString();  // song background metadata for loading the image reference

        if (!string.IsNullOrEmpty(metadataTitle))
            SongTitle.Text = metadataTitle;
        else SongTitle.Text = "Unknown Title ;w;";
        if (!string.IsNullOrEmpty(metadataArtist))
            SongArtist.Text = metadataArtist;
        else SongArtist.Text = "Unknown Artist";
        if (!string.IsNullOrEmpty(metadataMapper))
            SongMapper.Text = metadataMapper;
        else SongMapper.Text = "Unknown Mapper";
        if (!string.IsNullOrEmpty(metadatabackground))
        SongArt.Texture = SettingsOperator.LoadImage(metadatabackground); // Load the image from the path provided in metadata
        else
        {
            GD.PrintErr("MusicDisc: No background image found for " + metadataTitle);
            SongArt.Texture = null; // Set to null if no image is found
        }
    }
}
