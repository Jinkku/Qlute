using Godot;
using System;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using System.Net.Http;
using System.Text;
using System.Collections.Generic;
using System.IO;

public class CatalogCardLegend {
	public string cover { get; set; }
	public string card { get; set; }
	public string list { get; set; }
	public string slimcover { get; set; }
}
public class BrowseCatalogLegend {
	public int id { get; set; }
	public string artist { get; set; }
	public string title { get; set; }
	public string creator { get; set; }
	public string source { get; set; }
	public CatalogCardLegend covers { get; set; }
}
public partial class Browse : Control
{
	// Called when the node enters the scene tree for the first time.
    public static List<Dictionary<string,object>> BrowseCatalog = new List<Dictionary<string,object>>();
	public static HttpRequest BrowseApi { get; set; }
	public int CardSizeX { get; set; }
	public override void _Ready()
	{
		BrowseApi = new HttpRequest();
		BrowseApi.Timeout = 60;
		AddChild(BrowseApi);
		BrowseApi.Connect("request_completed", new Callable(this, nameof(_BrowseAPI_finished)));
		StartBrowse();
	}
	public void StartBrowse(){
		BrowseApi.Request(ApiOperator.Beatmapapi + "/api/v2/search");
	}
public static async Task
DownloadImage(string path, Action<ImageTexture> callback)
{
    try
    {
        // 1. Download the image
        using (var client = new System.Net.Http.HttpClient())
        {
            byte[] imageBytes = await client.GetByteArrayAsync(path);

            // 2. Create a Godot Image object
            Godot.Image image = new Godot.Image();
            using (var stream = new MemoryStream(imageBytes))
            {
                image.LoadJpgFromBuffer(imageBytes);
            }

            // 3. Create a texture and display the image
            ImageTexture texture = new ImageTexture();
            texture.SetImage(image);

            // Call the callback function with the texture
            callback(texture);
        }
    }
    catch (Exception e)
    {
        GD.PrintErr("Error downloading or processing image: " + e.Message);
    }
}
	private async void _BrowseAPI_finished(long result, long responseCode, string[] headers, byte[] body){
		string BrowseEntries = (string)Encoding.UTF8.GetString(body);
		List<BrowseCatalogLegend> items = JsonSerializer.Deserialize<List<BrowseCatalogLegend>>(BrowseEntries);
		try{
		foreach (BrowseCatalogLegend line in items){
			var Element = GD.Load<PackedScene>("res://Panels/BrowseElements/Card.tscn").Instantiate().GetNode<Button>(".");
			CardSizeX = (int)Element.Size.X+5;
			Element.GetNode<Label>("SongTitle").Text = line.title;
			Element.GetNode<Label>("SongArtist").Text = line.artist;
			Element.GetNode<Label>("SongMapper").Text = "mapped by " + line.creator;
			Element.SetMeta("pic",line.covers.card);
			GetNode<GridContainer>("BeatmapSec/Scroll/Center/Spacer/Beatmaps").AddChild(Element);
		}
		}catch (Exception e){
			GD.Print(e);
		}
	}
	// Called every frame. 'delta' is the elapsed time since the previous frame.
	private void _on_back(){
		GetNode<SceneTransition>("/root/Scene").Switch("res://Panels/Screens/home_screen.tscn");
	}
	public override void _Process(double delta)
	{
		var waba = (int)(GetViewportRect().Size.X/CardSizeX);
		if (waba <1){
			waba = 1;
		}
		GetNode<GridContainer>("BeatmapSec/Scroll/Center/Spacer/Beatmaps").Columns = waba;
	}
}
