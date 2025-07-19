using Godot;
using System;
using System.Collections.Generic;


static class Notify
{
    public static void Post(string text, string uri = ""){
        NotificationListener.NotificationList.Add(new NotificationLegend{
        Time = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        Title = text,
        Text = "a",
        uri = uri,
        id = NotificationListener.NotificationList.Count}
        );
    }
}
public class NotificationLegend
{
    public long Time {get;set;}
    public string Title {get;set;}
    public string Text {get;set;}
    public string uri {get;set;}
    public bool Finished {get;set;}
    public int id {get;set;}
}
public partial class NotificationListener
{
    public static List<NotificationLegend> NotificationList = new List<NotificationLegend>();
    public static List<Button> NotificationCards = new List<Button>();
    public static int Count {get;set;}
}
