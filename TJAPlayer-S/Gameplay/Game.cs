namespace TjaPlayer.Gameplay;

public enum Judgment
{
    None,
    Perfect,
    Good,
    Miss,
    Balloon,
    BalloonBreak
}

public class JudgmentSystem
{
    // Original TJAPlayer windows
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
    public int BalloonCount { get; private set; }

    public int ScoreInit { get; }
    public int ScoreDiff { get; }
    private readonly int scoremode; // 0: ドンダフル, 1: 旧配点, 2: 新配点

    public ScoringSystem(int scoreInit = 0, int scoreDiff = 0, int scoremode = 0)
    {
        this.ScoreInit = scoreInit;
        this.ScoreDiff = scoreDiff;
        this.scoremode = scoremode;
        this.Score = scoreInit;
    }

    public void AddScore(Judgment judgment, bool isBigNote, bool isGogo)
    {
        if (judgment == Judgment.Miss)
        {
            Combo = 0;
            MissCount++;
            return;
        }

        // Calculate DiffMul based on combo and scoremode
        int DiffMul = 0;
        switch (scoremode)
        {
            case 0: // ドンダフル配点
                DiffMul = (Combo >= 200) ? 1 : 0;
                break;
            case 1: // 旧配点
                DiffMul = (Combo + 1) / 10;
                if (Combo > 100) DiffMul = 10;
                break;
            case 2: // 新配点
                if (Combo >= 0 && Combo < 9) DiffMul = 0;
                else if (Combo >= 9 && Combo < 29) DiffMul = 1;
                else if (Combo >= 29 && Combo < 49) DiffMul = 2;
                else if (Combo >= 49 && Combo < 99) DiffMul = 4;
                else if (Combo >= 99) DiffMul = 8;
                break;
        }

        int HitScore = ScoreInit + ScoreDiff * DiffMul;
        double GOGOMul = isGogo ? 1.2 : 1.0;
        int points = 0;

        switch (judgment)
        {
            case Judgment.Perfect:
                points = (int)(HitScore * GOGOMul);
                PerfectCount++;
                break;
            case Judgment.Good:
                points = (int)(HitScore / 2);
                GoodCount++;
                break;
            case Judgment.Balloon:
                // Balloon points: 100 normal, 300 GOGO (based on 3DS scoremode 0/1)
                points = (int)(100 * GOGOMul);
                BalloonCount++;
                break;
            case Judgment.BalloonBreak:
                // Balloon break points: 5000 normal, 6000 GOGO (all scoremodes)
                points = (int)(5000 * GOGOMul);
                // BalloonBreak counts as a hit for combo purposes
                PerfectCount++;
                break;
        }

        if (isBigNote)
        {
            points *= 2;
        }

        Score += points;

        // Increase combo for judgments that represent successful hits
        if (judgment == Judgment.Perfect ||
            judgment == Judgment.Good ||
            judgment == Judgment.Balloon ||
            judgment == Judgment.BalloonBreak)
        {
            Combo++;
            if (Combo > MaxCombo) MaxCombo = Combo;
        }
        // Miss judgments already reset combo above
    }
}