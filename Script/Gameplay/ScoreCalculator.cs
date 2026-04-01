using Godot;
using System;
namespace Game;

public class ScoreCalculator
{
    
    /// <summary>
    /// Gets the Score calculated
    /// </summary>
    public int ProcessScore(int Max, int Great, int Meh, int Notecount, float Multiplier)
    {
        var ratio = Max;
        ratio += Great / 2;
        ratio += Meh / 3;
		
        double noteCount = Math.Max(Notecount, 1);
        double accuracy = ratio / noteCount;
        double scorelocal = accuracy * 1_000_000 * Multiplier;

        return (int)Math.Round(Math.Max(0, scorelocal));
    }
}