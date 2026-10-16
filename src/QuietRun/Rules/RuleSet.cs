namespace QuietRun;

/// <summary>
/// Builds the standard rules for an envelope and a setting. Each rule gets only its own thresholds, already
/// turned from spreads into fractions. A rule switched off in the setting is left out.
/// </summary>
public static class RuleSet
{
    /// <summary>The in-run rules, in the order the monitor asks them on each sample.</summary>
    public static IReadOnlyList<IRule> InRun(Envelope env, DetectorSettings s)
    {
        var rules = new List<IRule>();
        if (s.IsOn(Verdict.Quiet)) rules.Add(new QuietRule(s.QuietS));
        if (s.IsOn(Verdict.Leak)) rules.Add(new LeakRule(s.LevelZ * env.LevelSigma, s.LevelHoldS));
        if (s.IsOn(Verdict.Clog)) rules.Add(new ClogRule(s.LevelZ * env.LevelSigma, s.LevelHoldS));
        if (s.IsOn(Verdict.Air)) rules.Add(new AirRule(s.SpikeZ * env.SpikeSigma, s.SpikeCount, s.SpikeWindowS, s.SpikeMaxS));
        return rules;
    }

    /// <summary>The cross-run rules.</summary>
    public static IReadOnlyList<ISequenceRule> AcrossRuns(Envelope env, DetectorSettings s) =>
        s.IsOn(Verdict.Drift) ? [new DriftRule(env.RunSigma, s.DriftK, s.DriftH)] : [];
}
