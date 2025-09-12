using Godot;
using System;

public partial class HitCounter : RichTextLabel
{
    public override void _Process(double delta)
    {
        Text = $"[color=#00B5FF]{SettingsOperator.Gameplaycfg.Max}[/color]\n[color=#00ff08]{SettingsOperator.Gameplaycfg.Great}[/color]\n[color=#FFA800]{SettingsOperator.Gameplaycfg.Meh}[/color]\n[color=#FF4700]{SettingsOperator.Gameplaycfg.Bad}[/color]";
    }
}
