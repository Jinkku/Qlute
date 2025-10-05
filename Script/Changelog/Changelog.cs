using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using GodotRichTextHeaders;
using System.Threading.Tasks;
using System.Linq;
public partial class Changelog : Control
{
	private ColorRect Blank { get; set; }
	private Button Back { get; set; }
	private Tween Animation { get; set; }
	private VBoxContainer Rows { get; set; }
	private List<Dictionary<string, object>> ChangeLogData { get; set; }
	private HttpRequest Request { get; set; }
	private void completed(int result, int responsecode, string[] array, byte[] body)
	{
		if (responsecode == 200)
		{
			ChangeLogData = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(System.Text.Encoding.UTF8.GetString(body));
			LoadChangeLog();
		}
	}

	private void LoadChangeLog()
	{
		foreach (var col in ChangeLogData)
		{
			var Body = new RichTextLabel();
			Body.FitContent = true;
			Body.BbcodeEnabled = true;
			Body.Text = MdHeadersToBbcode.Convert($"[color=#4fbfff][font_size=24]{col["tag_name"]}[/font_size][/color]\n[font_size=14]Released on {col["published_at"].ToString().Split("T").First()}[/font_size]\n\n{col["body"]}");
			Body.SizeFlagsVertical = SizeFlags.ExpandFill;
			Body.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			Rows.AddChild(Body);
		}
	}

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		Animation = CreateTween();
		//Back = GetNode<Button>("Back");
		Blank = new ColorRect();
		Blank.Color = new Color(0, 0, 0, 0.5f);
		Blank.AnchorLeft = 0;
		Blank.AnchorTop = 0;
		Blank.AnchorRight = 1;
		Blank.AnchorBottom = 1;
		Blank.Modulate = new Color(0, 0, 0, 0f);
		Blank.Name = "Blank-Chan";
		var Button = new Button();
		Button.AnchorLeft = 0;
		Button.AnchorTop = 0;
		Button.AnchorRight = 1;
		Button.AnchorBottom = 1;
		Button.Pressed += _close;
		Button.Modulate = new Color(0f, 0f, 0f, 0f);
		Blank.AddChild(Button);
		GetTree().CurrentScene.AddChild(Blank);
		GetTree().CurrentScene.MoveChild(Blank, -1);
		GetTree().CurrentScene.MoveChild(this, -1);
		Position = new Vector2(0, GetViewportRect().Size.Y);
		Animation.TweenInterval(2f);
		Animation.SetParallel(true);
		Animation.TweenProperty(Blank, "modulate", new Color(1f, 1f, 1f, 1f), 0.5f)
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.Out);
		Animation.TweenProperty(this, "position", new Vector2(0, SettingsOperator.TopPanelPosition), 0.5f)
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.Out);
		Animation.Play();
		Rows = GetNode<VBoxContainer>("Pill/Scroll/Rows");
		Request = GetNode<HttpRequest>("Request");
		Request.Request("https://api.github.com/repos/Jinkku/Qlute/releases");
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		if (!Animation.IsRunning())
		{
			Position = new Vector2(Position.X, SettingsOperator.TopPanelPosition);
			Size = new Vector2(Size.X, GetViewportRect().Size.Y - SettingsOperator.TopPanelPosition);
		}
	}
	private void _endit()
	{
		Blank.QueueFree();
		QueueFree();
	}
	private void _close()
	{
		Animation?.Kill();
		Animation = CreateTween();
		Animation.Connect("finished", new Callable(this, nameof(_endit)));
		Animation.SetParallel(true);
		Animation.TweenProperty(Blank, "modulate", new Color(1f, 1f, 1f, 0f), 0.5f)
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.Out);
		Animation.TweenProperty(this, "position", new Vector2(0, GetViewportRect().Size.Y), 0.5f)
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.Out);
		Animation.Play();
	}
}
