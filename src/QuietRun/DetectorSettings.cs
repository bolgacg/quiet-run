namespace QuietRun;

/// <summary>
/// How strict each rule is. Thresholds are in units of the spreads the envelope learned from healthy runs,
/// so the same setting carries over to a method with a different pressure.
/// </summary>
/// <param name="Name">relaxed, chosen or sensitive.</param>
/// <param name="LevelZ">LEAK and CLOG: how many level spreads the smoothed pressure must sit away from the envelope.</param>
/// <param name="LevelHoldS">LEAK and CLOG: for how many seconds in a row.</param>
/// <param name="SpikeZ">AIR: how many spike spreads a short swing must reach to count.</param>
/// <param name="SpikeCount">AIR: how many short swings must fall inside the window.</param>
/// <param name="SpikeWindowS">AIR: the window, in seconds.</param>
/// <param name="SpikeMaxS">AIR: a swing longer than this is not short and does not count.</param>
/// <param name="QuietS">QUIET: seconds the method clock may stand still while the instrument says Running.</param>
/// <param name="DriftK">DRIFT: allowance per run, in run spreads, before the cumulative sum grows.</param>
/// <param name="DriftH">DRIFT: the cumulative sum, in run spreads, at which the rule fires.</param>
/// <param name="Disabled">Rules switched off. Used by tests that prove each test depends on its rule.</param>
public sealed record DetectorSettings(
    string Name,
    double LevelZ,
    double LevelHoldS,
    double SpikeZ,
    int SpikeCount,
    double SpikeWindowS,
    double SpikeMaxS,
    double QuietS,
    double DriftK,
    double DriftH,
    IReadOnlySet<Verdict>? Disabled = null)
{
    public static readonly DetectorSettings Relaxed = new("relaxed", 4.5, 30, 7.0, 4, 90, 6, 45, 0.5, 6.5);
    public static readonly DetectorSettings Chosen = new("chosen", 3.5, 20, 6.0, 3, 90, 6, 30, 0.5, 5.0);
    public static readonly DetectorSettings Sensitive = new("sensitive", 2.5, 10, 4.5, 2, 90, 6, 20, 0.5, 3.5);

    public static IReadOnlyList<DetectorSettings> All { get; } = [Relaxed, Chosen, Sensitive];

    public static DetectorSettings ByName(string name) =>
        All.FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
        ?? throw new ArgumentException($"Unknown setting '{name}'. Use relaxed, chosen or sensitive.");

    public bool IsOn(Verdict rule) => Disabled is null || !Disabled.Contains(rule);

    /// <summary>The same setting with one rule switched off.</summary>
    public DetectorSettings Without(Verdict rule) =>
        this with { Disabled = new HashSet<Verdict>(Disabled ?? new HashSet<Verdict>()) { rule } };
}
