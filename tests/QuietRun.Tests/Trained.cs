using QuietRun;

namespace QuietRun.Tests;

/// <summary>One envelope and one pair of fixed limits, learned once from 40 healthy runs, shared by every test.</summary>
public static class Trained
{
    private static readonly Lazy<(Envelope Env, FixedLimits Limits)> Model = new(() =>
    {
        var runs = Simulator.HealthyRuns(40, seed: 7);
        return (Envelope.Learn(runs), FixedLimits.Learn(runs));
    });

    public static Envelope Envelope => Model.Value.Env;
    public static FixedLimits Limits => Model.Value.Limits;

    public static RunReport Check(IEnumerable<Sample> run, DetectorSettings? settings = null) =>
        RunChecker.Check(run, Envelope, settings ?? DetectorSettings.Chosen);
}
