using Godot;
using System;
using System.Collections.Generic;

public static class NotificationIcons
{
    public enum NotificationType
    {
        Warning,
        VolumeMuted,
        UserOnline,
        UserOffline,
        Upload,
        Trash,
        Send,
        Report,
        NoInternet,
        Info,
        Gift,
        Download
    }
    public static Texture2D GetIcon(NotificationType type)
    {
        string path = $"res://Resources/NotificationIcons/{type}.png";
        return GD.Load<Texture2D>(path);
    }
}
static public class Notify
{
    /// <summary>
    /// Post a notification to the board: a <paramref name="text"/> title and an optional <paramref name="Description"/> under it.
    /// Without a description, a multi-line text is split: its first line is the title, the rest the description.
    ///
    /// For a progress bar pass "ProgressGetter: () => X" (X = the current value) and "Max: () => Y". Keep the returned
    /// notification and call <see cref="NotificationLegend.Complete"/> or <see cref="NotificationLegend.Fail"/> when the
    /// work ends, so the card never gets stuck (a failed download never reaches 100%).
    /// </summary>
    public static NotificationLegend Post(string text, string uri = "", Func<int> ProgressGetter = null, Func<int> Max = null,
        NotificationIcons.NotificationType Type = NotificationIcons.NotificationType.Info, string Description = null)
    {
        var (title, description) = Split(text, Description);
        var legend = new NotificationLegend
        {
            Time = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Title = title,
            Text = description,
            uri = uri,
            type = Type,
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
        return legend;
    }

    /// <summary>(title, description): an explicit description wins, else the text's first line is the title.</summary>
    public static (string title, string description) Split(string text, string description)
    {
        text = (text ?? "").Trim();
        if (description != null) return (text, description.Trim());
        int nl = text.IndexOf('\n');
        if (nl < 0) return (text, "");
        return (text[..nl].Trim(), text[(nl + 1)..].Trim());
    }
}
public class NotificationLegend
{
    public enum States { Running, Done, Failed }

    public long Time {get;set;}
    /// <summary>The bold first line of the card.</summary>
    public string Title {get;set;}
    /// <summary>The description under the title (may be empty).</summary>
    public string Text {get;set;} = "";
    public string uri {get;set;}
    public NotificationIcons.NotificationType type {get;set;}
    /// <summary>Came from the website (apiv2/notifications): its link is only opened if it's https.</summary>
    public bool FromServer {get;set;}
    /// <summary>Called once when the player clicks the card (after its link is opened).</summary>
    public Action OnClick {get;set;}
    public Func<int> _progressGetter = () => 0;
    public Func<int> _MaxValueGetter = () => 0;

    /// <summary>Running until <see cref="Complete"/> / <see cref="Fail"/> (or the progress bar fills up).</summary>
    public States State { get; private set; } = States.Running;
    public bool Ended => State != States.Running;
    /// <summary>Bumped whenever the title, text, icon or state changes, so open cards redraw.</summary>
    public int Version { get; private set; }

    /// <summary>The work finished: the card shows the new title / description for a moment, then goes away.</summary>
    public void Complete(string title = null, string description = null)
    {
        if (Ended) return;
        State = States.Done;
        if (title != null) Title = title;
        if (description != null) Text = description;
        Version++;
    }

    /// <summary>The work failed: the card turns into a warning with a close button instead of staying stuck.</summary>
    public void Fail(string title, string description = null)
    {
        if (Ended) return;
        State = States.Failed;
        Title = title ?? Title;
        Text = description ?? "";
        type = NotificationIcons.NotificationType.Warning;
        Version++;
    }

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
    // The getters usually read an HttpRequest, which is freed once the work ends: stop asking then.
    private int _lastProgress, _lastMax;
    public int Progress => Ended ? _lastProgress : (_lastProgress = SafeGet(_progressGetter, _lastProgress));
    public int MaxProgress => Ended ? _lastMax : (_lastMax = SafeGet(_MaxValueGetter, _lastMax));
    private static int SafeGet(Func<int> getter, int fallback)
    {
        try { return getter(); }
        catch (ObjectDisposedException) { return fallback; }
        catch (InvalidOperationException) { return fallback; }
    }
    public bool ShowProgress { get; set; } = false;
    /// <summary>
    /// True: done as soon as the bar is full (simple progress). Set false when the code calls Complete / Fail itself,
    /// e.g. a download that's still checked after its last byte, so a later failure isn't hidden behind "done".
    /// </summary>
    public bool AutoComplete { get; set; } = true;
    
    public bool Finished { get; set; }
    public int id {get;set;}
}
public partial class NotificationListener
{
    public static List<NotificationLegend> NotificationList = new List<NotificationLegend>();
    public static List<Button> NotificationCards = new List<Button>();
    public static int Count {get;set;}
}
