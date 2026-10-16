using QuietRun;

namespace QuietRun.Tests;

/// Time comes from the samples' own timestamps. These tests measure what jitter, lost readings, gaps and other rates do to a verdict.
public class TimingTests
{
    public static IEnumerable<object[]> Kinds() =>
        new[] { "healthy", "leak", "clog", "quiet" }.Select(k => new object[] { k });

    private static FaultSpec? Fault(string kind) => kind switch
    {
        "healthy" => null,
        "leak" => new FaultSpec(Verdict.Leak, 0.07, 400),
        "clog" => new FaultSpec(Verdict.Clog, 0.10, 400),
        "air" => new FaultSpec(Verdict.Air, 0.08, 500),
        "quiet" => new FaultSpec(Verdict.Quiet, double.PositiveInfinity, 600),
        _ => throw new ArgumentException(kind),
    };

    /// Twenty runs of one kind, sampled once a second.
    private static IEnumerable<(int Seed, List<Sample> Run)> Runs(string kind) =>
        Enumerable.Range(1000, 20).Select(seed => (seed, Simulator.Run(seed, runOffset: 0, Fault(kind))));

    private static List<Sample> Run(string kind) => Runs(kind).First().Run;

    private static void AssertSameReport(RunReport expected, RunReport actual, double toleranceS, int seed)
    {
        Assert.True(expected.Verdict == actual.Verdict, $"seed {seed}: {expected.Verdict}, then {actual.Verdict}");
        if (expected.FlaggedAtS is null) { Assert.Null(actual.FlaggedAtS); return; }
        Assert.InRange(actual.FlaggedAtS!.Value, expected.FlaggedAtS.Value - toleranceS, expected.FlaggedAtS.Value + toleranceS);
    }

    [Theory]
    [MemberData(nameof(Kinds))]
    public void Timestamps_jittered_by_up_to_300_ms_give_the_same_verdict_within_3_s_on_20_runs(string kind)
    {
        foreach (var (seed, run) in Runs(kind))
            AssertSameReport(Trained.Check(run), Trained.Check(Resample.Jitter(run, 0.3, seed)), 3, seed);
    }

    [Theory]
    [MemberData(nameof(Kinds))]
    public void Losing_one_reading_in_five_gives_the_same_verdict_within_6_s_on_20_runs(string kind)
    {
        foreach (var (seed, run) in Runs(kind))
            AssertSameReport(Trained.Check(run), Trained.Check(Resample.Drop(run, 0.2, seed)), 6, seed);
    }

    [Fact]
    public void AIR_runs_keep_their_verdict_in_at_least_19_of_20_when_readings_jitter_or_one_in_five_is_lost()
    {
        // Measured, not hoped. AIR is the sensitive rule: a dip lasts 2 to 4 s, so losing readings can erase one,
        // and a run with exactly three swings near the edge of the 90 s window flips when a timestamp moves.
        int jitterKept = 0, dropKept = 0;
        foreach (var (seed, run) in Runs("air"))
        {
            var clean = Trained.Check(run).Verdict;
            if (Trained.Check(Resample.Jitter(run, 0.3, seed)).Verdict == clean) jitterKept++;
            if (Trained.Check(Resample.Drop(run, 0.2, seed)).Verdict == clean) dropKept++;
        }
        Assert.InRange(jitterKept, 19, 20);
        Assert.InRange(dropKept, 19, 20);
    }

    [Fact]
    public void A_twenty_second_gap_in_the_readings_raises_no_alarm_on_healthy_runs()
    {
        for (int seed = 700; seed < 710; seed++)
        {
            var report = Trained.Check(Resample.Gap(Simulator.Run(seed, runOffset: 0), 600, 20));
            Assert.True(report.Verdict == Verdict.Ok, $"seed {seed}: {report.Verdict} {report.Reason}");
        }
    }

    [Fact]
    public void At_one_reading_every_two_seconds_a_stall_is_QUIET_after_30_s_not_60()
    {
        var run = Resample.EveryNth(Run("quiet"), 2);
        var report = Trained.Check(run);
        Assert.Equal(Verdict.Quiet, report.Verdict);
        Assert.InRange(report.FlaggedAtS!.Value, 600 + 29, 600 + 34);
        Assert.Contains("its heartbeat arrives every 2 s", report.Reason);
    }

    [Fact]
    public void At_two_readings_a_second_a_leak_is_flagged_when_it_is_at_one()
    {
        var run = Run("leak");
        var report = Trained.Check(Resample.Doubled(run));
        AssertSameReport(Trained.Check(run), report, 2, 1000);
        Assert.Contains("for 20 s", report.Reason);
    }

    [Fact]
    public void A_reading_out_of_time_order_is_refused_not_misread()
    {
        var run = Run("healthy");
        (run[100], run[101]) = (run[101], run[100]);
        var e = Assert.Throws<ArgumentException>(() => Trained.Check(run));
        Assert.Contains("not after the previous one", e.Message);
    }
}
