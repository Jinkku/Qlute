using Godot;
using System;

public partial class HealthBar : ProgressBar
{
    public Tween HealthBarAnimation {get;set;}
    public static int Health {get;set;}
    public int HealthProgress {get;set;}
    public static int MaxHealth = 150;

    public static void Reset(){
        Health = MaxHealth;
    }
    public static int Get(){
        return Health;
    }
    
    public static void Damage(int value) {
        if (Health-value >-1){
            Health = Health - value;
        }else {
            Health = 0;
        }
    }
    public static void Heal(int value) {
        if (Health+value < MaxHealth){
            Health = Health + value;
        }else {
            Health = MaxHealth;
        }
    }

    public void ValueChange(int health){
        if (HealthBarAnimation != null){
			HealthBarAnimation.Kill();
		}
        HealthProgress = health;
		HealthBarAnimation = CreateTween();
		HealthBarAnimation.TweenProperty(this,"value", health, 0.1f);
	    HealthBarAnimation.Play();
    }
    public override void _Ready()
    {
        ValueChange(Health);
        MaxValue = MaxHealth;
    }
    public override void _Process(double _delta)
    {
        if (Health != HealthProgress){
            ValueChange(Health);
        }
    }
}
