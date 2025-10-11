using Godot;
using System;
using System.Threading.Tasks;

public partial class SceneTransition : Control
{
	public enum TransitionMode
		{
			None,
			FadeToBlack,
			CrossFade,
			FadeToWhite,

			SlideLeft,
			SlideRight,
			SlideUp,
			SlideDown,
		}

		[Export] private float _time = 1.0f;
		[Export] private TransitionMode _mode = TransitionMode.FadeToBlack;
		[Export] private Tween.EaseType _easeType = Tween.EaseType.Out;
		[Export] private Tween.TransitionType _transitionType = Tween.TransitionType.Linear;

		private ColorRect _black;
		private ColorRect _white;
		private TextureRect _stillRender;

		public override void _Ready() {
			_black = GetNode<ColorRect>("Black");
			_black.Modulate = new Color(0, 0, 0, 0);
			_white = GetNode<ColorRect>("White");
			_white.Modulate = new Color(0, 0, 0, 0);
			_stillRender = GetNode<TextureRect>("StillRender");
			_stillRender.Modulate = new Color(1, 1, 1, 0);
		}
		public async void Switch(string sceneName, bool Fadein = true, TransitionMode mode = TransitionMode.None, float time = 0) {
			if (mode == TransitionMode.None)
			{
				mode = _mode;
			}
			if (time == 0) time = _time;
			Node oldScene = GetTree().CurrentScene;
			if (Fadein)
			{
				await _TransitionIn(sceneName,mode,time);
			}
			if (mode != TransitionMode.CrossFade)
				GetTree().ChangeSceneToFile(sceneName);
			_TransitionOut(mode, oldScene,time);
		}

		private async Task _TransitionIn(string sceneName, TransitionMode _mode, float _time) {
			if (_mode == TransitionMode.FadeToBlack) {
				Tween t = GetTree().CreateTween();
				t.TweenProperty(_black, "modulate:a", 1.0f, _time / 2f);
				await ToSignal(t, Tween.SignalName.Finished);
			} else {
				if (_mode != TransitionMode.CrossFade)
				{
					_stillRender.Texture = ImageTexture.CreateFromImage(GetViewport().GetTexture().GetImage());
					_stillRender.Modulate = new Color(1, 1, 1, 1);
					_stillRender.Position = Vector2.Zero;
				}

				Vector2 size = _stillRender.Size;
				Tween t = GetTree().CreateTween();
				switch (_mode) {
					case TransitionMode.SlideLeft:
						t.TweenProperty(_stillRender, "position:x", -size.X, _time)
							.SetEase(_easeType).SetTrans(_transitionType);
						break;
					case TransitionMode.CrossFade:
						var Scene = GD.Load<PackedScene>(sceneName).Instantiate();
						GetTree().Root.AddChild(Scene);
						GetTree().CurrentScene = Scene;
						t.TweenProperty(Scene, "modulate:a", 0f, 0f);
						t.TweenProperty(Scene, "modulate:a", 1f, _time)
							.SetEase(_easeType).SetTrans(_transitionType);
						break;
					case TransitionMode.SlideRight:
						t.TweenProperty(_stillRender, "position:x", size.X * 2f, _time)
							.SetEase(_easeType).SetTrans(_transitionType);
						break;
					case TransitionMode.SlideUp:
						t.TweenProperty(_stillRender, "position:y", -size.Y, _time)
							.SetEase(_easeType).SetTrans(_transitionType);
						break;
					case TransitionMode.SlideDown:
						t.TweenProperty(_stillRender, "position:y", size.Y * 2f, _time)
							.SetEase(_easeType).SetTrans(_transitionType);
						break;
				}
			}
		}

		private void _TransitionOut(TransitionMode _mode, Node oldScene,float _time) {
			if (
				_mode == TransitionMode.SlideLeft ||
				_mode == TransitionMode.SlideRight ||
				_mode == TransitionMode.SlideUp ||
				_mode == TransitionMode.SlideDown
			)
				return;

			Tween t = GetTree().CreateTween();
			switch (_mode) {
				case TransitionMode.FadeToBlack:
					t.TweenProperty(_black, "modulate:a", 0.0f, _time / 2f);
					break;
				case TransitionMode.FadeToWhite:
					_white.Modulate = new Color(1f, 1f, 1f, 1f);
					t.TweenProperty(_white, "modulate:a", 0.0f, _time);
					break;
				case TransitionMode.CrossFade:
					t.TweenProperty(oldScene, "modulate:a", 0.0f, _time);
					t.TweenCallback(Callable.From(() => oldScene.QueueFree()));
					break;
				default:
					break;
			}
		}
}
