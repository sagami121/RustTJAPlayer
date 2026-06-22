using System;
using TjaPlayer.Gameplay;

namespace TjaPlayer.Models;

/// <summary>
/// Gauge system for Taiko no Tatsujin style fail/pass mechanics
/// </summary>
public class Gauge
{
    private double _value;
    private const double Maximum = 1.0;
    private const double Minimum = 0.0;

    // Gain/Loss amounts (tunable)
    public double PerfectGain { get; set; } = 0.015;
    public double GoodGain { get; set; } = 0.010;
    public double MissLoss { get; set; } = 0.020;

    public double Value => Math.Max(Minimum, Math.Min(Maximum, _value));
    public bool IsEmpty => Value <= Minimum;
    public bool IsFull => Value >= Maximum;
    public bool IsFailed => IsEmpty;

    public Gauge(double initialValue = 0.0)
    {
        _value = Math.Max(Minimum, Math.Min(Maximum, initialValue));
    }

    /// <summary>
    /// Increase gauge based on judgment
    /// </summary>
    public void Add(Judgment judgment)
    {
        switch (judgment)
        {
            case Judgment.Perfect:
                _value += PerfectGain;
                break;
            case Judgment.Good:
                _value += GoodGain;
                break;
            case Judgment.Miss:
                _value -= MissLoss;
                break;
        }

        // Clamp value
        _value = Math.Max(Minimum, Math.Min(Maximum, _value));
    }

    /// <summary>
    /// Reset gauge to initial value
    /// </summary>
    public void Reset(double initialValue = 0.0)
    {
        _value = Math.Max(Minimum, Math.Min(Maximum, initialValue));
    }
}