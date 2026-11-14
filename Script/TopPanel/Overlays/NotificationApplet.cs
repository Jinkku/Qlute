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
	private NotificationLegend Notification { get; set; }
	public override void _Ready()
	{
		startpos = (int)(GetViewportRect().Size.X - Size.X);
		ID = (int)GetMeta("listid");
		Close = GetNode<Button>("Info/V/Close");
		ProgressBar = GetNode<ProgressBar>("ProgressBar");
		// "listid" is only valid right now: removing other notifications shifts the list,
		// so keep a reference to our notification and never look it up by index again.
		if (ID < 0 || ID >= NotificationListener.NotificationList.Count)
		{
			QueueFree();
			return;
		}
		Notification = NotificationListener.NotificationList[ID];
		if (Notification.type != NotificationIcons.NotificationType.Info)
			Icon = NotificationIcons.GetIcon(Notification.type);
		if (Notification.ShowProgress)
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
	private void _remove()
	{
		// Remove by reference: an index would point at the wrong notification once others were removed.
		// (Already gone, e.g. after "Clear all", is fine.)
		NotificationListener.NotificationList.Remove(Notification);
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
			tween.SetParallel(true);
			tween.TweenProperty(this, "position:x", GetViewportRect().Size.X, 0.4f)
					.SetTrans(Tween.TransitionType.Cubic)
				.SetEase(Tween.EaseType.Out);
			tween.TweenProperty(this, "modulate", new Color(1f, 1f, 1f, 0f), 0.4f)
				.SetTrans(Tween.TransitionType.Cubic)
				.SetEase(Tween.EaseType.Out);
			tween.Connect("finished", new Callable(this, nameof(_anifin)));
			tween.Play();
		}
		else // if on Notification Panel
		{
			tween.TweenProperty(this, "modulate", new Color(0f,0f,0f,0f), 0.4f)
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
		var uri = Notification?.uri;
		if (!string.IsNullOrEmpty(uri))
		{
			if (uri.StartsWith("http"))
			{
				OS.ShellOpen(uri);
			} else if (uri.StartsWith("updatefile://"))
			{
				UpdateInstallScreen InstallNotice = GD.Load<PackedScene>("res://Panels/Overlays/UpdateInstallScreen.tscn").Instantiate().GetNode<UpdateInstallScreen>(".");
				GetTree().CurrentScene.AddChild(InstallNotice);
			}
		}
		_remove();
		CloseOut();
	}
	public const float StackTop = 60;
	public const float StackGap = 10;

	/// <summary>Height a card takes in the popup stack (its wrapped text can make it taller than the scene's 80px).</summary>
	public static float CardHeight(Control card) => Mathf.Max(card.Size.Y, card.GetCombinedMinimumSize().Y);

	/// <summary>Y just below every card already in the popup stack, where a new card goes.</summary>
	public static float NextStackY()
	{
		float y = StackTop;
		foreach (var card in NotificationListener.NotificationCards)
			if (IsInstanceValid(card)) y += CardHeight(card) + StackGap;
		return y;
	}
	private bool Finished { get; set; }
	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		// Stack cards under each other using each card's own height: long messages wrap onto more lines
		// and make their card taller, so a fixed slot height made the next card overlap them.
		float targetY = StackTop;
		foreach (var tempcard in NotificationListener.NotificationCards)
		{
			if (!IsInstanceValid(tempcard)) continue;
			// Smoothly interpolate Y position if card is moving
			float newY = Mathf.Lerp(tempcard.Position.Y, targetY, 0.15f); // Adjust the lerp factor for smoothness
			tempcard.Position = new Vector2(tempcard.Position.X, newY);
			targetY += CardHeight(tempcard) + StackGap;
		}

		if (!isinit && HasMeta("is_popup") && (bool)GetMeta("is_popup") && DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - (long)GetMeta("time") > 4000 && !ProgressShow)
		{
			isinit = true;
			CloseOut();
		}
		ProgressBar.Visible = (ProgressBar.Value < ProgressBar.MaxValue && ProgressShow);
		if (ProgressShow)
		{
			ProgressBar.Value = Notification.Progress;
			ProgressBar.MaxValue = Notification.MaxProgress;
			if (Notification.MaxProgress != -1 && ProgressBar.Value > ProgressBar.MaxValue - 1 && !Finished)
			{
				CloseOut();
				Finished = true;
			}
		}
	}
}
