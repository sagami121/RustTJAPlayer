using System;

namespace TjaPlayer.Animations;

public class CCounter
{
    private double startValue;
    private double endValue;
    private double tickInterval;
    private DateTime startTime;
    private bool isRunning;

    public double CurrentValue { get; private set; }

    public void t開始(double start, double end, double interval, System.Diagnostics.Stopwatch timer)
    {
        startValue = start;
        endValue = end;
        tickInterval = interval;
        startTime = DateTime.Now;
        isRunning = true;
        CurrentValue = start;
    }

    public void t停止()
    {
        isRunning = false;
    }

    public void t進行()
    {
        if (!isRunning) return;

        double elapsed = (DateTime.Now - startTime).TotalMilliseconds;
        double progress = elapsed / tickInterval;

        if (progress >= 1.0)
        {
            CurrentValue = endValue;
            isRunning = false;
        }
        else
        {
            CurrentValue = startValue + (endValue - startValue) * progress;
        }
    }

    public bool b終了値に達した => !isRunning;
}
