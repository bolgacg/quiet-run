namespace QuietRun;

/// <summary>
/// What a healthy run of one method looks like, learned from healthy runs only.
/// The expected pressure is indexed by method position, not by wall-clock time, so a run that pauses
/// is still compared with the right point of the method.
/// </summary>
public sealed class Envelope
{
    /// <summary>Length of the smoothing window in seconds, by the samples' timestamps.</summary>
    public const double SmoothSeconds = 15;

    public double BinSeconds { get; init; } = 10;

    /// <summary>Expected smoothed pressure per bin of method position.</summary>
    public double[] Expected { get; init; } = [];

    /// <summary>Spread of the smoothed pressure around Expected across healthy runs, as a fraction.</summary>
    public double LevelSigma { get; init; }

    /// <summary>Spread of raw minus smoothed pressure in healthy runs, as a fraction, estimated from the median absolute deviation so rare glitches do not inflate it.</summary>
    public double SpikeSigma { get; init; }

    /// <summary>Spread of one whole run's median deviation across healthy runs, as a fraction.</summary>
    public double RunSigma { get; init; }

    public int TrainingRuns { get; init; }

    /// <summary>Expected pressure at a method position, interpolated between bin centres.</summary>
    public double ExpectedAt(double methodS)
    {
        if (Expected.Length == 0) throw new InvalidOperationException("Empty envelope.");
        double x = methodS / BinSeconds - 0.5;
        if (x <= 0) return Expected[0];
        int i = (int)Math.Floor(x);
        if (i >= Expected.Length - 1) return Expected[^1];
        double f = x - i;
        return Expected[i] * (1 - f) + Expected[i + 1] * f;
    }

    /// <summary>
    /// Smoothed pressure for every sample once the window is ready, paired with the method position at the
    /// middle of the window, so the comparison is not biased by the smoothing delay.
    /// </summary>
    public static IEnumerable<(Sample Sample, double Smoothed, double CentreMethodS)> Smooth(IEnumerable<Sample> run)
    {
        var window = new SampleWindow(SmoothSeconds);
        foreach (var s in run)
        {
            window.Push(s);
            if (!window.Ready) continue;
            yield return (s, window.MedianPressure(), window.CentreMethodS());
        }
    }

    /// <summary>Learns the envelope from healthy runs of the same method.</summary>
    public static Envelope Learn(IReadOnlyList<IReadOnlyList<Sample>> healthyRuns, double binSeconds = 10)
    {
        if (healthyRuns.Count < 3) throw new ArgumentException("Need at least three healthy runs.", nameof(healthyRuns));
        double maxMethod = healthyRuns.Max(r => r.Max(s => s.MethodS));
        int bins = (int)Math.Ceiling((maxMethod + 1e-9) / binSeconds);
        var sum = new double[bins];
        var cnt = new int[bins];
        foreach (var run in healthyRuns)
            foreach (var (_, sm, cm) in Smooth(run))
            {
                int b = Math.Min(bins - 1, (int)(cm / binSeconds));
                sum[b] += sm; cnt[b]++;
            }
        var expected = new double[bins];
        for (int b = 0; b < bins; b++)
            expected[b] = cnt[b] > 0 ? sum[b] / cnt[b] : double.NaN;
        // Fill any empty bin from its neighbour so interpolation never meets a gap.
        for (int b = 1; b < bins; b++) if (double.IsNaN(expected[b])) expected[b] = expected[b - 1];
        for (int b = bins - 2; b >= 0; b--) if (double.IsNaN(expected[b])) expected[b] = expected[b + 1];

        var draft = new Envelope { BinSeconds = binSeconds, Expected = expected };
        var level = new List<double>();
        var spike = new List<double>();
        var runMedians = new List<double>();
        foreach (var run in healthyRuns)
        {
            var devs = new List<double>();
            foreach (var (s, sm, cm) in Smooth(run))
            {
                double e = draft.ExpectedAt(cm);
                double d = (sm - e) / e;
                devs.Add(d);
                level.Add(d);
                spike.Add((s.Pressure - sm) / e);
            }
            runMedians.Add(Stats.Median(devs));
        }

        return new Envelope
        {
            BinSeconds = binSeconds,
            Expected = expected,
            LevelSigma = Stats.StdDev(level),
            SpikeSigma = Stats.SigmaFromMad(spike),
            RunSigma = Stats.StdDev(runMedians),
            TrainingRuns = healthyRuns.Count,
        };
    }
}
