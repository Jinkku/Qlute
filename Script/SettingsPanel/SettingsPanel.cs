using Godot;
using System;

public partial class SettingsPanel : Control
{
	[Signal]
	public delegate void UpdateInfoEventHandler();

	private SettingsOperator SettingsOperator { get; set; }
	public ScrollContainer Scrolls { get; set; }

	public override void _Ready()
	{
		Scrolls = GetNode<ScrollContainer>("Panels/Scroll");
	}
	private void _display()
	{
		Scrolls.GetVScrollBar().Value = GetNode<PanelContainer>("Panels/Scroll/Sections/Display").Position.Y;
	}

	private void _audio()
	{
		Scrolls.GetVScrollBar().Value = GetNode<PanelContainer>("Panels/Scroll/Sections/Audio").Position.Y;
	}
	private void _debug()
	{
		Scrolls.GetVScrollBar().Value = GetNode<PanelContainer>("Panels/Scroll/Sections/Debug").Position.Y;
	}
	private void _skinning()
	{
		Scrolls.GetVScrollBar().Value = GetNode<PanelContainer>("Panels/Scroll/Sections/Skinning").Position.Y;
	}
	private void _input()
	{
		Scrolls.GetVScrollBar().Value = GetNode<PanelContainer>("Panels/Scroll/Sections/Input").Position.Y;
	}
	public override void _Process(double delta)
	{
		Size = new Vector2(Size.X, GetViewportRect().Size.Y - (GetNode<ColorRect>("..").Position.Y + 50));
	}
	private void _SkinEditor()
	{	
		if (!Global._SkinStart) {
			var Settings = new Settings();
			Global._SkinStart = !Global._SkinStart;
			Settings.togglesettingspanel();
		}
	}
}
