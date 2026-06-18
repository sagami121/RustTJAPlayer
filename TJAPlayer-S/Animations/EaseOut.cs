namespace TjaPlayer.Animations;

/// <summary>
/// イーズアウトを行うクラス。
/// </summary>
internal class EaseOut : Animator
{
    private readonly double StartPoint;
    private readonly double Sa;
    private readonly double TimeMs;

    public EaseOut(double startPoint, double endPoint, double timeMs, System.Diagnostics.Stopwatch timer) 
        : base(0, timeMs, timeMs, false, timer)
    {
        StartPoint = startPoint;
        Sa = endPoint - startPoint;
        TimeMs = timeMs;
    }

    public override object GetAnimation()
    {
        var persent = (double)Counter.CurrentValue / TimeMs;
        persent -= 1;
        return (double)Sa * (persent * persent * persent + 1) + StartPoint;
    }
}
