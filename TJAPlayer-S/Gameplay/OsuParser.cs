using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using TjaPlayer.Models;

namespace TjaPlayer.Gameplay;

public class OsuParser
{
    public static OsuChart Parse(string filePath)
    {
        var chart = new OsuChart();
        var lines = File.ReadAllLines(filePath);
        string section = "";

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (line.StartsWith("[") && line.EndsWith("]"))
            {
                section = line;
                continue;
            }

            if (section == "[General]")
            {
                if (line.StartsWith("AudioFilename:")) chart.AudioFilename = line.Substring(14).Trim();
            }
            else if (section == "[Metadata]")
            {
                if (line.StartsWith("Title:")) chart.Title = line.Substring(6).Trim();
                else if (line.StartsWith("Artist:")) chart.Artist = line.Substring(7).Trim();
            }
            else if (section == "[TimingPoints]")
            {
                var parts = line.Split(',');
                if (parts.Length >= 8)
                {
                    chart.TimingPoints.Add(new OsuTimingPoint
                    {
                        Time = double.Parse(parts[0], CultureInfo.InvariantCulture),
                        BeatLength = double.Parse(parts[1], CultureInfo.InvariantCulture),
                        Meter = int.Parse(parts[2])
                    });
                }
            }
            else if (section == "[HitObjects]")
            {
                var parts = line.Split(',');
                if (parts.Length >= 5)
                {
                    int type = int.Parse(parts[3]);
                    chart.HitObjects.Add(new OsuHitObject
                    {
                        Time = double.Parse(parts[2], CultureInfo.InvariantCulture),
                        Type = type,
                        IsBig = (type & 4) != 0 // 太鼓の達人譜面における大音符フラグ(簡易)
                    });
                }
            }
        }
        return chart;
    }
}
