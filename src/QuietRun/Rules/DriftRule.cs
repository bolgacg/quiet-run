using System.Globalization;

namespace QuietRun;

/// <summary>
/// DRIFT: a slow trend across runs that no single run shows. It is a two-sided cumulative sum (CUSUM, Page 1954)
/// of each run's median deviation, measured in run-to-run spreads.
/// </summary>
public sealed class DriftRule : ISequenceRule
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private readonly double _runSigma, _allowance, _limit;
    private double _up, _down;
    private readonly List<double> _upRuns = [], _downRuns = [];

    /// <param name="runSigma">Spread of one whole run's median deviation across healthy runs, as a fraction.</param>
    /// <param name="allowance">Allowance per run, in run spreads, before the cumulative sum grows.</param>
    /// <param name="limit">The cumulative sum, in run spreads, at which the rule fires.</param>
    public DriftRule(double runSigma, double allowance, double limit)
    {
        _runSigma = runSigma;
        _allowance = allowance;
        _limit = limit;
    }

    public Verdict Kind => Verdict.Drift;

    public Alarm? AddRun(RunSummary run)
    {
        double medianDeviation = run.MedianDeviation;
        if (double.IsNaN(medianDeviation)) return null;
        double z = medianDeviation / _runSigma;
        _up = Math.Max(0, _up + z - _allowance);
        _down = Math.Max(0, _down - z - _allowance);
        if (_up > 0) _upRuns.Add(medianDeviation); else _upRuns.Clear();
        if (_down > 0) _downRuns.Add(medianDeviation); else _downRuns.Clear();

        List<double>? streak = _up > _limit ? _upRuns : _down > _limit ? _downRuns : null;
        if (streak is null) return null;

        string dir = streak == _upRuns ? "risen" : "fallen";
        double perRun = Stats.Slope(streak) * 100;
        string reason = streak.Count >= 2
            ? string.Format(Inv,
                "Across the last {0} runs the run-average pressure has {1} to {2:+0.0;-0.0}% of normal, about {3:+0.00;-0.00}% per run. This rule compares runs with each other, so it can fire while every run on its own looks normal.",
                streak.Count, dir, medianDeviation * 100, perRun)
            : string.Format(Inv,
                "This run's average pressure sits at {0:+0.0;-0.0}% of normal, far enough from the runs before it that the cross-run rule fires on one run.",
                medianDeviation * 100);
        var alarm = new Alarm(Verdict.Drift, run.EndT, reason);
        _up = _down = 0;
        _upRuns.Clear();
        _downRuns.Clear();
        return alarm;
    }
}
