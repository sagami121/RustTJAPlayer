using System;
using System.Drawing;
using System.IO;

namespace TjaPlayer.Utils;

public static class SkinManager
{
    public static Image? BackgroundImage { get; private set; }
    public static Image? LaneImage { get; private set; }
    public static Image? ComboImage { get; private set; }
    public static Image? ComboBigImage { get; private set; }
    public static Image? ComboTextImage { get; private set; }

    public static void Load()
    {
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string themeDir = Path.Combine(baseDir, "Theme", "default", "img");

        // フォールバックパス
        if (!Directory.Exists(themeDir))
        {
            themeDir = @"D:\dev\sagami\TjaPlayer\TJAPlayer-S\Theme\default\img";
        }

        string bgPath = Path.Combine(themeDir, "bg1.png");
        string lanePath = Path.Combine(themeDir, "lane.png");
        string comboPath = Path.Combine(themeDir, "Combo.png");
        string comboBigPath = Path.Combine(themeDir, "Combo_Big.png");
        string comboTextPath = Path.Combine(themeDir, "Combo_Text.png");

        if (File.Exists(bgPath))
        {
            try { BackgroundImage = Image.FromFile(bgPath); }
            catch (Exception ex) { Console.WriteLine($"Failed to load background image: {ex.Message}"); }
        }

        if (File.Exists(lanePath))
        {
            try { LaneImage = Image.FromFile(lanePath); }
            catch (Exception ex) { Console.WriteLine($"Failed to load lane image: {ex.Message}"); }
        }

        if (File.Exists(comboPath))
        {
            try { ComboImage = Image.FromFile(comboPath); }
            catch (Exception ex) { Console.WriteLine($"Failed to load combo image: {ex.Message}"); }
        }

        if (File.Exists(comboBigPath))
        {
            try { ComboBigImage = Image.FromFile(comboBigPath); }
            catch (Exception ex) { Console.WriteLine($"Failed to load combo_big image: {ex.Message}"); }
        }

        if (File.Exists(comboTextPath))
        {
            try { ComboTextImage = Image.FromFile(comboTextPath); }
            catch (Exception ex) { Console.WriteLine($"Failed to load combo_text image: {ex.Message}"); }
        }
    }

    public static void RenderBackground(Graphics g, int width, int height)
    {
        if (BackgroundImage != null)
        {
            g.DrawImage(BackgroundImage, 0, 0, width, height);
        }
        else
        {
            g.Clear(Color.Black);
        }
    }
}
