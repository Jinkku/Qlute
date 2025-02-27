using Godot;
using System;
using System.Linq;

public partial class NotificationApplet : Button
{
	// Called when the node enters the scene tree for the first time.
	private bool isinit {get;set;}
	public override void _Ready()
	{
	}

	private void _anifin(){
		QueueFree();
	}
	// Called every frame. 'delta' is the elapsed time since the previous frame.
	private void _pressed(){
		int inoid = 1;
		NotificationListener.NotificationCards.Remove(this);
		NotificationListener.Count = NotificationListener.NotificationCards.Count;
		var tween = CreateTween();
		tween.TweenProperty(this, "position", new Vector2(GetViewportRect().Size.X,Position.Y), 0.2f)
				.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.Out);
		tween.Connect("finished",new Callable(this,nameof(_anifin)));
		tween.Play();
		if (1==1){
			foreach (var ino in Enumerable.Range((int)GetMeta("id"),NotificationListener.Count)){
				var noteid = ino;
				if (NotificationListener.NotificationCards.Count > noteid && IsInstanceValid(NotificationListener.NotificationCards[noteid])){
				var tempcard = NotificationListener.NotificationCards[ino];
				tween = tempcard.CreateTween();
				tween.TweenProperty(tempcard, "position", new Vector2(tempcard.Position.X,60 + ((Size.Y+10) * (noteid))), 0.2f)
					.SetTrans(Tween.TransitionType.Cubic)
					.SetEase(Tween.EaseType.Out);
				tween.Play();
				NotificationListener.NotificationList[noteid].id = noteid - 1;
				inoid++;
				tempcard.SetMeta("id",ino);
				};
				}
			}
		//NotificationListener.NotificationList.Remove((int)GetMeta("id"));
	}
	public override void _Process(double delta)
	{
		if (DateTimeOffset.UtcNow.ToUnixTimeSeconds()-(long)GetMeta("time") >3 && !isinit){
			isinit = true;
			_pressed();
		}
	}
}
