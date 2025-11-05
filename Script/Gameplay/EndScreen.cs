using Godot;
using System;
using System.Threading.Tasks;

public partial class EndScreen : ColorRect
{
	private Tween Tween { get; set; }
	private TextureRect ComboScreen { get; set; }
	private SettingsOperator SettingsOperator { get; set; }
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		SettingsOperator = GetNode<SettingsOperator>("/root/SettingsOperator");
		Tween = CreateTween();
		Tween.Connect("finished", new Callable(this, nameof(done)));
		ComboScreen = GetNode<TextureRect>("ComboScene");
		if (SettingsOperator.Gameplaycfg.Bad == 0) FC();
		else if (SettingsOperator.Gameplaycfg.Accuracy > 0.89f) MAX();
		else if (SettingsOperator.Gameplaycfg.Accuracy > 0.69f) Good();
		else Bad();
	}
    public override void _PhysicsProcess(double delta)
    {
		ComboScreen.PivotOffset = ComboScreen.Size / 2;
    }
	private void done()
	{
		SettingsOperator.toppaneltoggle(true);
		BeatmapBackground.FlashEnable = true;
		GetNode<SceneTransition>("/root/Transition").Switch("res://Panels/Screens/ResultsScreen.tscn");
	}

	private void FC()
	{
		ComboScreen.Texture = Skin.Element.FullCombo;
		ComboScreen.Scale = new Vector2(0.8f, 0.8f);
		Modulate = new Color(0f, 0f, 0f, 0f);
		Sample.PlaySample("res://SelectableSkins/Slia/Sounds/comboclear.wav");
		Tween.Parallel().TweenProperty(this, "modulate", new Color(1f, 1f, 1f, 1f), 1f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		Tween.Parallel().TweenProperty(ComboScreen, "scale", new Vector2(1f, 1f), 1f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		Tween.TweenInterval(3);
	}
	private void Bad()
	{
		Modulate = new Color(0f, 0f, 0f, 0f);
		ComboScreen.Scale = new Vector2(0.8f, 0.8f);
		ComboScreen.Texture = Skin.Element.Bad;
		Sample.PlaySample("res://SelectableSkins/Slia/Sounds/comboclear.wav");
		Tween.Parallel().TweenProperty(this, "modulate", new Color(1f, 1f, 1f, 1f), 1f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		Tween.Parallel().TweenProperty(ComboScreen, "scale", new Vector2(1f, 1f), 1f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		Tween.TweenInterval(3);
	}
	private void Good()
	{
		Modulate = new Color(0f, 0f, 0f, 0f);
		ComboScreen.Scale = new Vector2(0.8f, 0.8f);
		ComboScreen.Texture = Skin.Element.Good;
		Sample.PlaySample("res://SelectableSkins/Slia/Sounds/comboclear.wav");
		Tween.Parallel().TweenProperty(this, "modulate", new Color(1f, 1f, 1f, 1f), 1f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		Tween.Parallel().TweenProperty(ComboScreen, "scale", new Vector2(1f, 1f), 1f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		Tween.TweenInterval(3);
	}
	private void MAX()
	{
		Modulate = new Color(0f, 0f, 0f, 0f);
		ComboScreen.Scale = new Vector2(0.8f, 0.8f);
		ComboScreen.Texture = Skin.Element.Perfect;
		Sample.PlaySample("res://SelectableSkins/Slia/Sounds/comboclear.wav");
		Tween.Parallel().TweenProperty(this, "modulate", new Color(1f, 1f, 1f, 1f), 1f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		Tween.Parallel().TweenProperty(ComboScreen, "scale", new Vector2(1f, 1f), 1f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		Tween.TweenInterval(3);
	}
}
