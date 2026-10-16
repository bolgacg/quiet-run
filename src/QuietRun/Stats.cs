namespace QuietRun;

/// <summary>Small statistics helpers. Nothing here allocates per sample except the rolling median.</summary>
public static class Stats
{
    public static double Median(IReadOnlyList<double> xs)
    {
        if (xs.Count == 0) return double.NaN;
        var a = xs.ToArray();
        Array.Sort(a);
        int n = a.Length;
        return n % 2 == 1 ? a[n / 2] : 0.5 * (a[n / 2 - 1] + a[n / 2]);
    }

    public static double Mean(IReadOnlyList<double> xs) => xs.Count == 0 ? double.NaN : xs.Sum() / xs.Count;

    public static double StdDev(IReadOnlyList<double> xs)
    {
        if (xs.Count < 2) return double.NaN;
        double m = Mean(xs), ss = 0;
        foreach (var x in xs) ss += (x - m) * (x - m);
        return Math.Sqrt(ss / (xs.Count - 1));
    }

    /// <summary>Standard deviation estimated from the median absolute deviation, so rare spikes do not inflate it.</summary>
    public static double SigmaFromMad(IReadOnlyList<double> xs)
    {
        double med = Median(xs);
        var dev = xs.Select(x => Math.Abs(x - med)).ToArray();
        return 1.4826 * Median(dev);
    }

    /// <summary>Least-squares slope of ys against 0, 1, 2, ...</summary>
    public static double Slope(IReadOnlyList<double> ys)
    {
        int n = ys.Count;
        if (n < 2) return 0;
        double mx = (n - 1) / 2.0, my = Mean(ys), sxy = 0, sxx = 0;
        for (int i = 0; i < n; i++) { sxy += (i - mx) * (ys[i] - my); sxx += (i - mx) * (i - mx); }
        return sxy / sxx;
    }

    public static double Percentile(IReadOnlyList<double> xs, double p)
    {
        if (xs.Count == 0) return double.NaN;
        var a = xs.ToArray();
        Array.Sort(a);
        double pos = p * (a.Length - 1);
        int lo = (int)Math.Floor(pos), hi = (int)Math.Ceiling(pos);
        return a[lo] + (a[hi] - a[lo]) * (pos - lo);
    }
}

/// <summary>Median of the last N values pushed. N is small (15), so a sort per push is cheap.</summary>
public sealed class RollingMedian
{
    private readonly int _window;
    private readonly Queue<double> _q = new();
    private readonly double[] _buf;

    public RollingMedian(int window)
    {
        if (window < 1) throw new ArgumentOutOfRangeException(nameof(window));
        _window = window;
        _buf = new double[window];
    }

    public int Count => _q.Count;
    public bool Full => _q.Count == _window;

    public double Push(double x)
    {
        _q.Enqueue(x);
        if (_q.Count > _window) _q.Dequeue();
        int n = 0;
        foreach (var v in _q) _buf[n++] = v;
        Array.Sort(_buf, 0, n);
        return n % 2 == 1 ? _buf[n / 2] : 0.5 * (_buf[n / 2 - 1] + _buf[n / 2]);
    }
}
