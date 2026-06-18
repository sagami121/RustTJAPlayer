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

    public static double EaseOutBack(double t)
    {
        const double c1 = 1.70158;
        const double c3 = c1 + 1;
        return 1 + c3 * Math.Pow(t - 1, 3) + c1 * Math.Pow(t - 1, 2);
    }

    public static double EaseOutElastic(double t)
    {
        const double c4 = (2 * Math.PI) / 3;
        return t == 0 ? 0 : t == 1 ? 1 : Math.Pow(2, -10 * t) * Math.Sin((t * 10 - 0.75) * c4) + 1;
    }

    public static double EaseOutBounce(double t)
    {
        const double n1 = 7.5625;
        const double d1 = 2.75;

        if (t < 1 / d1) return n1 * t * t;
        else if (t < 2 / d1) return n1 * (t -= 1.5 / d1) * t + 0.75;
        else if (t < 2.5 / d1) return n1 * (t -= 2.25 / d1) * t + 0.9375;
        else return n1 * (t -= 2.625 / d1) * t + 0.984375;
    }
}
