using Godot;
using System;

public partial class Modscfg : Button
{
    private Label _modTitle;
    private Label _modDesc;
	public override void _Ready()
    {
        _modTitle = GetNode<Label>("Margin/Pill/Title");
        _modDesc = GetNode<Label>("Margin/Pill/Desc");
        if (HasMeta("Title"))
        {
            _modTitle.Text = GetMeta("Title").ToString();
        }
        if (HasMeta("Desc"))
        {
            _modDesc.Text = GetMeta("Desc").ToString();
        }
        ButtonPressed = ModsOperator.Mods[GetMeta("ModName").ToString()];
    }
    private void DisabledButton(){
        Disabled = true;
        ModsOperator.Mods[GetMeta("ModName").ToString()] = !Disabled;
    }

    public override void _PhysicsProcess(double _delta)
	{
        // This makes it so that if one of the mods don't conflict with one another
        if (ModsOperator.Mods["dt"] && GetMeta("ModName").ToString() == "ht")
        {
            DisabledButton();
        } else if (ModsOperator.Mods["ht"] && GetMeta("ModName").ToString() == "dt"){
            DisabledButton();
        } else if (ModsOperator.Mods["slice"] && GetMeta("ModName").ToString() == "black-out")
        {
            DisabledButton();
        } else if (ModsOperator.Mods["black-out"] && GetMeta("ModName").ToString() == "slice"){
            DisabledButton();
        }else if (ModsOperator.Mods["no-fail"] && GetMeta("ModName").ToString() == "auto")
        {
            DisabledButton();
        } else if (ModsOperator.Mods["auto"] && GetMeta("ModName").ToString() == "no-fail"){
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
