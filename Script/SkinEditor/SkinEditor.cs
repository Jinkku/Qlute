using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;

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
	private void SaveChanges()
	{
		Dictionary<string, object> SkinSettings = new Dictionary<string, object> // Settings for the skin!!!!
		{
			{"Name" , Skin.Element.Name},
			{"NoteLane1" , Skin.Element.LaneNotes[0].ToHtml(false)},
			{"NoteLane2" , Skin.Element.LaneNotes[1].ToHtml(false)},
			{"NoteLane3" , Skin.Element.LaneNotes[2].ToHtml(false)},
			{"NoteLane4" , Skin.Element.LaneNotes[3].ToHtml(false)},
		};


		using var saveFile = FileAccess.Open(Skin.Element.SkinPath.PathJoin("settings.json"), FileAccess.ModeFlags.Write);
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
		if (index == 1)
		{
			SaveChanges();
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
