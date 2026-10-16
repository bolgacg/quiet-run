using System.Globalization;

namespace QuietRun;

/// <summary>
/// The alternative an instrument would otherwise use: one fixed low and one fixed high pressure limit for the
/// whole run. Learned here as tightly as possible: the lowest and highest smoothed pressure seen in healthy
/// runs, widened by a small margin, which is stricter than most real setups.
/// </summary>
public sealed record FixedLimits(double Low, double High)
{
    /// <summary>
    /// Seconds the pressure must stay past a limit, by the samples' timestamps, before it trips. At one sample a
    /// second that is three samples in a row.
    /// </summary>
    public const double DebounceS = 2;

    /// <summary>Seconds of the median used to find the healthy extremes when learning the limits.</summary>
    public const double LearnSmoothS = 5;

    /// <summary>How far outside the healthy extremes the limits sit, in index points.</summary>
    public const double DefaultMargin = 2.0;

    public static FixedLimits Learn(IReadOnlyList<IReadOnlyList<Sample>> healthyRuns, double marginIndexPoints = DefaultMargin)
    {
        double lo = double.MaxValue, hi = double.MinValue;
        foreach (var run in healthyRuns)
        {
            var window = new SampleWindow(LearnSmoothS);
            foreach (var s in run)
            {
                window.Push(s);
                if (!window.Ready) continue;
                double m = window.MedianPressure();
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
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private readonly FixedLimits _limits;
    private double _belowSince = double.NaN, _aboveSince = double.NaN;

    public FixedLimitMonitor(FixedLimits limits) => _limits = limits;

    public Alarm? Fired { get; private set; }

    public Alarm? Push(Sample s)
    {
        if (Fired is not null || s.Status != InstrumentStatus.Running) return null;
        _belowSince = s.Pressure < _limits.Low ? (double.IsNaN(_belowSince) ? s.T : _belowSince) : double.NaN;
        _aboveSince = s.Pressure > _limits.High ? (double.IsNaN(_aboveSince) ? s.T : _aboveSince) : double.NaN;
        if (s.T - _belowSince >= FixedLimits.DebounceS)
            Fired = new Alarm(Verdict.Leak, s.T, string.Format(Inv,
                "Pressure {0:0.0} has been under the fixed low limit of {1:0.0} for {2:0} s.", s.Pressure, _limits.Low, s.T - _belowSince));
        else if (s.T - _aboveSince >= FixedLimits.DebounceS)
            Fired = new Alarm(Verdict.Clog, s.T, string.Format(Inv,
                "Pressure {0:0.0} has been over the fixed high limit of {1:0.0} for {2:0} s.", s.Pressure, _limits.High, s.T - _aboveSince));
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
