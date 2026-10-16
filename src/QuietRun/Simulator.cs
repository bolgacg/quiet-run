namespace QuietRun;

/// <summary>A fault to inject into a synthetic run.</summary>
/// <param name="Kind">Leak, Clog, Air or Quiet. Drift is applied across runs through the run offset instead.</param>
/// <param name="Size">Leak and Clog: fraction of pressure lost or gained. Air: depth of each dip as a fraction.
/// Quiet: seconds the method clock stands still (infinity for the rest of the run).</param>
/// <param name="StartS">Seconds into the run when the fault begins.</param>
public sealed record FaultSpec(Verdict Kind, double Size, double StartS);

/// <summary>
/// Generates labelled synthetic runs. Everything here is invented for this demo: the pressure shape, the noise
/// and the faults. The only public facts used are the run length (a 21-minute gradient, as in a 60 samples-per-day
/// method; Bache et al. 2018) and that runs record a pressure profile.
/// Pressure is an index: 100 is the healthy average over the run.
/// </summary>
public static class Simulator
{
    /// <summary>A 21-minute gradient, one sample per second.</summary>
    public const int RunSeconds = 1260;

    public const double LeakRampS = 30;
    public const double ClogRampS = 240;
    public const double AirBurstS = 120;
    public const int AirDips = 6;

    /// <summary>Planned pauses of the method clock in healthy runs last between these many seconds.</summary>
    public const double PauseMinS = 3, PauseMaxS = 12;

    /// <summary>Fault sizes for severities 1 to 4.</summary>
    public static readonly IReadOnlyDictionary<Verdict, double[]> Severities = new Dictionary<Verdict, double[]>
    {
        [Verdict.Leak] = [0.02, 0.04, 0.07, 0.12],
        [Verdict.Clog] = [0.03, 0.06, 0.10, 0.18],
        [Verdict.Air] = [0.02, 0.04, 0.08, 0.15],
        [Verdict.Quiet] = [20, 45, 120, double.PositiveInfinity],
        [Verdict.Drift] = [0.0025, 0.005, 0.01, 0.02],
    };

    private static readonly double ShapeMean = ComputeShapeMean();

    private static double RawShape(double methodS)
    {
        // Rises and falls once over the run, loosely like a solvent mix whose thickness changes along a gradient.
        double u = Math.Clamp(methodS / RunSeconds, 0, 1);
        double z = (u - 0.35) / 0.22;
        return 1 + 0.12 * Math.Exp(-z * z) - 0.12 * u;
    }

    private static double ComputeShapeMean()
    {
        double s = 0;
        for (int t = 0; t < RunSeconds; t++) s += RawShape(t);
        return s / RunSeconds;
    }

    /// <summary>The healthy pressure at a method position, before run offset and noise. Averages 100 over the run.</summary>
    public static double Shape(double methodS) => 100 * RawShape(methodS) / ShapeMean;

    /// <summary>
    /// How far one healthy run sits from the average, as a fraction. Most runs sit within about one percent; one
    /// run in twelve is drawn from a wider spread, so the healthy set has a few runs further out.
    /// </summary>
    public static double DrawRunOffset(Random rng) =>
        rng.NextDouble() < 0.08 ? Gaussian(rng) * 0.02 : Gaussian(rng) * 0.008;

    public static double Gaussian(Random rng)
    {
        double u1 = 1.0 - rng.NextDouble(), u2 = rng.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
    }

    /// <summary>
    /// One run. The same seed gives the same run.
    /// </summary>
    /// <param name="seed">Seed for this run's noise, pauses and fault timing details.</param>
    /// <param name="runOffset">Healthy run-to-run offset as a fraction (draw it with DrawRunOffset, or pass 0).</param>
    /// <param name="fault">Optional in-run fault.</param>
    /// <param name="driftOffset">Extra offset from a slow trend across runs, as a fraction.</param>
    public static List<Sample> Run(int seed, double runOffset, FaultSpec? fault = null, double driftOffset = 0)
    {
        var rng = new Random(seed);
        var samples = new List<Sample>(RunSeconds);

        // Planned pauses of the method clock (a valve switching, a wait step): zero to two per run.
        int nPauses = rng.Next(0, 3);
        var pauses = new List<(double Start, double Len)>();
        for (int i = 0; i < nPauses; i++) pauses.Add((60 + rng.NextDouble() * 1140, PauseMinS + rng.NextDouble() * (PauseMaxS - PauseMinS)));

        // Rare single-sample glitches from the sensor: about one run in seven has one.
        var glitches = new Dictionary<int, double>();
        double l = Math.Exp(-0.15), p = rng.NextDouble();
        int nGlitch = 0;
        while (p > l) { nGlitch++; p *= rng.NextDouble(); }
        for (int i = 0; i < nGlitch; i++)
        {
            int at = 30 + rng.Next(RunSeconds - 40);
            glitches[at] = (rng.NextDouble() < 0.5 ? -1 : 1) * (1.5 + rng.NextDouble() * 2.5);
        }

        // Air: a burst of short dips after the fault starts.
        var dips = new List<(int Start, int Len, double Depth)>();
        if (fault?.Kind == Verdict.Air)
        {
            for (int i = 0; i < AirDips; i++)
                dips.Add(((int)(fault.StartS + rng.NextDouble() * AirBurstS), 2 + rng.Next(3), fault.Size * (0.7 + 0.3 * rng.NextDouble())));
        }

        double phase = rng.NextDouble() * 2 * Math.PI;
        double method = 0;
        for (int t = 0; t < RunSeconds; t++)
        {
            bool paused = pauses.Any(q => t >= q.Start && t < q.Start + q.Len);
            bool stalled = fault?.Kind == Verdict.Quiet && t >= fault.StartS && t < fault.StartS + fault.Size;

            double pressure = Shape(method) * (1 + runOffset + driftOffset);
            if (fault is not null && t >= fault.StartS)
            {
                double since = t - fault.StartS;
                switch (fault.Kind)
                {
                    case Verdict.Leak:
                        pressure *= 1 - fault.Size * Math.Min(1, since / LeakRampS);
                        break;
                    case Verdict.Clog:
                        pressure *= 1 + fault.Size * Math.Min(1, since / ClogRampS);
                        break;
                }
            }
            foreach (var d in dips)
                if (t >= d.Start && t < d.Start + d.Len)
                    pressure -= Shape(method) * d.Depth * (t == d.Start + d.Len - 1 ? 0.5 : 1.0);

            pressure += 0.3 * Math.Sin(2 * Math.PI * t / 6.5 + phase) + 0.25 * Gaussian(rng);
            if (glitches.TryGetValue(t, out var g)) pressure += g;

            double flow = 100 + 0.3 * Gaussian(rng);
            samples.Add(new Sample(t, pressure, flow, InstrumentStatus.Running, t + 1, method));

            if (!paused && !stalled) method += 1;
        }
        return samples;
    }

    /// <summary>Healthy runs with drawn offsets, for learning an envelope.</summary>
    public static List<IReadOnlyList<Sample>> HealthyRuns(int count, int seed)
    {
        var rng = new Random(seed);
        var runs = new List<IReadOnlyList<Sample>>(count);
        for (int i = 0; i < count; i++)
        {
            int runSeed = rng.Next();
            double offset = DrawRunOffset(rng);
            runs.Add(Run(runSeed, offset));
        }
        return runs;
    }
}
