using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.IO.Compression;
using System.IO;

public partial class SkinEditor : Control
{
	Node currentScene;
	private SettingsOperator SettingsOperator { get; set; }
	private VBoxContainer Menu { get; set; }
	private HBoxContainer ViewPoints { get; set; }
	private PopupMenu FileMenu { get; set; }
	public override void _Ready()
	{
		currentScene = GetTree().CurrentScene;
		SettingsOperator = GetNode<SettingsOperator>("/root/SettingsOperator");
		Menu = GetNode<VBoxContainer>("Creativity/VBoxContainer/ScrollContainer/Elements");
		ViewPoints = GetNode<HBoxContainer>("ToolBar/VBoxContainer/PartA/ViewPoints");
		FileMenu = GetNode<MenuButton>("ToolBar/VBoxContainer/PartA/File").GetPopup();
		FileMenu.Connect("id_pressed", new Callable(this, nameof(FileID)));
		if (SettingsOperator.TopPanelPosition > 49)
		{
			SettingsOperator.toppaneltoggle();
		}
		Check();
	}
	private void Export()
	{
		GD.Print("Save changes before exporting");
		SaveChanges();
		GD.Print("Should be saved..");
		var path = Skin.Element.SkinPath;
		if (Skin.Element.SkinPath == null)
		{
			path = SettingsOperator.skinsdir.PathJoin(Skin.Element.Name);
		}
		ZipFile.CreateFromDirectory(path, SettingsOperator.exportdir.PathJoin($"{Skin.Element.Name}.qsk"), CompressionLevel.Optimal, true);
		Notify.Post($"Exported {Skin.Element.Name}.\nClick to view.", $"file://{SettingsOperator.exportdir}");
	}
	private void SaveChanges()
	{
		if (Skin.Element.Name.Length < 1)
		{
			Skin.Element.Name = new SkinningLegend().Name;
		}
		Dictionary<string, object> SkinSettings = new Dictionary<string, object> // Settings for the skin!!!!
		{
			{"Name" , Skin.Element.Name},
			{"NoteLane1" , Skin.Element.LaneNotes[0].ToHtml(false)},
			{"NoteLane2" , Skin.Element.LaneNotes[1].ToHtml(false)},
			{"NoteLane3" , Skin.Element.LaneNotes[2].ToHtml(false)},
			{"NoteLane4" , Skin.Element.LaneNotes[3].ToHtml(false)},
		};
		var path = Skin.Element.SkinPath;
		if (Skin.Element.SkinPath == null)
		{
			path = SettingsOperator.skinsdir.PathJoin(Skin.Element.Name);
		}
		if (!Directory.Exists(path))
		{
			System.IO.Directory.CreateDirectory(path);
		}
		var index = 0;
		foreach (Texture2D img in new List<Texture2D>([Skin.Element.NoteBack, Skin.Element.NoteFore, Skin.Element.Cursor]))
		{
			Error resultPng = img.GetImage().SavePng(path.PathJoin(Skin.ImageNames[index]));
			if (resultPng == Error.Ok)
			{
				GD.Print($"Image saved successfully to: {path.PathJoin(Skin.ImageNames[index])}");
			}
			else
			{
				GD.PrintErr($"Failed to save image to PNG: {resultPng}");
			}
			index++;
		}
		using var saveFile = Godot.FileAccess.Open(path.PathJoin("settings.json"), Godot.FileAccess.ModeFlags.Write);
		var json = JsonSerializer.Serialize(SkinSettings);
		saveFile.StoreString(json);
		saveFile.Close();
		Notify.Post($"Saved {Skin.Element.Name}");
	}
	private bool MissingFunction { get; set; } = false;
	private Container MissingDisclaimer { get; set; } = null;

	private void Check()
	{
		if (GetTree().CurrentScene == null && MissingFunction == false)
		{
			MissingFunction = true;
			Menu.Hide();
		}
		else if (GetTree().CurrentScene.Name == "Gameplay" && MissingFunction == true)
		{
			MissingFunction = false;
			Menu.Show();
		}
		else if (GetTree().CurrentScene.Name != "Gameplay" && MissingFunction == false)
		{
			MissingFunction = true;
			Menu.Hide();
		}
	}
	public override void _Process(double delta)
	{
		Check();
	}

	private void _on_back()
	{
		Global._SkinStart = !Global._SkinStart;
	}
	private void FileID(int index)
	{
		GD.Print(index);
		if (index == 0)
		{
			SaveChanges();
		}
		else if (index == 1)
		{
			Export();
		}
		else if (index == 2)
		{
			string uri = "file://" + Skin.Element.SkinPath;
        	OS.ShellOpen(uri);
		}
		else if (index == 3)
		{
			Skin.ReloadSkin(Skin.Element.SkinPath);
			Skin.Element = Skin.List[Skin.SkinIndex];
			Notify.Post($"Reloaded {Skin.Element.Name}");
		}
	}
	private PanelContainer Settings { get; set; }
	private void _Game()
	{
		if (Settings != null)
		{
			Settings.QueueFree();
			Settings = null;
		}
	}
	private void _Settings()
	{
		if (Settings == null)
		{
			Settings = GD.Load<PackedScene>("res://Panels/SkinEditorElements/Settings.tscn").Instantiate().GetNode<PanelContainer>(".");
			AddChild(Settings);
			Settings.Size = new Vector2(Settings.Size.X, Settings.Size.Y - 150);
			Settings.Position = new Vector2(Settings.Position.X, 100);
		}
	}
}
