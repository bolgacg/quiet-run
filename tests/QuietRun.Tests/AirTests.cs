using QuietRun;

namespace QuietRun.Tests;

/// AIR: a burst of short pressure swings while the average holds.
public class AirTests
{
    private static List<Sample> EightPercentBubblesAt500() =>
        Simulator.Run(seed: 301, runOffset: 0, new FaultSpec(Verdict.Air, 0.08, 500));

    [Fact]
    public void A_burst_of_eight_percent_dips_is_flagged_as_AIR_during_the_burst()
    {
        var report = Trained.Check(EightPercentBubblesAt500());
        Assert.Equal(Verdict.Air, report.Verdict);
        Assert.InRange(report.FlaggedAtS!.Value, 500, 500 + Simulator.AirBurstS + 6);
        Assert.Contains("short pressure dips", report.Reason);
    }

    [Fact]
    public void One_sensor_glitch_is_not_AIR()
    {
        var run = Simulator.Run(seed: 302, runOffset: 0);
        run[600] = run[600] with { Pressure = run[600].Pressure - 4 };
        var report = Trained.Check(run);
        Assert.Equal(Verdict.Ok, report.Verdict);
    }

    [Fact]
    public void A_fast_leak_does_not_count_as_short_swings()
    {
        var run = Simulator.Run(seed: 303, runOffset: 0, new FaultSpec(Verdict.Leak, 0.12, 400));
        var report = Trained.Check(run);
        Assert.Equal(Verdict.Leak, report.Verdict);
        Assert.DoesNotContain(report.Alarms, a => a.Kind == Verdict.Air);
    }

    [Fact]
    public void Without_the_AIR_rule_the_same_run_is_not_called_AIR()
    {
        var report = Trained.Check(EightPercentBubblesAt500(), DetectorSettings.Chosen.Without(Verdict.Air));
        Assert.NotEqual(Verdict.Air, report.Verdict);
    }
}
