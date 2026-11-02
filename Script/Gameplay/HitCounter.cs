using Godot;
using System;

public partial class HitCounter : RichTextLabel
{
    private int _max = -1, _great = -1, _meh = -1, _bad = -1;
    public override void _Process(double delta)
    {
        var c = (SettingsOperator.Gameplaycfg.Max, SettingsOperator.Gameplaycfg.Great, SettingsOperator.Gameplaycfg.Meh, SettingsOperator.Gameplaycfg.Bad);
        if (c == (_max, _great, _meh, _bad)) return; // avoid re-formatting BBCode every frame
        (_max, _great, _meh, _bad) = c;
        Text = $"[color=#00B5FF]{_max}[/color]\n[color=#00ff08]{_great}[/color]\n[color=#FFA800]{_meh}[/color]\n[color=#FF4700]{_bad}[/color]";
    }
}
