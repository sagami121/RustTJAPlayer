using System;
using System.Drawing;

namespace TjaPlayer.Models;

public class JudgmentDisplay
{
    public string Text { get; set; }
    public Color Color { get; set; }
    public System.Diagnostics.Stopwatch Timer { get; set; }
    public float StartY { get; set; }
    public const int DurationMs = 600;

    public JudgmentDisplay(string text, Color color, float startY)
    {
        Text = text;
        Color = color;
        StartY = startY;
        Timer = System.Diagnostics.Stopwatch.StartNew();
    }
}
