namespace TjaPlayer.Gameplay;

public enum Judgment
{
    None,
    Perfect,
    Good,
    Bad,
    Miss
}

public class JudgmentSystem
{
    public const double PerfectWindowMs = 30.0;
    public const double GoodWindowMs = 70.0;
    public const double BadWindowMs = 120.0;

    public Judgment Judge(double diffMs)
    {
        double absDiff = System.Math.Abs(diffMs);
        if (absDiff <= PerfectWindowMs) return Judgment.Perfect;
        if (absDiff <= GoodWindowMs) return Judgment.Good;
        if (absDiff <= BadWindowMs) return Judgment.Bad;
        return Judgment.None;
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

    public void AddScore(Judgment judgment)
    {
        switch (judgment)
        {
            case Judgment.Perfect:
                Score += 1000;
                Combo++;
                PerfectCount++;
                break;
            case Judgment.Good:
                Score += 500;
                Combo++;
                GoodCount++;
                break;
            case Judgment.Bad:
                break;
            case Judgment.Miss:
                Combo = 0;
                MissCount++;
                break;
        }
        if (Combo > MaxCombo) MaxCombo = Combo;
    }
}
