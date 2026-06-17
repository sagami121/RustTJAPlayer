using System;
using System.IO;

namespace TjaPlayer.Utils;

public static class ConfigManager
{
    private static readonly string ConfigPath = "config.ini";

    public static bool Autoplay { get; set; } = false;
    public static double JudgeOffset { get; set; } = 0.0; // ミリ秒単位でのタイミング微調整
    public static double PlaybackSpeed { get; set; } = 1.0; // 再生速度 (1.0 = 標準)

    public static void Load()
    {
        if (!File.Exists(ConfigPath)) return;
        
        var lines = File.ReadAllLines(ConfigPath);
        foreach (var line in lines)
        {
            if (line.StartsWith("Autoplay=")) Autoplay = bool.Parse(line.Substring(9));
            if (line.StartsWith("JudgeOffset="))
            {
                if (double.TryParse(line.Substring(12), out var jo)) JudgeOffset = jo;
            }
            if (line.StartsWith("PlaybackSpeed="))
            {
                if (double.TryParse(line.Substring(14), out var ps)) PlaybackSpeed = ps;
            }
        }
    }

    public static void Save()
    {
        File.WriteAllLines(ConfigPath, new[] { 
            $"Autoplay={Autoplay}",
            $"JudgeOffset={JudgeOffset}",
            $"PlaybackSpeed={PlaybackSpeed}"
        });
    }
}
