using System;
using System.Collections.Generic;

namespace TjaPlayer.Models;

public class OsuTimingPoint
{
    public double Time { get; set; }
    public double BeatLength { get; set; } // < 0: Bpm変化, > 0: BPM定義
    public int Meter { get; set; }
}

public class OsuHitObject
{
    public double Time { get; set; }
    public int Type { get; set; } // bitmask: 1=Circle, 2=Slider, 8=Spinner
    public bool IsBig { get; set; }
}

public class OsuChart
{
    public string Title { get; set; } = "";
    public string Artist { get; set; } = "";
    public string AudioFilename { get; set; } = "";
    public List<OsuTimingPoint> TimingPoints { get; set; } = new();
    public List<OsuHitObject> HitObjects { get; set; } = new();
}
