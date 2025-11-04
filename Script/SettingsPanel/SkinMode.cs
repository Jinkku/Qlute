using Godot;
using System;

public partial class SkinMode : OptionButton
{
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		foreach (var skin in Skin.List)
		{
			AddItem(skin.Name);
		}
		Selected = Skin.SkinIndex;
	}
	/// <summary>
	/// Loads the skin specified by the index (Used when opening settings.)
	/// </summary>
	/// <param name="index"></param>
	private void _LoadSkin(int index)
	{
		Skin.Element = Skin.List[index];
		SettingsOperator.SetSetting("skin", Skin.Element.SkinPath);
		Skin.SkinIndex = index;
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}
}
