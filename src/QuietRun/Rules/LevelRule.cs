using System.Globalization;

namespace QuietRun;

/// <summary>
/// What LEAK and CLOG share: the smoothed pressure sits past the envelope, on one side, for a set time.
/// The time is measured from the timestamp of the first sample past the threshold.
/// </summary>
public abstract class LevelRule : IRule
{
    protected static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private readonly double _holdS;
    private double _since = double.NaN;

    /// <param name="threshold">How far from the envelope, as a fraction, the smoothed pressure must sit.</param>
    /// <param name="holdS">For how many seconds in a row.</param>
    protected LevelRule(double threshold, double holdS)
    {
        Threshold = threshold;
        _holdS = holdS;
    }

    public abstract Verdict Kind { get; }

    protected double Threshold { get; }

    /// <summary>True when the deviation is past the threshold on this rule's side.</summary>
    protected abstract bool IsPast(double deviation);

    protected abstract string Reason(in RuleContext x, double heldS);

    public string? Check(in RuleContext x)
    {
        if (!x.Comparable) return null;
        if (!IsPast(x.Deviation))
        {
            _since = double.NaN;
            return null;
        }
        if (double.IsNaN(_since)) _since = x.Sample.T;
        double held = x.Sample.T - _since;
        return held >= _holdS ? Reason(x, held) : null;
    }
}

/// <summary>LEAK: the smoothed pressure sits below the envelope for a set time.</summary>
public sealed class LeakRule(double threshold, double holdS) : LevelRule(threshold, holdS)
{
    public override Verdict Kind => Verdict.Leak;

    protected override bool IsPast(double deviation) => deviation < -Threshold;

    protected override string Reason(in RuleContext x, double heldS) => string.Format(Inv,
        "Pressure has sat {0:0.0}% below what this method normally shows at {1} for {2:0} s, while the pump's flow reads {3:0.0} of its set 100. Liquid is leaving the flow path somewhere.",
        -x.Deviation * 100, VerdictText.Clock(x.CentreMethodS), heldS, x.Sample.Flow);
}

/// <summary>CLOG: the smoothed pressure sits above the envelope for a set time.</summary>
public sealed class ClogRule(double threshold, double holdS) : LevelRule(threshold, holdS)
{
    public override Verdict Kind => Verdict.Clog;

    protected override bool IsPast(double deviation) => deviation > Threshold;

    protected override string Reason(in RuleContext x, double heldS) => string.Format(Inv,
        "Pressure has sat {0:0.0}% above what this method normally shows at {1} for {2:0} s, while the pump's flow reads {3:0.0} of its set 100. Something in the flow path is narrowing.",
        x.Deviation * 100, VerdictText.Clock(x.CentreMethodS), heldS, x.Sample.Flow);
}
