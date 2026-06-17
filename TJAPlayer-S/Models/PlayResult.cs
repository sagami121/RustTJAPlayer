namespace TjaPlayer.Models;

public class PlayResult
{
    public string SongTitle { get; set; } = "";
    public int PerfectCount { get; set; }
    public int GoodCount { get; set; }
    public int MissCount { get; set; }
    public int MaxCombo { get; set; }
    public int TotalScore { get; set; }
}
