using Godot;
using System;

public partial class CardFunctions : Button
{
    public async override void _Ready()
    {
        await Browse.DownloadImage(GetMeta("pic").ToString(), (ImageTexture texture) =>
        {
            GetNode<TextureRect>("SongBackgroundPreview/BackgroundPreview").Texture = texture;
        });

    }
    private void _download()
    {
        Notify.Post("Downloading "+ GetMeta("title").ToString());
    }
}
