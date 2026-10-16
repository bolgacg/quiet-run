using QuietRun;

namespace QuietRun.Tests;

/// CLOG: pressure rises above the run's expected envelope and stays there.
public class ClogTests
{
    private static List<Sample> TenPercentClogAt400() =>
        Simulator.Run(seed: 201, runOffset: 0, new FaultSpec(Verdict.Clog, 0.10, 400));

    [Fact]
    public void A_ten_percent_clog_is_flagged_as_CLOG_within_four_minutes()
    {
        var report = Trained.Check(TenPercentClogAt400());
        Assert.Equal(Verdict.Clog, report.Verdict);
        Assert.InRange(report.FlaggedAtS!.Value, 400, 640);
    }

    [Fact]
    public void The_CLOG_reason_says_pressure_is_above_normal()
    {
        var report = Trained.Check(TenPercentClogAt400());
        Assert.Contains("above what this method normally shows", report.Reason);
        Assert.Contains("narrowing", report.Reason);
    }

    [Fact]
    public void Fixed_limits_let_a_six_percent_clog_run_to_the_end()
    {
        var run = Simulator.Run(seed: 202, runOffset: 0, new FaultSpec(Verdict.Clog, 0.06, 700));
        Assert.Null(FixedLimitMonitor.Check(run, Trained.Limits));
        Assert.Equal(Verdict.Clog, Trained.Check(run).Verdict);
    }

    [Fact]
    public void Without_the_CLOG_rule_the_same_run_is_not_called_CLOG()
    {
        var report = Trained.Check(TenPercentClogAt400(), DetectorSettings.Chosen.Without(Verdict.Clog));
        Assert.NotEqual(Verdict.Clog, report.Verdict);
    }
}
