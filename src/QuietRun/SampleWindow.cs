namespace QuietRun;

/// <summary>
/// The samples of the last few seconds, chosen by the samples' own timestamps, not by counting samples.
/// At one sample a second a 15-second window holds 15 samples; at two a second it holds 30; with dropped
/// samples it holds fewer. It gives the median pressure and the method position at the window's middle.
/// </summary>
public sealed class SampleWindow
{
    private readonly RingBuffer<Sample> _buf = new(32);
    private double[] _scratch = new double[32];
    private double _firstT = double.NaN;
    private long _seen;
    private bool _filled;

    /// <param name="seconds">The window keeps samples newer than this many seconds before the latest one.</param>
    public SampleWindow(double seconds)
    {
        if (!(seconds > 0)) throw new ArgumentOutOfRangeException(nameof(seconds));
        Seconds = seconds;
    }

    public double Seconds { get; }

    public int Count => _buf.Count;

    /// <summary>
    /// True once the run's samples cover the whole window, counting each sample as one average sampling interval
    /// (at one sample a second, a 15-second window is ready at the 15th sample), and while the samples in the
    /// window still span at least half of it. Right after a long gap in the data it is false until it refills.
    /// </summary>
    public bool Ready => _filled && _buf.Last.T - _buf.First.T >= Seconds / 2;

    public void Push(Sample s)
    {
        if (_buf.Count > 0 && !(s.T > _buf.Last.T))
            throw new ArgumentException($"Sample at t = {s.T} s is not after the previous one at t = {_buf.Last.T} s.", nameof(s));
        _buf.Push(s);
        while (_buf.First.T <= s.T - Seconds) _buf.PopFront();

        if (_seen++ == 0) _firstT = s.T;
        if (!_filled && _seen >= 2)
        {
            double covered = (s.T - _firstT) * _seen / (_seen - 1);
            _filled = covered >= Seconds - 1e-9;
        }
    }

    /// <summary>Median pressure of the samples in the window.</summary>
    public double MedianPressure()
    {
        int n = _buf.Count;
        if (n == 0) return double.NaN;
        if (_scratch.Length < n) _scratch = new double[Math.Max(n, _scratch.Length * 2)];
        for (int i = 0; i < n; i++) _scratch[i] = _buf[i].Pressure;
        Array.Sort(_scratch, 0, n);
        return n % 2 == 1 ? _scratch[n / 2] : 0.5 * (_scratch[n / 2 - 1] + _scratch[n / 2]);
    }

    /// <summary>
    /// The method position at the middle of the window in time, interpolated between the two samples around it.
    /// The median lags the newest sample by about half the window, so this is the method position it belongs to.
    /// </summary>
    public double CentreMethodS()
    {
        int n = _buf.Count;
        if (n == 0) return double.NaN;
        double mid = 0.5 * (_buf.First.T + _buf.Last.T);
        int i = 0;
        while (i + 1 < n && _buf[i + 1].T <= mid) i++;
        var a = _buf[i];
        if (a.T == mid || i + 1 == n) return a.MethodS;
        var b = _buf[i + 1];
        double f = (mid - a.T) / (b.T - a.T);
        return a.MethodS + f * (b.MethodS - a.MethodS);
    }
}
