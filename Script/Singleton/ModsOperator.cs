using Godot;
using System.Collections.Generic;
using System;

public partial class ModsOperator : Node
{
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
	}
	public static void Reset()
	{
		foreach(var mod in Mods)
		{
			Mods[mod.Key] = false;
		}
		Refresh();
	}
	public static void Refresh()
	{
        if (ModsOperator.Mods["auto"])
        {
            SettingsOperator.SpectatorMode = true;
        }
        else SettingsOperator.SpectatorMode = false;
		
		if (Mods["ht"])
		{
			AudioPlayer.Instance.PitchScale = 0.5f;
		}
		else if (Mods["dt"])
		{
			AudioPlayer.Instance.PitchScale = 1.25f;
		}
		else
		{
			AudioPlayer.Instance.PitchScale = 1f;
		}
		var modalias = GetModAlias();
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public static List<string> ModsEnabled { get; set; } = new List<string>();
	public static string GetModAlias()
	{
		var modalias = "";
		ModsEnabled.Clear();
		foreach (var modst in Mods.Keys)
		{
			if (Mods[modst])
			{
				if (modst == "auto")
				{
					modalias += "AT";
					ModsEnabled.Add("AT");
				}
				else if (modst == "dt")
				{
					modalias += "DT";
					ModsEnabled.Add("DT");
				}
				else if (modst == "ht")
				{
					modalias += "HT";
					ModsEnabled.Add("HT");
				}
				else if (modst == "random")
				{
					modalias += "RND";
					ModsEnabled.Add("RND");
				}
				else if (modst == "slice")
				{
					modalias += "SL";
					ModsEnabled.Add("SL");
				}
				else if (modst == "black-out")
				{
					modalias += "BT";
					ModsEnabled.Add("BT");
				}
				else if (modst == "no-fail")
				{
					modalias += "NF";
					ModsEnabled.Add("NF");
				}
				else if (modst == "npc")
				{
					modalias += "";
					ModsEnabled.Add("NPC");
				}
			}
		}
		return modalias;
	}
    public static Dictionary<string, bool> Mods { get; set; } = new Dictionary<string, bool>
    {
		{"auto", false},
		{"no-fail", false},
		{"dt", false},
		{"ht", false},
		{"random", false},
		{"slice", false},
		{"black-out", false},
		{"npc", false},
	};
}
