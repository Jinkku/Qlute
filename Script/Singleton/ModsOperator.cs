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
		if (Mods["ht"]){
			AudioPlayer.Instance.PitchScale = 0.5f;
		} else if (Mods["dt"]) {
			AudioPlayer.Instance.PitchScale = 1.25f;
		} else{
			AudioPlayer.Instance.PitchScale = 1f;
		}
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public static string GetModAlias(){
		var modalias = "";
		foreach (var modst in Mods.Keys){
			if (Mods[modst]){
				if (modst == "auto"){
					modalias += "AT";
				} else if (modst == "dt"){
					modalias += "DT";
				} else if (modst == "ht"){
					modalias += "HT";
				} else if (modst == "random"){
					modalias += "RND";
				} else if (modst == "slice"){
					modalias += "SL";
				} else if (modst == "black-out"){
					modalias += "BT";
				}
			}
		}
		return modalias;
	}
    public static Dictionary<string, bool> Mods { get; set; } = new Dictionary<string, bool>
    {
		{"auto", false},
		{"dt", false},
		{"ht", false},
		{"random", false},
		{"slice", false},
		{"black-out", false},
	};
}
