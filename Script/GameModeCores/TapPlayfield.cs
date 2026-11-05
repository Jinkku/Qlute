using Godot;

/// <summary>
/// Full-screen canvas for the Tap! gamemode.
/// Bodies holds the slider body nodes (one CanvasGroup per slider); Overlay is drawn on top of them
/// by TapGameCore.DrawField (circles, slider heads/balls, spinners, popups).
/// </summary>
public partial class TapPlayfield : Control
{
	public TapGameCore Core { get; set; }
	public Node2D Bodies { get; private set; }
	public Control Overlay { get; private set; }

	public override void _Ready()
	{
		// SetAnchorsPreset alone keeps the current (empty) rect; offsets must be reset too.
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		MouseFilter = MouseFilterEnum.Ignore;

		Bodies = new Node2D { Name = "SliderBodies" };
		AddChild(Bodies);

		Overlay = new Control { Name = "Overlay", MouseFilter = MouseFilterEnum.Ignore };
		AddChild(Overlay);
		Overlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		Overlay.Draw += () => Core?.DrawField(Overlay);
	}

	public override void _Process(double delta) => Overlay.QueueRedraw();
}
