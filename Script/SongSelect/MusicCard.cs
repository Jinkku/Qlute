using Godot;
using System;
using System.IO;
using System.Threading.Tasks;

public partial class MusicCard : Button
{
	// Called when the node enters the scene tree for the first time.
	public static SettingsOperator SettingsOperator { get; set; }
	
	public Button self { get ;set; }
	public TextureRect Cover { get; set; }
	public Timer Wait { get; set; }
	public TextureRect Preview { get; set; }
	public int SongID { get; set; }
	public int waitt = 0;
	public static Task LoadImage(string path, Action<Texture2D> callback)
	{
		callback(SettingsOperator.LoadImage(path));
		return Task.CompletedTask;
	}
	public async override void _Ready()
	{
		SettingsOperator = GetNode<SettingsOperator>("/root/SettingsOperator");
		self = GetNode<Button>(".");
		Cover = GetTree().Root.GetNode<TextureRect>("Song Select/BeatmapBackground");
		Preview = GetNode<TextureRect>("SongBackgroundPreview/BackgroundPreview");
		GD.Print(self.GetMeta("background"));
		if (self.HasMeta("background"))
		{
			await LoadImage(self.GetMeta("background").ToString(), (Texture2D texture) =>
			{
				Preview.Texture = texture;
			});
		}
		// Check if "SongID" metadata exists, if not, set it to 0
		if (!self.HasMeta("SongID"))
		{
			self.SetMeta("SongID", 0);
		}

		if (Checkid())
		{
			SelfModulate = toggledcolour;
		}
		else
		{
			SelfModulate = Idlecolour;
		}
	}
    private  Tween _focus_animation;
    private void AnimationButton(Color colour)
    {
        if (_focus_animation != null)
        {
            _focus_animation.Kill();
        }
        _focus_animation = CreateTween();
        _focus_animation.TweenProperty(this, "self_modulate", colour, 0.2f)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.Out);
        _focus_animation.Play();
    }
	private Color Idlecolour = new Color(0.20f, 0.20f, 0.20f, 1f); // Colour when idle
	private Color Focuscolour = new Color(1f, 1f, 1f, 1f); // Colour when focused
	private Color highlightcolour = new Color(0.19f, 0.37f, 0.65f, 1f); // Colour when highlighted	
	private Color toggledcolour = new Color(0.09f, 0.38f, 0.85f, 1f); // Colour when toggled	
	private void _highlight()
	{
		AnimationButton(highlightcolour);
	}
	private void _focus()
	{
        AnimationButton(Focuscolour);
	}private void _unfocus()
	{
		if (Checkid())
		{
			AnimationButton(toggledcolour);
		}
		else
		{
			AnimationButton(Idlecolour);
		}
	}
	public void _on_pressed()
	{
		int songID = (int)self.GetMeta("SongID");
		Connection_Button = true;
		SettingsOperator.SelectSongID(songID);
	}
	private bool Accessed = false;
	public static bool Connection_Button = false;
	public bool Checkid()
	{
		return SettingsOperator.Sessioncfg["SongID"].ToString().Equals(self.GetMeta("SongID").ToString());
	}
	public override void _Process(double _delta)
	{
		if (ButtonPressed != Checkid())
		{
			ButtonPressed = Checkid();
			Accessed = true;

		}
		if (ButtonPressed == true && Accessed)
		{
			AnimationButton(toggledcolour);
			Accessed = false;
		}
		else if (ButtonPressed == false && Accessed)
		{
			AnimationButton(Idlecolour);
			Accessed = false;
		}
	}
}
