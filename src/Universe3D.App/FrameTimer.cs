using System.Diagnostics;

namespace Universe3D.App;

/// <summary>Keeps the last 600 intervals between rendered frames, for a median frame time.</summary>
public sealed class FrameTimer
{
    private const int Capacity = 600;
    private readonly double[] _ms = new double[Capacity];
    private readonly Stopwatch _sw = new();
    private int _n;

    public int Count => Math.Min(_n, Capacity);

    public void Reset() { _n = 0; _sw.Reset(); }

    public void Tick()
    {
        if (_sw.IsRunning) _ms[_n++ % Capacity] = _sw.Elapsed.TotalMilliseconds;
        _sw.Restart();
    }

    public double MedianMs
    {
        get
        {
            var a = Samples.OrderBy(x => x).ToArray();
            return a.Length == 0 ? 0 : a[a.Length / 2];
        }
    }

    public IReadOnlyList<double> Samples => _ms.Take(Count).ToArray();
}
