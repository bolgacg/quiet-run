using QuietRun;

namespace QuietRun.Tests;

/// LEAK: pressure falls below the run's expected envelope and stays there.
public class LeakTests
{
    private static List<Sample> SevenPercentLeakAt400() =>
        Simulator.Run(seed: 101, runOffset: 0, new FaultSpec(Verdict.Leak, 0.07, 400));

    [Fact]
    public void A_seven_percent_leak_is_flagged_as_LEAK_within_two_minutes()
    {
        var report = Trained.Check(SevenPercentLeakAt400());
        Assert.Equal(Verdict.Leak, report.Verdict);
        Assert.InRange(report.FlaggedAtS!.Value, 400, 520);
    }

    [Fact]
    public void The_LEAK_reason_says_pressure_is_below_normal_while_flow_holds()
    {
        var report = Trained.Check(SevenPercentLeakAt400());
        Assert.Contains("below what this method normally shows", report.Reason);
        Assert.Contains("Liquid is leaving the flow path", report.Reason);
    }

    [Fact]
    public void Fixed_limits_let_a_four_percent_leak_run_to_the_end()
    {
        var run = Simulator.Run(seed: 102, runOffset: 0, new FaultSpec(Verdict.Leak, 0.04, 300));
        Assert.Null(FixedLimitMonitor.Check(run, Trained.Limits));
        Assert.Equal(Verdict.Leak, Trained.Check(run).Verdict);
    }

    [Fact]
    public void Without_the_LEAK_rule_the_same_run_is_not_called_LEAK()
    {
        var report = Trained.Check(SevenPercentLeakAt400(), DetectorSettings.Chosen.Without(Verdict.Leak));
        Assert.NotEqual(Verdict.Leak, report.Verdict);
    }
}
