using Godot;
using System;
using System.Collections.Generic;

public partial class Playfield : Control
{
	[Export] public bool ShowKeycode {get; set;}
	private HBoxContainer ChartSections { get; set; }
	private Timer KeycodeTimer { get; set; }
	private Tween tween { get; set; }
	private List<Label> KeyLabels { get; set; }
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		
		KeycodeTimer = GetNode<Timer>("KeyHide");
		KeycodeTimer.Timeout += () => StartDetonation();
		ChartSections = GetNode<HBoxContainer>("ChartSections");
		KeyLabels = new List<Label>();
		if (ShowKeycode)
		{
			tween = CreateTween();
			tween.SetEase(Tween.EaseType.Out);
			tween.SetTrans(Tween.TransitionType.Cubic);
			tween.SetParallel(true);
			var id = 0;
			foreach (var key in ChartSections.GetChildren())
			{
				var Label = new Label();
				Label.Name = "KeyCode";
				ChartSections.GetNode<Control>($"Section{id + 1}/Control").AddChild(Label);
				Label.HorizontalAlignment = HorizontalAlignment.Center;
				Label.VerticalAlignment = VerticalAlignment.Center;
				Label.AnchorLeft = 0.5f;
				Label.AnchorTop = 1f;
				Label.AnchorRight = 0.5f;
				Label.AnchorBottom = 1f;
				Label.OffsetLeft = -50;
				Label.OffsetTop = -19;
				Label.OffsetRight = 50;
				Label.OffsetBottom = 0;
				Label.GrowHorizontal = GrowDirection.Both;
				Label.GrowVertical = GrowDirection.Begin;

				Label.Position = new Vector2(Label.Position.X, Label.Position.Y - 100);
				Label.Text = SettingsOperator.GetSetting($"Key{id}").ToString();
				tween.TweenProperty(Label, "self_modulate:a", 0, 0.5f).SetDelay(1);
				KeyLabels.Add(Label);
				id++;
			}
			tween.Finished += () => StartDetonation();
			
		}
		
	}

	private void StartDetonation()
	{
		foreach (var Key in KeyLabels) 
		{
			Key.QueueFree();
		}
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}
}
