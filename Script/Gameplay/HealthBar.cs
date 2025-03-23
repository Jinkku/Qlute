using Godot;
using System;

public partial class HealthBar : ProgressBar
{
    public Tween HealthBarAnimation {get;set;}
    public static int Health {get;set;}
    public static int MaxHealth = 150;
    public static void Reset(){
        Health = 150;
    }

    public void ValueChange(int health){
        if (HealthBarAnimation != null){
			HealthBarAnimation.Kill();
		}
		HealthBarAnimation = CreateTween();
		HealthBarAnimation.TweenProperty(this,"value", MaxHealth, 1f);
	    HealthBarAnimation.Play();
    }
    public override void _Ready()
    {
        ValueChange(MaxHealth);
    }
    public override void _Process(double _delta)
    {
        if (Value != Health){
            ValueChange(Health);
        }
    }
}
