using Godot;
using System;
using System.IO;
using System.Threading.Tasks;

public partial class MusicCard : Button
{
	private SettingsOperator SettingsOperator { get; set; }

	public Button self { get; set; }
	public TextureRect Cover { get; set; }
	public Timer Wait { get; set; }
	public TextureRect Preview { get; set; }
	private int SongID { get; set; }
	public int waitt = 0;
	private bool isLoadingImage = false;
	private string BackgroundPath = null;
	private Texture2D texture { get; set; }
	private Label Title { get; set; }
	private Label Artist { get; set; }
	private Label Mapper { get; set; }
	private Label Version { get; set; }
	private bool Update { get; set; }
	public override void _ExitTree()
	{
		if (texture != null)
		{
			texture.Dispose();
			texture = null;
		}
		GC.Collect();
		GC.WaitForPendingFinalizers();
	}

	private async void LoadExternalImage(string path)
	{
		if (isLoadingImage || string.IsNullOrEmpty(path)) 
			return;

		isLoadingImage = true;

		// Load image off-thread
		var data = await Task.Run(() =>
		{
			return Image.LoadFromFile(path);
		});

		// Dispose the old texture before replacing
		if (texture != null)
		{
			texture.Dispose();
			texture = null;
		}

		if (data == null)
		{
			texture = SettingsOperator.GetNullImage();
		}
		else
		{
			texture = ImageTexture.CreateFromImage(data);
			data.Dispose(); // free the Image after creating texture
		}

		// Assign to the Preview node if it's still valid
		if (texture != null && IsInstanceValid(Preview))
		{
			LoadTween = CreateTween();
			LoadTween.TweenProperty(this, "modulate", new Color(1f, 1f, 1f, 1f), 0.5f)
					.SetEase(Tween.EaseType.Out)
					.SetTrans(Tween.TransitionType.Cubic);
			LoadTween.Play();

			Preview.Texture = texture;
		}

		isLoadingImage = false;
	}

	private Tween LoadTween { get; set; }

	private void ReloadInfo()
	{
		Update = Check.CheckBoolValue(SettingsOperator.GetSetting("showunicode").ToString());
		var cache = SettingsOperator.Beatmaps[SongID];
		if (Update)
		{
			Title.Text = cache.TitleUnicode;
			Artist.Text = cache.ArtistUnicode;
		}
		else
		{
			Title.Text = cache.Title;
			Artist.Text = cache.Artist;
		}
		
		Mapper.Text = "Created by " + cache.Mapper;
		Version.Text = cache.Version;
	}

	public override void _Ready()
	{
		PivotOffset = new Vector2(Size.X / 2, Size.Y / 2);
		SettingsOperator = GetNode<SettingsOperator>("/root/SettingsOperator");
		self = GetNode<Button>(".");
		Cover = GetTree().Root.GetNode<TextureRect>("Song Select/BeatmapBackground");
		Preview = GetNode<TextureRect>("SongBackgroundPreview/BackgroundPreview");
		Title = GetNode<Label>("MarginContainer/VBoxContainer/SongTitle");
		Artist = GetNode<Label>("MarginContainer/VBoxContainer/SongArtist");
		Mapper = GetNode<Label>("MarginContainer/VBoxContainer/SongMapper");
		Version = GetNode<Label>("MarginContainer/VBoxContainer/InfoBoxBG/InfoBox/Version");

		if (!self.HasMeta("SongID"))
			self.SetMeta("SongID", 0);

		SongID = (int)GetMeta("SongID");

		ReloadInfo();





		if (self.HasMeta("background"))
		{
			BackgroundPath = self.GetMeta("background").ToString();
			Modulate = new Color(1f, 1f, 1f, 0f);
			if (File.Exists(BackgroundPath))
				LoadExternalImage(BackgroundPath);
			else
				GD.PrintErr("Background image not found: " + BackgroundPath);
		}

		SelfModulate = Checkid() ? toggledcolour : Idlecolour;
	}

	private Tween _focus_animation;
	private void AnimationButton(Color colour, bool clicked=false)
	{
		_focus_animation?.Kill();

		_focus_animation = CreateTween();
		_focus_animation.SetParallel(true);
		_focus_animation.TweenProperty(this, "self_modulate", colour, 0.2f)
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.Out);
		if (clicked)
		{
			_focus_animation.TweenProperty(this, "position:x", -40, 0.2f)
				.SetTrans(Tween.TransitionType.Cubic)
				.SetEase(Tween.EaseType.Out);
		}
		else
		{
			_focus_animation.TweenProperty(this, "position:x", 0, 0.2f)
				.SetTrans(Tween.TransitionType.Cubic)
				.SetEase(Tween.EaseType.Out);
			
		}
	}

	private Color Idlecolour = new Color(0.20f, 0.20f, 0.20f, 1f);
	private Color Focuscolour = new Color(1f, 1f, 1f, 1f);
	private Color highlightcolour = new Color(0.19f, 0.37f, 0.65f, 1f);
	private Color toggledcolour = new Color(0.09f, 0.38f, 0.85f, 1f);

	private void _highlight() => AnimationButton(highlightcolour);
	private void _focus()
	{
		SettingsOperator.SongIDHighlighted = (int)self.GetMeta("SongID");
		AnimationButton(Focuscolour, true);
	}
	private void _unfocus()
	{
		SettingsOperator.SongIDHighlighted = -1;
		AnimationButton(Checkid() ? toggledcolour : Idlecolour, false);
	}
	public void _on_pressed()
	{
		Connection_Button = true;
		SettingsOperator.SelectSongID(SongID);
	}
	private bool Accessed = false;
	public static bool Connection_Button = false;
	public bool Checkid()
	{
		return SettingsOperator.SongID.ToString().Equals(self.GetMeta("SongID").ToString());
	}

	public override void _PhysicsProcess(double delta)
	{
		if (Update != Check.CheckBoolValue(SettingsOperator.GetSetting("showunicode").ToString()))
		{
			ReloadInfo();
		}
    }



	public override void _Process(double _delta)
	{
		if (ButtonPressed != Checkid())
		{
			ButtonPressed = Checkid();
			Accessed = true;
		}
		if (ButtonPressed && Accessed)
		{
			AnimationButton(toggledcolour);
			Accessed = false;
		}
		else if (!ButtonPressed && Accessed)
		{
			AnimationButton(Idlecolour);
			Accessed = false;
		}
	}
}
