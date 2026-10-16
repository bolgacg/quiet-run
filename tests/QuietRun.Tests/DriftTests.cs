using QuietRun;

namespace QuietRun.Tests;

/// DRIFT: a slow trend across runs that no single run shows.
public class DriftTests
{
    /// Twenty runs; from run 5 on, pressure creeps up by perRun each run.
    private static List<RunReport> Sequence(double perRun, int seed, DetectorSettings? settings = null)
    {
        var s = settings ?? DetectorSettings.Chosen;
        var drift = new DriftMonitor(Trained.Envelope, s);
        var rng = new Random(seed);
        var reports = new List<RunReport>();
        for (int i = 0; i < 20; i++)
        {
            double creep = i >= 5 ? perRun * (i - 4) : 0;
            var run = Simulator.Run(rng.Next(), Simulator.DrawRunOffset(rng), driftOffset: creep);
            reports.Add(RunChecker.Check(run, Trained.Envelope, s, drift));
        }
        return reports;
    }

    [Fact]
    public void A_clean_trend_of_half_a_percent_per_run_is_DRIFT_before_any_single_run_trips_CLOG()
    {
        var env = Trained.Envelope;
        var drift = new DriftMonitor(env, DetectorSettings.Chosen);
        var reports = new List<RunReport>();
        for (int i = 0; i < 20; i++)
        {
            double creep = i >= 5 ? 0.005 * (i - 4) : 0;
            reports.Add(RunChecker.Check(Simulator.Run(800 + i, runOffset: 0, driftOffset: creep), env, DetectorSettings.Chosen, drift));
        }
        int first = reports.FindIndex(r => r.Verdict != Verdict.Ok);
        Assert.InRange(first, 5, 5 + 8);
        Assert.Equal(Verdict.Drift, reports[first].Verdict);
        Assert.Contains("per run", reports[first].Reason);
    }

    [Fact]
    public void With_run_to_run_noise_a_slow_trend_is_still_caught_by_DRIFT_or_CLOG()
    {
        // Measured, not hoped: once one run lands high by chance, the in-run CLOG rule can fire first.
        var reports = Sequence(0.005, seed: 501);
        int first = reports.FindIndex(r => r.Verdict != Verdict.Ok);
        Assert.InRange(first, 5, 5 + 10);
        Assert.Contains(reports[first].Verdict, new[] { Verdict.Drift, Verdict.Clog });
    }

    [Fact]
    public void One_unusual_run_is_not_a_trend()
    {
        var env = Trained.Envelope;
        var m = new DriftMonitor(env, DetectorSettings.Chosen);
        foreach (var z in new[] { 0.0, 0.0, 3.0, 0.0, 0.0, 0.0 })
            Assert.Null(m.AddRun(z * env.RunSigma, 1260));
    }

    [Fact]
    public void A_steady_step_of_one_run_spread_is_flagged_within_eleven_runs()
    {
        var env = Trained.Envelope;
        var m = new DriftMonitor(env, DetectorSettings.Chosen);
        int? firedAt = null;
        for (int i = 1; i <= 11 && firedAt is null; i++)
            if (m.AddRun(1.0 * env.RunSigma, 1260) is not null) firedAt = i;
        Assert.NotNull(firedAt);
    }

    [Fact]
    public void A_flat_sequence_is_never_DRIFT()
    {
        var env = Trained.Envelope;
        var drift = new DriftMonitor(env, DetectorSettings.Chosen);
        for (int i = 0; i < 20; i++)
        {
            var report = RunChecker.Check(Simulator.Run(600 + i, runOffset: 0), env, DetectorSettings.Chosen, drift);
            Assert.DoesNotContain(report.Alarms, a => a.Kind == Verdict.Drift);
        }
    }

    [Fact]
    public void Without_the_DRIFT_rule_the_same_sequence_is_never_called_DRIFT()
    {
        var reports = Sequence(0.005, seed: 501, DetectorSettings.Chosen.Without(Verdict.Drift));
        Assert.DoesNotContain(reports, r => r.Alarms.Any(a => a.Kind == Verdict.Drift));
    }
}
