using System.Collections.Generic;
using System.Numerics;
using TjaPlayer.Utils;

namespace TjaPlayer.Models;

public enum NoteType { None, Don = 1, Ka, BigDon, BigKa, Roll, BigRoll, End = 7, Balloon = 8 }
public enum BranchType { Normal, Professional, Master }

public class Note
{
    public NoteType Type { get; set; }
    public BranchType Branch { get; set; } = BranchType.Normal; // 追加
    public double TimeMs { get; set; }
    public double EndTimeMs { get; set; }
    public Complex ScrollValue { get; set; } = new Complex(1.0, 0.0);
    public double Bpm { get; set; }
    public bool IsHit { get; set; }
    public bool IsGogo { get; set; }
    public bool IsVisible { get; set; } = true;
    public double LastHitTimeMs { get; set; } // 連打用：最後に叩いた時間
    public int BalloonHitCount { get; set; }  // 風船ヒットカウント
    public int BalloonRequiredHits { get; set; } = 4; // デフォルト必要ヒット数
}

public class Barline
{
    public double TimeMs { get; set; }
    public Complex ScrollValue { get; set; } = new Complex(1.0, 0.0);
    public double Bpm { get; set; } // 追加
    public bool IsVisible { get; set; } = true;
}

public class LyricEvent
{
    public double TimeMs { get; set; }
    public string Text { get; set; } = "";
}

public class TjaChart
{
    public List<Note> Notes { get; set; } = new List<Note>();
    public List<Barline> Barlines { get; set; } = new List<Barline>();
    public List<LyricEvent> Lyrics { get; set; } = new List<LyricEvent>();
    public double WaveOffsetMs { get; set; }   // OFFSET値 (秒換算してミリ秒に)
    public string AudioFileName { get; set; } = "";  // WAVE値 (ファイル名)
    public string DirectoryPath { get; set; } = ""; // 譜面ファイルのディレクトリ
    public bool HasBranches { get; set; } = false;

    // コース情報
    public int Level { get; set; }
    public string CourseName { get; set; } = "Oni";

    public void ApplyGameOptions(Utils.ConfigManager.NoteMod noteMod, bool isDoron)
    {
        Random rng = new Random();
        foreach (var note in Notes)
        {
            note.IsHit = false;
            note.LastHitTimeMs = 0;

            // オプション適用
            if (noteMod == Utils.ConfigManager.NoteMod.Abekobe)
            {
                if (note.Type == NoteType.Don) note.Type = NoteType.Ka;
                else if (note.Type == NoteType.Ka) note.Type = NoteType.Don;
                else if (note.Type == NoteType.BigDon) note.Type = NoteType.BigKa;
                else if (note.Type == NoteType.BigKa) note.Type = NoteType.BigDon;
            }
            // きまぐれ/でたらめ (簡易)
            if (noteMod == Utils.ConfigManager.NoteMod.Kimagure && rng.NextDouble() < 0.2)
                note.Type = (note.Type == NoteType.Don || note.Type == NoteType.BigDon) ? NoteType.Ka : NoteType.Don;
            if (noteMod == Utils.ConfigManager.NoteMod.Detarame && rng.NextDouble() < 0.5)
                note.Type = (note.Type == NoteType.Don || note.Type == NoteType.BigDon) ? NoteType.Ka : NoteType.Don;
        }
    }

    public string GetCurrentLyric(double time)
    {
        return Lyrics.LastOrDefault(l => l.TimeMs <= time)?.Text ?? "";
    }

    public BranchType? GetNextNoteBranch(double time)
    {
        var nextNote = Notes.FirstOrDefault(n => !n.IsHit && n.TimeMs >= time);
        return nextNote?.Branch;
    }
}