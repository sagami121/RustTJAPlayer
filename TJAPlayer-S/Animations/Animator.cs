using System;

namespace TjaPlayer.Animations;

internal class Animator : IAnimatable
{
    public Animator(double startValue, double endValue, double tickInterval, bool isLoop, System.Diagnostics.Stopwatch timer)
    {
        StartValue = startValue;
        EndValue = endValue;
        TickInterval = tickInterval;
        IsLoop = isLoop;
        this.timer = timer;
        Counter = new CCounter();
    }

    public void Start()
    {
        Counter.t開始((double)StartValue, (double)EndValue, (double)TickInterval, timer);
    }

    public void Stop()
    {
        Counter.t停止();
    }

    public void Reset()
    {
        Start();
    }

    public void Tick()
    {
        Counter.t進行();
    }

    public virtual object GetAnimation()
    {
        throw new NotImplementedException();
    }

    protected CCounter Counter { get; private set; }
    protected object StartValue { get; private set; }
    protected object EndValue { get; private set; }
    protected object TickInterval { get; private set; }
    protected bool IsLoop { get; private set; }
    private System.Diagnostics.Stopwatch timer;
}
