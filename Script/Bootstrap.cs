using Godot;
using System;
using System.Security.Cryptography.X509Certificates;

public partial class Bootstrap : Control
{
	public static SettingsOperator SettingsOperator { get; set; }
	public Control Home {get;set;}
	public override void _Ready()
	{		
		AnimationPlayer animationPlayer = GetNode<AnimationPlayer>("./AnimationPlayer");
		SettingsOperator = GetNode<SettingsOperator>("/root/SettingsOperator");
		animationPlayer.Play("Intro");
		SettingsOperator.Sessioncfg["toppanelhide"] = true;
		if (SettingsOperator.Beatmaps.Count>0) {
			SettingsOperator.SelectSongID(SettingsOperator.RndSongID());
		}
	    Home = GD.Load<PackedScene>("res://Panels/Screens/home_screen.tscn").Instantiate().GetNode<Control>(".");
		AddChild(Home);
		Home.MouseFilter = MouseFilterEnum.Stop;
		Home.SetProcessInput(false);
		Home.Modulate = new Color(0f,0f,0f,0f);
	}
	private void _anifin(){
		SettingsOperator.toppaneltoggle();
		GetTree().ChangeSceneToFile("res://Panels/Screens/home_screen.tscn");
	}
	public void _intro_finished(string animationame){
		var _tween = Home.CreateTween();
		_tween.Connect("finished",new Callable(this,nameof(_anifin)));
			_tween.TweenProperty(Home, "modulate", new Color(1f,1f,1f,1f), 0.5f)
				.SetTrans(Tween.TransitionType.Cubic)
				.SetEase(Tween.EaseType.Out);
			_tween.Play();
		
		
	}
}
