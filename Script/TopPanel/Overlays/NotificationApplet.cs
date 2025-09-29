using Godot;
using System;
using System.Linq;

public partial class NotificationApplet : Button
{
	private bool isinit {get;set;}
	private bool ProgressShow { get; set; }
	private int ID { get; set; }
	private ProgressBar ProgressBar { get; set; }
	private Button Close { get; set; }
	private int startpos { get; set; }
	private void _remove()
	{
		try
		{
			NotificationListener.NotificationList.RemoveAt(ID);
		}
		catch
		{
			GD.Print("[Qlute] Either this has already been removed, or it just doesn't exist- (this can be ignored, this just means this notification applet had already been deleted from the game :>)");
		}
	}
	private void _anifin(){
		NotificationListener.NotificationCards.Remove(this);
		QueueFree();
	}
	private void CloseOut()
	{
		NotificationListener.Count--; // Removes the card from the list and updates the count
		var tween = CreateTween();

		if (HasMeta("is_popup") && (bool)GetMeta("is_popup")) // To do: FIX THIS SHIT
		{
			tween.TweenProperty(this, "position:x", GetViewportRect().Size.X, 0.2f)
					.SetTrans(Tween.TransitionType.Cubic)
				.SetEase(Tween.EaseType.Out);
			tween.Connect("finished", new Callable(this, nameof(_anifin)));
			tween.Play();
		}
		else // if on Notification Panel
		{
			tween.TweenProperty(this, "modulate", new Color(0f,0f,0f,0f), 0.2f)
					.SetTrans(Tween.TransitionType.Cubic)
				.SetEase(Tween.EaseType.Out);
			tween.Connect("finished", new Callable(this, nameof(_anifin)));
			tween.Play();
		}
	}
	private void _close()
	{
		_remove();
		CloseOut();
	}
	private void _pressed()
	{
		// Check if uri is set, if so, open it
		if (NotificationListener.NotificationList.ElementAt<NotificationLegend>(ID).uri != "")
		{
			var uri = NotificationListener.NotificationList.ElementAt<NotificationLegend>(ID).uri;
			if (uri.StartsWith("http"))
			{
				OS.ShellOpen(uri);
			}
		}
		_remove();
		CloseOut();
	}

	public override void _Ready()
	{
		startpos = (int)(GetViewportRect().Size.X - Size.X);
		ID = (int)GetMeta("listid");
		Close = GetNode<Button>("Info/V/Close");
		ProgressBar = GetNode<ProgressBar>("ProgressBar");
		if (NotificationListener.NotificationList[ID].ShowProgress)
		{
			ProgressBar.Visible = true;
			Close.Visible = false;
			ProgressShow = true;
		}
		else
		{
			Pressed += _pressed;
		}

		if (HasMeta("is_popup") && (bool)GetMeta("is_popup"))
		{
			ZIndex = 4096;
		}
    }
	private bool Finished { get; set; }
	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		for (int i = 0; i < NotificationListener.NotificationCards.Count; i++)
		{
			var tempcard = NotificationListener.NotificationCards[i];
			float targetY = 60 + ((Size.Y + 10) * i);

			// Smoothly interpolate Y position if card is moving
			float currentY = tempcard.Position.Y;
			float newY = Mathf.Lerp(currentY, targetY, 0.15f); // Adjust the lerp factor for smoothness

			tempcard.Position = new Vector2(tempcard.Position.X, newY);
		}


		if (!isinit && HasMeta("is_popup") && (bool)GetMeta("is_popup") && DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - (long)GetMeta("time") > 4000 && !ProgressShow)
		{
			isinit = true;
			CloseOut();
		}
		ProgressBar.Visible = (ProgressBar.Value < ProgressBar.MaxValue && ProgressShow);
		if (ProgressShow)
		{
			ProgressBar.Value = NotificationListener.NotificationList[ID].Progress;
			ProgressBar.MaxValue = NotificationListener.NotificationList[ID].MaxProgress;
			if (NotificationListener.NotificationList[ID].MaxProgress != -1 && ProgressBar.Value > ProgressBar.MaxValue - 1 && !Finished)
			{
				CloseOut();
				Finished = true;
			}
		}
	}
}
