using System.Collections.Generic;
using System.Numerics;

namespace TjaPlayer.Models;

public enum NoteType { None, Don = 1, Ka, BigDon, BigKa, Roll, BigRoll, End = 7, Balloon = 8 }

public class Note
{
    public NoteType Type { get; set; }
    public double TimeMs { get; set; }
    public double EndTimeMs { get; set; }
    public Complex ScrollValue { get; set; } = new Complex(1.0, 0.0);
    public double Bpm { get; set; }
    public bool IsHit { get; set; }
    public bool IsGogo { get; set; }      
    public bool IsVisible { get; set; } = true; 
    public double LastHitTimeMs { get; set; } // 連打用：最後に叩いた時間
}

public class Barline
{
    public double TimeMs { get; set; }
    public Complex ScrollValue { get; set; } = new Complex(1.0, 0.0);
    public double Bpm { get; set; } // 追加
    public bool IsVisible { get; set; } = true;
}

public class TjaChart
{
    public List<Note> Notes { get; set; } = new List<Note>();
    public List<Barline> Barlines { get; set; } = new List<Barline>();
    public double WaveOffsetMs { get; set; }   // OFFSET値 (秒換算してミリ秒に)
    public string AudioFileName { get; set; } = "";  // WAVE値
    public string DirectoryPath { get; set; } = ""; // 譜面ファイルのディレクトリ
}