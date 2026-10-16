using System.Globalization;

namespace QuietRun;

/// <summary>
/// AIR: several short pressure swings within a time window while the average holds, the pattern of gas bubbles
/// passing through the pump. Swing lengths and the window are measured by the samples' timestamps.
/// </summary>
public sealed class AirRule : IRule
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private readonly double _threshold, _windowS, _maxSwingS;
    private readonly int _count;

    private bool _inSwing;
    private double _swingStart, _swingDepth;
    private int _swingSign;
    private readonly RingBuffer<(double End, int Sign, double Depth)> _swings = new(8);

    /// <param name="threshold">How far, as a fraction of expected pressure, one sample must jump from the smoothed pressure to start a swing.</param>
    /// <param name="count">How many short swings must fall inside the window.</param>
    /// <param name="windowS">The window, in seconds.</param>
    /// <param name="maxSwingS">A swing longer than this is not short and does not count.</param>
    public AirRule(double threshold, int count, double windowS, double maxSwingS)
    {
        _threshold = threshold;
        _count = count;
        _windowS = windowS;
        _maxSwingS = maxSwingS;
    }

    public Verdict Kind => Verdict.Air;

    public string? Check(in RuleContext x)
    {
        if (!x.Comparable) return null;
        double t = x.Sample.T, swing = x.Swing;

        // A swing starts above the threshold and only ends once it falls back under half of it, so a slow ramp
        // hovering near the threshold counts as one long swing, not many short ones.
        if (!_inSwing && Math.Abs(swing) > _threshold)
        {
            _inSwing = true; _swingStart = t; _swingSign = Math.Sign(swing); _swingDepth = 0;
        }
        if (_inSwing && Math.Abs(swing) >= _threshold / 2)
        {
            _swingDepth = Math.Max(_swingDepth, Math.Abs(swing));
        }
        else if (_inSwing)
        {
            _inSwing = false;
            if (t - _swingStart <= _maxSwingS) _swings.Push((t, _swingSign, _swingDepth));
        }

        while (_swings.Count > 0 && _swings.First.End < t - _windowS) _swings.PopFront();
        if (_swings.Count < _count) return null;

        int dips = _swings.Count(w => w.Sign < 0);
        string what = dips * 2 >= _swings.Count ? "dips" : "spikes";
        double span = _swings.Last.End - _swings.First.End;
        double deepest = _swings.Max(w => w.Depth) * 100;
        return string.Format(Inv,
            "{0} short pressure {1} in {2:0} s, each shorter than {3:0} s and the deepest {4:0.0}%, while the average pressure held. That is the pattern of gas bubbles passing through the pump.",
            _swings.Count, what, span, _maxSwingS, deepest);
    }
}
