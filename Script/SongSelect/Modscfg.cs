using Godot;
using System;

public partial class Modscfg : Button
{
	public override void _Ready()
	{
        ButtonPressed = ModsOperator.Mods[GetMeta("ModName").ToString()];
	}
    private void DisabledButton(){
        Disabled = true;
        ModsOperator.Mods[GetMeta("ModName").ToString()] = !Disabled;
    }
	public override void _Process(double _delta)
	{
        if (ModsOperator.Mods["dt"] && GetMeta("ModName").ToString() == "ht")
        {
            DisabledButton();
        } else if (ModsOperator.Mods["ht"] && GetMeta("ModName").ToString() == "dt"){
            DisabledButton();
        }
        else{
            Disabled = false;
        }
        _Ready();
	}
    private void _modpressed(){
        ModsOperator.Mods[GetMeta("ModName").ToString()] = ButtonPressed;
        ModsOperator.Refresh();
    }
}
