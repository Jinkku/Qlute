using Godot;
using System;
using System.Collections.Generic;


static class Notify
{
    public static void Post(string text){
        GD.Print(NotificationListener.NotificationList.Count);
        NotificationListener.NotificationList.Add(new NotificationLegend{
        Time = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        Title = text,
        Text = "a",
        id = NotificationListener.NotificationList.Count}
        );
    }
}
public class NotificationLegend
{
    public long Time {get;set;}
    public string Title {get;set;}
    public string Text {get;set;}
    public bool Finished {get;set;}
    public int id {get;set;}
}
public partial class NotificationListener : CanvasLayer
{
    public static List<NotificationLegend> NotificationList = new List<NotificationLegend>();
    public static List<Button> NotificationCards = new List<Button>();
    public static int Count {get;set;}
    public override void _Process(double delta)
    {
    }
}
