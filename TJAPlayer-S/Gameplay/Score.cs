using System;
using System.Collections.Generic;
using TjaPlayer.Models;

namespace TjaPlayer.Gameplay;

/// <summary>
/// Represents song metadata and high-level information for song selection.
/// </summary>
public class Score
{
    public string Title { get; set; } = "";
    public string Subtitle { get; set; } = "";
    public string Artist { get; set; } = "";
    public string Genre { get; set; } = "";
    public System.Drawing.Color GenreColor { get; set; } = System.Drawing.Color.DimGray;
    public System.Drawing.Color FontColor { get; set; } = System.Drawing.Color.White;
    
    public string FilePath { get; set; } = "";
    public string DirectoryPath { get; set; } = "";
    
    // Level for each difficulty (Normal, Expert, Master, etc.)
    public int[] Levels { get; set; } = new int[5];
    public int HighScore { get; set; }
    
    public double BaseBpm { get; set; }
    public double MinBpm { get; set; }
    public double MaxBpm { get; set; }

    // Reference to the actual chart data if loaded
    public Dictionary<string, TjaChart> Charts { get; set; } = new();
}
