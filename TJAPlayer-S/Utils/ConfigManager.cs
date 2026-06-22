using System;
using System.IO;

namespace TjaPlayer.Utils;

public static class ConfigManager
{
    private static readonly string ConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.ini");

    public static bool Autoplay { get; set; } = false;
    public static double JudgeOffset { get; set; } = 0.0; // ミリ秒単位でのタイミング微調整
    public static double InputAdjustTimeMs { get; set; } = 0.0; // ミリ秒単位での入力タイミング調整
    public static double PlaybackSpeed { get; set; } = 1.0; // 再生速度 (1.0 = 標準)
    
    // 演奏オプション
    public enum NoteMod { None, Abekobe, Kimagure, Detarame }
    public static NoteMod CurrentNoteMod { get; set; } = NoteMod.None;
    public static bool IsDoron { get; set; } = false;
    public static int ScrollSpeed { get; set; } = 1; // 1, 2, 3, 4

    public static bool CreationMode_ShowMeasure { get; set; } = true; // 譜面制作モード設定

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
            if (line.StartsWith("InputAdjustTimeMs="))
            {
                if (double.TryParse(line.Substring(18), out var ia)) InputAdjustTimeMs = ia;
            }
            if (line.StartsWith("PlaybackSpeed="))
            {
                if (double.TryParse(line.Substring(14), out var ps)) PlaybackSpeed = ps;
            }
            if (line.StartsWith("NoteMod=")) CurrentNoteMod = (NoteMod)Enum.Parse(typeof(NoteMod), line.Substring(8));
            if (line.StartsWith("Doron=")) IsDoron = bool.Parse(line.Substring(6));
            if (line.StartsWith("ScrollSpeed="))
            {
                if (int.TryParse(line.Substring(12), out var ss)) ScrollSpeed = ss;
            }
            if (line.StartsWith("CreationMode_ShowMeasure="))
            {
                string val = line.Substring(25).Trim().ToLower();
                if (val == "true" || val == "t") CreationMode_ShowMeasure = true;
                else if (val == "false" || val == "f") CreationMode_ShowMeasure = false;
            }
        }
    }

    public static void Save()
    {
        File.WriteAllLines(ConfigPath, new[] {
            $"Autoplay={Autoplay}",
            $"JudgeOffset={JudgeOffset}",
            $"InputAdjustTimeMs={InputAdjustTimeMs}",
            $"PlaybackSpeed={PlaybackSpeed}",
            $"NoteMod={CurrentNoteMod}",
            $"Doron={IsDoron}",
            $"ScrollSpeed={ScrollSpeed}",
            $"CreationMode_ShowMeasure={CreationMode_ShowMeasure}"
        });
    }
}
