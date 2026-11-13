using Godot;
using System;
namespace Game;

public class ScoreCalculator
{
    /// <summary>
    /// Competitive score: 0..1,000,000 on NoMod (900k accuracy + 100k combo), times the mod multiplier.
    /// <paramref name="comboPortion"/> is the sum of PerformanceCalculator.ComboWeight over every hit.
    /// </summary>
    public int ProcessScore(int noteCount, int perfect, int great, int meh, long comboPortion, double multiplier)
    {
        return PerformanceCalculator.ComputeScore(Math.Max(noteCount, 1), perfect, great, meh, comboPortion, multiplier);
    }
}
