using Godot;
using System;
using System.IO;
public partial class DownloadProcess : Button
{
    private void _download()
    {
        Notify.Post("Downloading " + GetMeta("beatmap").ToString());
        DownloadBeatmap(int.Parse(GetMeta("beatmap").ToString()));
    }
    HttpRequest downloadRequest { get; set; }
    public void DownloadBeatmap(int id)
    {
        downloadRequest = new HttpRequest();
        downloadRequest.Timeout = 60;
        GetTree().Root.AddChild(downloadRequest);
        downloadRequest.Connect("request_completed", new Callable(this, nameof(_on_download_completed)));
        string url = ApiOperator.Beatmapapi + "/d/" + id;
        GD.Print("Downloading beatmap from: " + url);
        downloadRequest.DownloadFile = Path.Combine(SettingsOperator.downloadsdir, $"{id}.osz.tmp");
        downloadRequest.Request(url, null, Godot.HttpClient.Method.Get);
    }

    private void _on_download_completed(long result, long responseCode, string[] headers, byte[] body)
    {
        if (responseCode == 200)
        {
            try
            {
                int id = int.Parse(GetMeta("beatmap").ToString()); // Assuming ID is passed in headers
                string tempFilePath = Path.Combine(SettingsOperator.downloadsdir, $"{id}.osz.tmp");
                string finalFilePath = Path.Combine(SettingsOperator.downloadsdir, $"{id}.osz");

                if (File.Exists(tempFilePath))
                {
                    File.Move(tempFilePath, finalFilePath);
                }

                GD.Print($"Beatmap downloaded successfully to: {finalFilePath}");
                Notify.Post($"Beatmap downloaded successfully to: {finalFilePath}");
            }
            catch (Exception ex)
            {
                Notify.Post($"Error saving beatmap: {ex.Message}");
            }
        }
        else
        {
            Notify.Post($"Failed to download beatmap. HTTP Status: {responseCode}. You might be throttled ;w;");
        }
    }
}