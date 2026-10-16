using QuietRun;

namespace QuietRun.Tests;

public class EnvelopeTests
{
    [Fact]
    public void The_learned_envelope_follows_the_healthy_shape()
    {
        var env = Trained.Envelope;
        for (int m = 30; m < Simulator.RunSeconds - 30; m += 5)
            Assert.InRange(env.ExpectedAt(m) - Simulator.Shape(m), -1.0, 1.0);
    }

    [Fact]
    public void The_learned_spreads_are_in_a_sensible_order()
    {
        var env = Trained.Envelope;
        Assert.InRange(env.LevelSigma, 0.004, 0.03);
        Assert.InRange(env.RunSigma, 0.004, 0.03);
        Assert.InRange(env.SpikeSigma, 0.0005, env.LevelSigma);
    }

    [Fact]
    public void Expected_pressure_is_interpolated_between_bins()
    {
        var env = new Envelope { BinSeconds = 10, Expected = [100, 110] };
        Assert.Equal(100, env.ExpectedAt(5), 6);
        Assert.Equal(105, env.ExpectedAt(10), 6);
        Assert.Equal(110, env.ExpectedAt(15), 6);
    }

    [Fact]
    public void Fixed_limits_sit_outside_the_whole_healthy_range()
    {
        var lim = Trained.Limits;
        double lo = Enumerable.Range(0, Simulator.RunSeconds).Min(t => Simulator.Shape(t));
        double hi = Enumerable.Range(0, Simulator.RunSeconds).Max(t => Simulator.Shape(t));
        Assert.True(lim.Low < lo && lim.High > hi);
    }
}
