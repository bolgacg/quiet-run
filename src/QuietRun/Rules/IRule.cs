namespace QuietRun;

/// <summary>
/// One in-run rule. The monitor hands it every sample of one run, in time order, with what the envelope says
/// about that sample. A rule keeps whatever state it needs, and says when its condition holds. Whether that
/// becomes an alarm (each kind fires at most once a run) is the monitor's decision, not the rule's.
/// A rule only looks at samples it has already seen, so it can run live on an instrument.
/// </summary>
public interface IRule
{
    /// <summary>The verdict this rule gives.</summary>
    Verdict Kind { get; }

    /// <summary>Looks at one sample. Returns the plain-English reason when the rule's condition holds, else null.</summary>
    string? Check(in RuleContext x);
}

/// <summary>
/// One sample as the in-run rules see it.
/// </summary>
/// <param name="Sample">The raw sample.</param>
/// <param name="Comparable">True when the smoothing window is ready and the instrument reports Running, so the
/// pressure can be compared with the envelope. When false, the fields below are NaN.</param>
/// <param name="Smoothed">Median pressure over the smoothing window.</param>
/// <param name="CentreMethodS">Method position at the middle of the smoothing window.</param>
/// <param name="Expected">Expected pressure at that method position.</param>
/// <param name="Deviation">(Smoothed minus Expected) divided by Expected.</param>
/// <param name="Swing">(raw pressure minus Smoothed) divided by Expected: how far this one sample jumps.</param>
public readonly record struct RuleContext(
    Sample Sample,
    bool Comparable,
    double Smoothed,
    double CentreMethodS,
    double Expected,
    double Deviation,
    double Swing)
{
    public static RuleContext NotComparable(Sample s) =>
        new(s, false, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN);
}

/// <summary>
/// One cross-run rule. It sees each finished run's summary, in order, and may fire after any run.
/// It lives as long as the sequence of runs it watches, not one run.
/// </summary>
public interface ISequenceRule
{
    Verdict Kind { get; }

    /// <summary>Adds one finished run. Returns an alarm when the rule fires on it.</summary>
    Alarm? AddRun(RunSummary run);
}
