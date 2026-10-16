using QuietRun;

namespace QuietRun.Tests;

public class CsvAndSimulatorTests
{
    [Fact]
    public void The_same_seed_gives_the_same_run()
    {
        var a = Simulator.Run(42, 0.01, new FaultSpec(Verdict.Air, 0.08, 300));
        var b = Simulator.Run(42, 0.01, new FaultSpec(Verdict.Air, 0.08, 300));
        Assert.Equal(a, b);
    }

    [Fact]
    public void The_healthy_shape_averages_100()
    {
        double mean = Enumerable.Range(0, Simulator.RunSeconds).Average(t => Simulator.Shape(t));
        Assert.Equal(100, mean, 6);
    }

    [Fact]
    public void A_trace_written_to_CSV_reads_back_with_the_same_verdict()
    {
        var run = Simulator.Run(901, 0, new FaultSpec(Verdict.Leak, 0.07, 400));
        var path = Path.Combine(Path.GetTempPath(), $"quiet-run-{Guid.NewGuid():N}.csv");
        try
        {
            TraceCsv.Write(path, run);
            var back = TraceCsv.Read(path);
            Assert.Equal(run.Count, back.Count);
            Assert.Equal(Trained.Check(run).Verdict, Trained.Check(back).Verdict);
            Assert.Equal(run[500].Pressure, back[500].Pressure, 3);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void A_CSV_without_a_method_clock_is_refused_with_a_clear_message()
    {
        var lines = new[] { "t_s,pressure,flow,status,heartbeat", "0,100,100,Running,1" };
        var e = Assert.Throws<TraceFormatException>(() => TraceCsv.Parse(lines));
        Assert.Contains("method_s", e.Message);
    }

    [Fact]
    public void An_envelope_survives_a_round_trip_through_JSON()
    {
        var path = Path.Combine(Path.GetTempPath(), $"quiet-run-{Guid.NewGuid():N}.json");
        try
        {
            TraceCsv.WriteEnvelope(path, Trained.Envelope);
            var back = TraceCsv.ReadEnvelope(path);
            Assert.Equal(Trained.Envelope.Expected, back.Expected);
            Assert.Equal(Trained.Envelope.LevelSigma, back.LevelSigma);
        }
        finally { File.Delete(path); }
    }
}
