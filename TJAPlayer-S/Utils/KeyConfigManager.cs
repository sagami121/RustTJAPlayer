using System;
using System.IO;
using System.Windows.Forms;

namespace TjaPlayer.Utils;

public static class KeyConfigManager
{
    private static readonly string ConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "keyconfig.ini");

    public static Keys DonLeft { get; set; } = Keys.F;
    public static Keys DonRight { get; set; } = Keys.J;
    public static Keys KaLeft { get; set; } = Keys.D;
    public static Keys KaRight { get; set; } = Keys.K;

    public static void Load()
    {
        if (!File.Exists(ConfigPath)) return;
        
        var lines = File.ReadAllLines(ConfigPath);
        foreach (var line in lines)
        {
            var parts = line.Split('=');
            if (parts.Length != 2) continue;
            
            if (Enum.TryParse<Keys>(parts[1], out var key))
            {
                switch (parts[0])
                {
                    case "DonLeft": DonLeft = key; break;
                    case "DonRight": DonRight = key; break;
                    case "KaLeft": KaLeft = key; break;
                    case "KaRight": KaRight = key; break;
                }
            }
        }
    }

    public static void Save()
    {
        File.WriteAllLines(ConfigPath, new[] { 
            $"DonLeft={DonLeft}",
            $"DonRight={DonRight}",
            $"KaLeft={KaLeft}",
            $"KaRight={KaRight}"
        });
    }
}
