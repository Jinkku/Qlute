using Godot;
using System;
namespace Game;

public class ScoreCalculator
{
    /// <summary>
    /// Competitive score: 0..1,000,000 on NoMod (900k accuracy + 100k combo; all Great = 700k, all Meh = 300k), times the mod multiplier.
    /// <paramref name="comboPortion"/> is the sum of PerformanceCalculator.ComboWeight over every hit.
    /// Every miss (<paramref name="bad"/>) takes off one perfect note's accuracy value; never below 0.
    /// </summary>
    public int ProcessScore(int noteCount, int perfect, int great, int meh, int bad, long comboPortion, double multiplier)
    {
        return PerformanceCalculator.ComputeScore(Math.Max(noteCount, 1), perfect, great, meh, bad, comboPortion, multiplier);
    }
}
