using QuietRun;

namespace QuietRun.Tests;

/// The monitor is a list of rules, and it reads samples from any source.
public class CompositionTests
{
    /// A rule written outside the library: the pump's flow reads below 95 of its set 100.
    private sealed class LowFlowRule : IRule
    {
        public Verdict Kind => Verdict.Leak;
        public string? Check(in RuleContext x) => x.Sample.Flow < 95 ? "Flow reads below 95." : null;
    }

    [Fact]
    public void A_rule_written_outside_the_library_plugs_in_and_fires_once()
    {
        var run = Simulator.Run(seed: 703, runOffset: 0).Select(s => s.T >= 500 ? s with { Flow = 90 } : s);
        var m = new RunMonitor(Trained.Envelope, [new LowFlowRule()]);
        foreach (var s in run) m.Push(s);
        var alarm = Assert.Single(m.Alarms);
        Assert.Equal(500, alarm.T);
        Assert.Equal("Flow reads below 95.", alarm.Reason);
    }

    [Fact]
    public void A_rule_switched_off_in_the_setting_is_left_out_of_the_monitor()
    {
        var inRun = RuleSet.InRun(Trained.Envelope, DetectorSettings.Chosen.Without(Verdict.Air)).Select(r => r.Kind);
        Assert.Equal(new[] { Verdict.Quiet, Verdict.Leak, Verdict.Clog }, inRun);
        Assert.Empty(RuleSet.AcrossRuns(Trained.Envelope, DetectorSettings.Chosen.Without(Verdict.Drift)));
    }

    [Fact]
    public void A_CSV_file_and_the_simulator_are_read_the_same_way()
    {
        var sim = new SimulatedSource(904, 0, new FaultSpec(Verdict.Clog, 0.10, 400));
        var path = Path.Combine(Path.GetTempPath(), $"quiet-run-{Guid.NewGuid():N}.csv");
        try
        {
            TraceCsv.Write(path, sim.Samples());
            var fromSim = RunChecker.Check(sim, Trained.Envelope, DetectorSettings.Chosen);
            var fromCsv = RunChecker.Check(new CsvSource(path), Trained.Envelope, DetectorSettings.Chosen);
            Assert.Equal(Verdict.Clog, fromSim.Verdict);
            Assert.Equal(fromSim.Verdict, fromCsv.Verdict);
            Assert.Equal(fromSim.FlaggedAtS, fromCsv.FlaggedAtS);
        }
        finally { File.Delete(path); }
    }
}
