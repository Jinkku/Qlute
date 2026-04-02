using Godot;
using System.Collections.Generic;
using System;

public partial class ModsOperator : Node
{
	public float DTSpeedMultiplier { get; set; } = 1.25f;
	public float HTSpeedMultiplier { get; set; } = 0.5f;
	public static void Reset()
	{
		foreach(var mod in Mods)
		{
			Mods[mod.Key] = false;
		}
		DtCustom.Speed = DtCustom.DTSpeed;
		HtCustom.Speed = HtCustom.HTSpeed;
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

	/// <summary>
	/// Simulate the multiplier
	/// </summary>
	public float ProcessMultiplierByMod(string Mods)
	{
		var multiplier= 1.0f;
		if (Mods.Contains("DT"))
		{
			multiplier *= 1.15f * (AudioPlayer.Instance.PitchScale / DTSpeedMultiplier);
		}
		else if (Mods.Contains("HT"))
		{
			multiplier *= 0.3f * (AudioPlayer.Instance.PitchScale / HTSpeedMultiplier);
		}
		else if (Mods.Contains("SL"))
		{
			multiplier *= 1.05f;
		}
		else if (Mods.Contains("BT"))
		{
			multiplier *= 1.15f;
		}
		else if (Mods.Contains("NF"))
		{
			multiplier *= 0.5f;
		}
		else if (Mods.Contains("AM"))
		{
			multiplier *= 0.5f;
		}
		return multiplier;
	}
	/// <summary>
	/// Simulate the multiplier
	/// </summary>
	public float ProcessMultiplierByModList(Dictionary<string, bool> Mods)
	{
		var multiplier= 1.0f;
		foreach (var mod in Mods)
		{
			if (mod.Value && mod.Key == "dt")
			{
				multiplier *= 1.15f * (AudioPlayer.Instance.PitchScale / DTSpeedMultiplier);
			}
			else if (mod.Value && mod.Key == "ht")
			{
				multiplier *= 0.3f * (AudioPlayer.Instance.PitchScale / HTSpeedMultiplier);
			}
			else if (mod.Value && mod.Key == "no-fail")
			{
				multiplier *= 0.5f;
			}
			else if (mod.Value && mod.Key == "am")
			{
				multiplier *= 0.5f;
			}
			else if (mod.Value && mod.Key == "black-out")
			{
				multiplier *= 1.15f;
			}
			else if (mod.Value && mod.Key != "auto" && mod.Key != "random")
			{
				multiplier *= 1.05f;
			} 
		}

		return multiplier;
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
		else if (modalias.Contains("AM"))
		{
			modsparsed.Add("AM");
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
				else if (modst == "am")
				{
					modalias += "AM";
					ModsEnabled.Add("AM");
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
		else if (modalias == "AM")
		{
			Mods["am"] = true;
			ModsEnabled.Add("AM");
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
		{"am", false},
	};
}
