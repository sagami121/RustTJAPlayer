using System;
using System.Collections.Generic;
using TjaPlayer.Models;

namespace TjaPlayer.Gameplay;

/// <summary>
/// Represents the detailed content of a TJA chart.
/// </summary>
public class Tja
{
    public string Title { get; set; } = "";
    public string Subtitle { get; set; } = "";
    public string Artist { get; set; } = "";
    public string Genre { get; set; } = "";
    public string BgmPath { get; set; } = "";
    
    public double Offset { get; set; }
    public List<BpmChange> BpmChanges { get; set; } = new();
    public List<Chip> Chips { get; set; } = new();
    
    public List<GogoInterval> GogoIntervals { get; set; } = new();
    
    // Course metadata (Level, etc.) for this specific chart
    public int Level { get; set; }
    public string CourseName { get; set; } = "Oni";
}

public class BpmChange
{
    public double TimeMs { get; set; }
    public double Bpm { get; set; }
}

public class GogoInterval
{
    public double StartTimeMs { get; set; }
    public double EndTimeMs { get; set; }
}
