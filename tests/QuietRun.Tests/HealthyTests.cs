using QuietRun;

namespace QuietRun.Tests;

/// OK: a healthy run raises nothing.
public class HealthyTests
{
    public static IEnumerable<object[]> Settings() => DetectorSettings.All.Select(s => new object[] { s.Name });

    [Theory]
    [MemberData(nameof(Settings))]
    public void Healthy_runs_at_the_average_stay_OK(string setting)
    {
        var s = DetectorSettings.ByName(setting);
        for (int seed = 700; seed < 710; seed++)
        {
            var report = Trained.Check(Simulator.Run(seed, runOffset: 0), s);
            Assert.True(report.Verdict == Verdict.Ok, $"seed {seed}: {report.Verdict} {report.Reason}");
            Assert.Equal(RunChecker.OkReason, report.Reason);
        }
    }

    [Fact]
    public void Fixed_limits_stay_quiet_on_healthy_runs()
    {
        for (int seed = 720; seed < 740; seed++)
            Assert.Null(FixedLimitMonitor.Check(Simulator.Run(seed, runOffset: 0), Trained.Limits));
    }
}
