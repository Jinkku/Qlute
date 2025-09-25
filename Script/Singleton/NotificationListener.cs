using Godot;
using System;
using System.Collections.Generic;


static class Notify
{
    /// <summary>
    /// Post Notifications to the board.
    /// 
    /// For Progress bar this is the reference of what you should be using if you want this to actually work "ProgressGetter: () => X" where X is the value
    /// </summary>
    /// <param name="text"></param>
    /// <param name="uri"></param>
    /// <param name="ProgressGetter"></param>
    public static void Post(string text, string uri = "", Func<int> ProgressGetter = null, Func<int> Max = null)
    {
        var legend = new NotificationLegend
        {
            Time = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Title = text,
            Text = "a",
            uri = uri,
            id = NotificationListener.NotificationList.Count
        };
        if (ProgressGetter != null)
        {
            legend.ProgressGetter = ProgressGetter;
        }
        if (Max != null)
        {
            legend.MaxValueGetter = Max;
        }
        NotificationListener.NotificationList.Add(legend);
    }
}
public class NotificationLegend
{
    public long Time {get;set;}
    public string Title {get;set;}
    public string Text {get;set;}
    public string uri {get;set;}
    public Func<int> _progressGetter = () => 0;
    public Func<int> _MaxValueGetter = () => 0;

    /// <summary>
    /// This is to get the progress from the specified variable.
    /// </summary>
    /// <param name="getter"></param>
    public Func<int> ProgressGetter
    {
        set
        {
            if (value != null)
            {
                _progressGetter = value;
                ShowProgress = true;
            }
            else
            {
                _progressGetter = () => 0;
                ShowProgress = false;
            }
        }
    }
    /// <summary>
    /// Get max value
    /// </summary>
    public Func<int> MaxValueGetter
    {
        set
        {
            if (value != null)
            {
                _MaxValueGetter = value;
            }
            else
            {
                _MaxValueGetter = () => 100;
            }
        }
    }
    public int Progress => _progressGetter();
    public int MaxProgress => _MaxValueGetter();
    public bool ShowProgress { get; set; } = false;
    
    public bool Finished { get; set; }
    public int id {get;set;}
}
public partial class NotificationListener
{
    public static List<NotificationLegend> NotificationList = new List<NotificationLegend>();
    public static List<Button> NotificationCards = new List<Button>();
    public static int Count {get;set;}
}
