using Godot;
using System;
using System.Text;

public partial class Kiko : Node
{
    private HttpRequest KikoApi { get; set; }
    private string VersionCode { get; set; }
    public override void _Ready()
    {
        KikoApi = new HttpRequest();
        AddChild(KikoApi);
        KikoApi.Connect("request_completed", new Callable(this, nameof(_KikoApiDone)));
        KikoApi.Request("https://github.com/Jinkkuu/Qlute/releases/latest/download/RELEASE");
    }
    private void _KikoApiDone(long result, long responseCode, string[] headers, byte[] body)
    {
        if (responseCode == 200)
        {
            VersionCode = Encoding.UTF8.GetString(body);
            Notify.Post("New update is avaliable!\n" + VersionCode + " is available, click to view.",uri: "https://jinkku.itch.io/qlute");
        }
        else
        {
            GD.PrintErr("Kiko failed with response code: " + responseCode);
        }
    }
}
