using QuietRun;

namespace QuietRun.Tests;

/// <summary>
/// Changes how a one-a-second synthetic run was sampled, the way a real logger might: jittered timestamps,
/// lost readings, a gap, or a different rate.
/// </summary>
public static class Resample
{
    /// <summary>Moves each timestamp by up to maxS either way. With maxS under half a second the order is kept.</summary>
    public static List<Sample> Jitter(IReadOnlyList<Sample> run, double maxS, int seed)
    {
        var rng = new Random(seed);
        return run.Select(s => s with { T = s.T + (2 * rng.NextDouble() - 1) * maxS }).ToList();
    }

    /// <summary>Loses each reading after the first with the given probability.</summary>
    public static List<Sample> Drop(IReadOnlyList<Sample> run, double fraction, int seed)
    {
        var rng = new Random(seed);
        return run.Where((_, i) => i == 0 || rng.NextDouble() >= fraction).ToList();
    }

    /// <summary>Loses every reading from fromS for lengthS seconds.</summary>
    public static List<Sample> Gap(IReadOnlyList<Sample> run, double fromS, double lengthS) =>
        run.Where(s => s.T < fromS || s.T >= fromS + lengthS).ToList();

    /// <summary>Keeps every nth reading.</summary>
    public static List<Sample> EveryNth(IReadOnlyList<Sample> run, int n) =>
        run.Where((_, i) => i % n == 0).ToList();

    /// <summary>Two readings a second: adds one halfway between each pair, interpolated. The heartbeat still ticks once a second.</summary>
    public static List<Sample> Doubled(IReadOnlyList<Sample> run)
    {
        var result = new List<Sample>(run.Count * 2);
        for (int i = 0; i < run.Count; i++)
        {
            result.Add(run[i]);
            if (i + 1 == run.Count) break;
            Sample a = run[i], b = run[i + 1];
            result.Add(new Sample((a.T + b.T) / 2, (a.Pressure + b.Pressure) / 2, (a.Flow + b.Flow) / 2,
                a.Status, a.Heartbeat, (a.MethodS + b.MethodS) / 2));
        }
        return result;
    }
}
