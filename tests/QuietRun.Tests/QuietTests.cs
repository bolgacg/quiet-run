using QuietRun;

namespace QuietRun.Tests;

/// QUIET: the instrument reports alive, but the run has stopped progressing. Progress, not presence.
public class QuietTests
{
    private static List<Sample> StallFrom600() =>
        Simulator.Run(seed: 401, runOffset: 0, new FaultSpec(Verdict.Quiet, double.PositiveInfinity, 600));

    [Fact]
    public void A_stalled_method_clock_is_flagged_as_QUIET_thirty_seconds_in()
    {
        var report = Trained.Check(StallFrom600());
        Assert.Equal(Verdict.Quiet, report.Verdict);
        Assert.InRange(report.FlaggedAtS!.Value, 600 + 29, 600 + 32);
        Assert.Contains("method clock has stood", report.Reason);
    }

    [Fact]
    public void During_the_stall_every_presence_check_still_passes()
    {
        var run = StallFrom600();
        Assert.All(run, s => Assert.Equal(InstrumentStatus.Running, s.Status));
        for (int i = 1; i < run.Count; i++) Assert.True(run[i].Heartbeat > run[i - 1].Heartbeat);
    }

    [Fact]
    public void Fixed_pressure_limits_never_see_a_stall()
    {
        Assert.Null(FixedLimitMonitor.Check(StallFrom600(), Trained.Limits));
    }

    [Fact]
    public void A_twelve_second_pause_is_normal_and_not_QUIET()
    {
        var run = Simulator.Run(seed: 402, runOffset: 0, new FaultSpec(Verdict.Quiet, 12, 600));
        Assert.Equal(Verdict.Ok, Trained.Check(run).Verdict);
    }

    [Fact]
    public void A_lost_heartbeat_is_not_reported_as_QUIET()
    {
        // A silent instrument is a presence failure, which a heartbeat check already catches.
        var run = StallFrom600().Select(s => s.T >= 600 ? s with { Heartbeat = 600 } : s).ToList();
        Assert.NotEqual(Verdict.Quiet, Trained.Check(run).Verdict);
    }

    [Fact]
    public void Without_the_QUIET_rule_the_same_run_is_not_called_QUIET()
    {
        var report = Trained.Check(StallFrom600(), DetectorSettings.Chosen.Without(Verdict.Quiet));
        Assert.NotEqual(Verdict.Quiet, report.Verdict);
    }
}
