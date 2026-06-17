using System;
using System.Drawing;
using System.Drawing.Text;
using System.IO;

namespace TjaPlayer.Utils;

public static class FontManager
{
    private static PrivateFontCollection privateFonts = new PrivateFontCollection();
    public static FontFamily KantiryuFontFamily { get; private set; } = FontFamily.GenericSansSerif;

    public static void Load()
    {
        string fontPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fonts", "ＤＦＰ勘亭流.ttf");
        if (File.Exists(fontPath))
        {
            try
            {
                privateFonts.AddFontFile(fontPath);
                KantiryuFontFamily = privateFonts.Families[0];
            }
            catch
            {
                KantiryuFontFamily = FontFamily.GenericSansSerif;
            }
        }
        else
        {
            KantiryuFontFamily = FontFamily.GenericSansSerif;
        }
    }
}
