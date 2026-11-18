using Godot;
using System;

/// <summary>
/// One notification card: icon, a bold title and a description under it. Used for the popups (top right, they go
/// away by themselves) and for the list in the notification panel.
/// Progress cards show a bar until their work completes or fails; a failed one turns into a warning with a close button.
/// </summary>
public partial class NotificationApplet : Button
{
	private const float MinHeight = 100;
	private const float PadTop = 15, PadBottom = 15;

	private int ID { get; set; }
	private ProgressBar ProgressBar { get; set; }
	private Button Close { get; set; }
	private new NotificationLegend Notification { get; set; }
	private TextureRect IconRect;
	private Label TitleLabel, DescriptionLabel;
	private HBoxContainer Body;
	private bool IsPopup;
	private bool Closing;
	private int SeenVersion = -1;
	private bool SeenEnded;
	/// <summary>Seconds this popup has been readable (not counted while the mouse is over it).</summary>
	private double Life;

	public override void _Ready()
	{
		IsPopup = HasMeta("is_popup") && (bool)GetMeta("is_popup");
		ID = HasMeta("listid") ? (int)GetMeta("listid") : -1;
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
		BuildBody();
		Pressed += _pressed;
		if (IsPopup) ZIndex = 4096;
		Refresh();
	}

	/// <summary>The scene's Button only had room for one line of text: draw icon + title + description ourselves.</summary>
	private void BuildBody()
	{
		Text = "";
		Icon = null;
		Body = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore, Name = "Body" };
		Body.AddThemeConstantOverride("separation", 28);
		Body.SetAnchorsPreset(LayoutPreset.FullRect);
		Body.OffsetLeft = 18.75f;
		Body.OffsetTop = PadTop;
		Body.OffsetRight = -56.25f; // room for the close button
		Body.OffsetBottom = -PadBottom;
		AddChild(Body);
		MoveChild(Body, 0);

		IconRect = new TextureRect
		{
			MouseFilter = MouseFilterEnum.Ignore,
			CustomMinimumSize = new Vector2(37.5f, 37.5f),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
		};
		Body.AddChild(IconRect);

		var text = new VBoxContainer
		{
			MouseFilter = MouseFilterEnum.Ignore,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
		};
		text.AddThemeConstantOverride("separation", 3);
		Body.AddChild(text);

		TitleLabel = new Label { MouseFilter = MouseFilterEnum.Ignore, AutowrapMode = TextServer.AutowrapMode.WordSmart };
		TitleLabel.AddThemeFontSizeOverride("font_size", 18);
		TitleLabel.AddThemeColorOverride("font_color", Colors.White);
		TitleLabel.AddThemeConstantOverride("outline_size", 1);
		TitleLabel.AddThemeColorOverride("font_outline_color", Colors.White); // faux bold
		text.AddChild(TitleLabel);

