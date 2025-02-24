using Godot;
using System;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text;
using System.Collections.Generic;

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
	public string cover { get; set; }
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
		BrowseApi.Timeout = 3;
		AddChild(BrowseApi);
		BrowseApi.Connect("request_completed", new Callable(this, nameof(_BrowseAPI_finished)));
		StartBrowse();
	}
	public void StartBrowse(){
		BrowseApi.Request(ApiOperator.Beatmapapi + "/api/v2/search");
	}
	private void _BrowseAPI_finished(long result, long responseCode, string[] headers, byte[] body){
		string BrowseEntries = (string)Encoding.UTF8.GetString(body);
		var point = 0;
		List<BrowseCatalogLegend> items = JsonSerializer.Deserialize<List<BrowseCatalogLegend>>(BrowseEntries);
		foreach (BrowseCatalogLegend line in items){
			var Element = GD.Load<PackedScene>("res://Panels/BrowseElements/Card.tscn").Instantiate().GetNode<Button>(".");
			//GD.Print(line.cover);
			//List<CatalogCardLegend> pictures = JsonSerializer.Deserialize<List<CatalogCardLegend>>(line.cover);
			CardSizeX = (int)Element.Size.X+5;
			Element.GetNode<Label>("SongTitle").Text = line.title;
			Element.GetNode<Label>("SongArtist").Text = line.artist;
			Element.GetNode<Label>("SongMapper").Text = "mapped by " + line.creator;
			//Element.GetNode<TextureRect>("SongBackgroundPreview/BackgroundPreview").Texture = SettingsOperator.LoadImage(pictures[0].card);
			GetNode<GridContainer>("BeatmapSec/Scroll/Center/Spacer/Beatmaps").AddChild(Element);
		}

	}
	// Called every frame. 'delta' is the elapsed time since the previous frame.
	private void _on_back(){
		GetTree().ChangeSceneToFile("res://Panels/Screens/home_screen.tscn");
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
