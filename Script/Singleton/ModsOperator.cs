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

	public static List<string> ParseModAlias(string modalias)
	{
		var modsparsed = new List<string>();
		if (modalias.Contains("AT"))
		{
			modsparsed.Add("AT");
		}
		else if (modalias.Contains("DT"))
		{
			modsparsed.Add("DT");
		}
		else if (modalias.Contains("HT"))
		{
			modsparsed.Add("HT");
		}
		else if (modalias.Contains("RND"))
		{
			modsparsed.Add("RND");
		}
		else if (modalias.Contains("SL"))
		{
			modsparsed.Add("SL");
		}
		else if (modalias.Contains("BT"))
		{
			modsparsed.Add("BT");
		}
		else if (modalias.Contains("NF"))
		{
			modsparsed.Add("NF");
		}
		return modsparsed;
	}
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


	public static void SetMods(string modalias)
	{
		Reset();
		ModsEnabled.Clear();
		if (modalias == "AT")
		{
			Mods["auto"] = true;
			ModsEnabled.Add("AT");
		}
		else if (modalias == "DT")
		{
			Mods["dt"] = true;
			ModsEnabled.Add("DT");
		}
		else if (modalias == "HT")
		{
			Mods["ht"] = true;
			ModsEnabled.Add("HT");
		}
		else if (modalias == "RND")
		{
			Mods["random"] = true;
			ModsEnabled.Add("RND");
		}
		else if (modalias == "SL")
		{
			Mods["slice"] = true;
			ModsEnabled.Add("SL");
		}
		else if (modalias == "BT")
		{
			Mods["black-out"] = true;
			ModsEnabled.Add("BT");
		}
		else if (modalias == "NF")
		{
			Mods["no-fail"] = true;
			ModsEnabled.Add("NF");
		}
		Refresh();
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
