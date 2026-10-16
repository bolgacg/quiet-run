namespace QuietRun;

/// <summary>
/// The alternative an instrument would otherwise use: one fixed low and one fixed high pressure limit for the
/// whole run. Learned here as tightly as possible: the lowest and highest smoothed pressure seen in healthy
/// runs, widened by a small margin, which is stricter than most real setups.
/// </summary>
public sealed record FixedLimits(double Low, double High)
{
    /// <summary>Samples in a row that must sit outside a limit before it trips.</summary>
    public const int Debounce = 3;

    /// <summary>How far outside the healthy extremes the limits sit, in index points.</summary>
    public const double DefaultMargin = 2.0;

    public static FixedLimits Learn(IReadOnlyList<IReadOnlyList<Sample>> healthyRuns, double marginIndexPoints = DefaultMargin)
    {
        double lo = double.MaxValue, hi = double.MinValue;
        foreach (var run in healthyRuns)
        {
            var med = new RollingMedian(5);
            foreach (var s in run)
            {
                double m = med.Push(s.Pressure);
                if (!med.Full) continue;
                lo = Math.Min(lo, m);
                hi = Math.Max(hi, m);
            }
        }
        return new FixedLimits(lo - marginIndexPoints, hi + marginIndexPoints);
    }
}

/// <summary>Runs the fixed limits over a run, sample by sample. Fires once.</summary>
public sealed class FixedLimitMonitor
{
    private readonly FixedLimits _limits;
    private int _below, _above;

    public FixedLimitMonitor(FixedLimits limits) => _limits = limits;

    public Alarm? Fired { get; private set; }

    public Alarm? Push(Sample s)
    {
        if (Fired is not null || s.Status != InstrumentStatus.Running) return null;
        _below = s.Pressure < _limits.Low ? _below + 1 : 0;
        _above = s.Pressure > _limits.High ? _above + 1 : 0;
        if (_below >= FixedLimits.Debounce)
            Fired = new Alarm(Verdict.Leak, s.T,
                $"Pressure {s.Pressure:0.0} has been under the fixed low limit of {_limits.Low:0.0} for {FixedLimits.Debounce} s.");
        else if (_above >= FixedLimits.Debounce)
            Fired = new Alarm(Verdict.Clog, s.T,
                $"Pressure {s.Pressure:0.0} has been over the fixed high limit of {_limits.High:0.0} for {FixedLimits.Debounce} s.");
        return Fired;
    }

    public static Alarm? Check(IEnumerable<Sample> run, FixedLimits limits)
    {
        var m = new FixedLimitMonitor(limits);
        foreach (var s in run)
            if (m.Push(s) is { } a) return a;
        return null;
    }
}
