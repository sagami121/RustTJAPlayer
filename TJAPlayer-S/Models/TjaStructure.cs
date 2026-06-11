using System.Collections.Generic;
using TjaPlayer.Models;

namespace TjaPlayer.Models;

public class Tja
{
    public string Title { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public string BgmPath { get; set; } = string.Empty;
    
    // For prototype, we focus on what's needed for selection
    public List<CourseMetadata> CourseMetadata { get; set; } = new();
}

public class CourseMetadata
{
    public string CourseType { get; set; } = string.Empty;
    public int LevelTaiko { get; set; }
}

public class SongInfo
{
    public Tja Chart { get; set; } = new();
    public string TjaPath { get; set; } = string.Empty;
    public string? AudioPath { get; set; }
}
