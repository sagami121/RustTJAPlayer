namespace TjaPlayer.Gameplay;

public enum Judgment
{
    None,
    Perfect,
    Good,
    Miss
}

public class JudgmentSystem
{
    // TJAPlayer-inspired windows (approximate)
    public const double PerfectWindowMs = 25.0;
    public const double GoodWindowMs = 75.0;
    public const double BadWindowMs = 110.0;

    public Judgment Judge(double diffMs)
    {
        double absDiff = System.Math.Abs(diffMs);
        if (absDiff <= PerfectWindowMs) return Judgment.Perfect;
        if (absDiff <= GoodWindowMs) return Judgment.Good;
        return Judgment.Miss;
    }
}

public class ScoringSystem
{
    public int Score { get; private set; }
    public int Combo { get; private set; }
    public int MaxCombo { get; private set; }
    public int PerfectCount { get; private set; }
    public int GoodCount { get; private set; }
    public int MissCount { get; private set; }

    public void AddScore(Judgment judgment, bool isBigNote)
    {
        int baseScore = 0;
        switch (judgment)
        {
            case Judgment.Perfect:
                baseScore = GetScoreForCombo(Combo, true);
                Combo++;
                PerfectCount++;
                break;
            case Judgment.Good:
                baseScore = GetScoreForCombo(Combo, false) / 2;
                Combo++;
                GoodCount++;
                break;
            case Judgment.Miss:
                Combo = 0;
                MissCount++;
                return;
        }

        if (isBigNote) baseScore *= 2;
        
        Score += baseScore;
        
        if (Combo > MaxCombo) MaxCombo = Combo;
    }

    private int GetScoreForCombo(int combo, bool isPerfect)
    {
        // Simple combo-based progression
        if (combo < 10) return 1000;
        if (combo < 30) return 2000;
        if (combo < 50) return 3000;
        return 4000;
    }
}