		DescriptionLabel = new Label { MouseFilter = MouseFilterEnum.Ignore, AutowrapMode = TextServer.AutowrapMode.WordSmart };
		DescriptionLabel.AddThemeFontSizeOverride("font_size", 15);
		DescriptionLabel.AddThemeColorOverride("font_color", new Color(1, 1, 1, 0.75f));
		DescriptionLabel.MaxLinesVisible = 6;
		DescriptionLabel.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
		text.AddChild(DescriptionLabel);
	}

	/// <summary>Shows the notification's current title / description / icon / state.</summary>
	private void Refresh()
	{
		SeenVersion = Notification.Version;
		TitleLabel.Text = Notification.Title ?? "";
		DescriptionLabel.Text = Notification.Text ?? "";
		DescriptionLabel.Visible = !string.IsNullOrEmpty(Notification.Text);
		IconRect.Texture = NotificationIcons.GetIcon(Notification.type);
		bool running = Notification.ShowProgress && !Notification.Ended;
		Close.Visible = !running; // a running download can't be dismissed, a failed one can
		TooltipText = string.IsNullOrEmpty(Notification.Text) ? Notification.Title : $"{Notification.Title}\n{Notification.Text}";
		if (Notification.State == NotificationLegend.States.Failed) SelfModulate = new Color(1f, 0.85f, 0.85f);
	}

	/// <summary>How long a popup stays up: longer for longer text and for failures.</summary>
	private double Lifetime()
	{
		int chars = (Notification.Title?.Length ?? 0) + (Notification.Text?.Length ?? 0);
		double read = 4 + Math.Min(6, chars / 40.0);
		if (Notification.State == NotificationLegend.States.Failed) return Math.Max(8, read);
		if (Notification.ShowProgress && Notification.State == NotificationLegend.States.Done) return 2.5;
		return read;
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
		if (Closing) return;
		Closing = true;
		NotificationListener.Count--; // Removes the card from the list and updates the count
		var tween = CreateTween();

		if (IsPopup)
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
		if (Notification == null || Closing) return;
		if (Notification.ShowProgress && !Notification.Ended) return; // still working: nothing to open yet
		var uri = Notification.uri;
		if (!string.IsNullOrEmpty(uri))
		{
			if (SafeLink.IsHttps(uri))
			{
				OS.ShellOpen(uri);
			}
			else if (uri.StartsWith("updatefile://"))
			{
				UpdateInstallScreen InstallNotice = GD.Load<PackedScene>("res://Panels/Overlays/UpdateInstallScreen.tscn").Instantiate().GetNode<UpdateInstallScreen>(".");
				GetTree().CurrentScene.AddChild(InstallNotice);
			}
			else if (!Notification.FromServer && (uri.StartsWith("file://") || System.IO.Directory.Exists(uri)))
			{
				// Our own links to a local folder (screenshots, exported skins). Never for links from the server.
				OS.ShellOpen(uri.StartsWith("file://") ? uri : "file://" + uri);
			}
		}
		var onClick = Notification.OnClick;
		Notification.OnClick = null;
		try { onClick?.Invoke(); }
		catch (Exception e) { GD.PrintErr($"[Notify] click handler failed: {e.Message}"); }
		_remove();
		CloseOut();
	}
	/// <summary>Most popups on screen at once; the oldest finished ones make room (a batch import used to fill the screen).</summary>
	public const int MaxPopups = 5;

	public static void TrimPopups()
	{
		int open = 0;
		foreach (var card in NotificationListener.NotificationCards)
			if (card is NotificationApplet a && IsInstanceValid(a) && !a.Closing) open++;
		foreach (var card in NotificationListener.NotificationCards.ToArray())
		{
			if (open <= MaxPopups) break;
			if (card is not NotificationApplet a || !IsInstanceValid(a) || a.Closing || a.Notification == null) continue;
			if (a.Notification.ShowProgress && !a.Notification.Ended) continue; // running work stays visible
			a.CloseOut();
			open--;
		}
	}

	public const float StackTop = 75;
	public const float StackGap = 12.5f;

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

	public override void _Process(double delta)
	{
		if (Notification == null) return;

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

		if (SeenVersion != Notification.Version) Refresh();

		// Grow with the text (title + wrapped description).
		float want = Mathf.Max(MinHeight, Body.GetCombinedMinimumSize().Y + PadTop + PadBottom);
		if (!Mathf.IsEqualApprox(CustomMinimumSize.Y, want))
		{
			CustomMinimumSize = new Vector2(CustomMinimumSize.X, want);
			if (IsPopup) Size = new Vector2(Size.X, want);
		}

		if (Notification.ShowProgress)
		{
			int max = Notification.MaxProgress, value = Notification.Progress;
			ProgressBar.MaxValue = Math.Max(1, max);
			ProgressBar.Value = value;
			ProgressBar.Visible = !Notification.Ended;
			// Old-style progress (no Complete call): done once the bar is full. Unknown size (-1) waits for Complete.
			if (Notification.AutoComplete && !Notification.Ended && max > 0 && value >= max) Notification.Complete();
		}
		else ProgressBar.Visible = false;

		if (!IsPopup || Closing) return;
		if (Notification.ShowProgress && !Notification.Ended) return; // stays until the work ends
		if (Notification.Ended != SeenEnded)
		{
			SeenEnded = Notification.Ended;
			Life = 0; // give the finished / failed message its full time on screen
		}
		if (!GetGlobalRect().HasPoint(GetGlobalMousePosition())) Life += delta; // hovering keeps it up so it can be read
		if (Life > Lifetime()) CloseOut();
	}
}
