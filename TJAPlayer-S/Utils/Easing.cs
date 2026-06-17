using System;

namespace TjaPlayer.Utils;

public static class Easing
{
    public static double Lerp(double start, double end, double t)
    {
        return start + (end - start) * t;
    }

    // t: 0.0 to 1.0
    public static double EaseIn(double t) => t * t;
    public static double EaseOut(double t) => t * (2 - t);
    public static double EaseInOut(double t) => t < 0.5 ? 2 * t * t : -1 + (4 - 2 * t) * t;
}
