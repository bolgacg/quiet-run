namespace QuietRun;

/// <summary>
/// Watches one run, one sample at a time. It smooths the pressure, compares it with the envelope, and asks each
/// rule in turn. Each kind of rule fires at most once a run. The monitor knows nothing about any single rule,
/// so a rule can be added, removed or replaced without touching it.
/// </summary>
public sealed class RunMonitor
{
    private readonly Envelope _env;
    private readonly IReadOnlyList<IRule> _rules;
    private readonly SampleWindow _window = new(Envelope.SmoothSeconds);
    private readonly List<Alarm> _alarms = [];
    private readonly HashSet<Verdict> _fired = [];
    private readonly List<double> _deviations = [];

    /// <summary>A monitor with the standard rules for this setting (QUIET, LEAK, CLOG, AIR).</summary>
    public RunMonitor(Envelope envelope, DetectorSettings settings)
        : this(envelope, RuleSet.InRun(envelope, settings)) { }

    /// <summary>A monitor with any set of rules, asked in the order given.</summary>
    public RunMonitor(Envelope envelope, IEnumerable<IRule> rules)
    {
        _env = envelope;
        _rules = rules.ToList();
    }

    public IReadOnlyList<Alarm> Alarms => _alarms;

    /// <summary>The run's median deviation from the envelope so far, as a fraction. Feeds the DRIFT rule.</summary>
    public double MedianDeviation => Stats.Median(_deviations);

    /// <summary>Feeds one sample. Returns the first alarm raised on this sample, if any.</summary>
    /// <exception cref="ArgumentException">The sample's timestamp is not after the previous one.</exception>
    public Alarm? Push(Sample x)
    {
        _window.Push(x);
        var ctx = Context(x);
        if (ctx.Comparable) _deviations.Add(ctx.Deviation);

        Alarm? first = null;
        foreach (var rule in _rules)
        {
            if (_fired.Contains(rule.Kind)) continue;
            if (rule.Check(ctx) is not { } reason) continue;
            _fired.Add(rule.Kind);
            var alarm = new Alarm(rule.Kind, x.T, reason);
            _alarms.Add(alarm);
            first ??= alarm;
        }
        return first;
    }

    private RuleContext Context(Sample x)
    {
        if (!_window.Ready || x.Status != InstrumentStatus.Running) return RuleContext.NotComparable(x);
        double smoothed = _window.MedianPressure();
        double centre = _window.CentreMethodS();
        double expected = _env.ExpectedAt(centre);
        return new RuleContext(x, true, smoothed, centre, expected,
            (smoothed - expected) / expected, (x.Pressure - smoothed) / expected);
    }
}

/// <summary>
/// Watches whole runs in sequence with the cross-run rules (DRIFT), for trends no single run shows.
/// </summary>
public sealed class SequenceMonitor
{
    private readonly IReadOnlyList<ISequenceRule> _rules;

    /// <summary>A monitor with the standard cross-run rules for this setting.</summary>
    public SequenceMonitor(Envelope envelope, DetectorSettings settings)
        : this(RuleSet.AcrossRuns(envelope, settings)) { }

    public SequenceMonitor(IEnumerable<ISequenceRule> rules) => _rules = rules.ToList();

    /// <summary>Adds one finished run. Returns the alarms the cross-run rules raised on it.</summary>
    public IReadOnlyList<Alarm> AddRun(RunSummary run)
    {
        var fired = new List<Alarm>();
        foreach (var rule in _rules)
            if (rule.AddRun(run) is { } a) fired.Add(a);
        return fired;
    }
}

/// <summary>Checks a whole run in one call.</summary>
public static class RunChecker
{
    public const string OkReason =
        "Pressure stayed inside this method's envelope, there were no bursts of short swings, and the method clock kept moving.";

    /// <param name="sequence">Optional: the monitor for the sequence this run belongs to. The run is added to it.</param>
    public static RunReport Check(ISampleSource source, Envelope envelope, DetectorSettings settings, SequenceMonitor? sequence = null) =>
        Check(source.Samples(), envelope, settings, sequence);

    /// <param name="sequence">Optional: the monitor for the sequence this run belongs to. The run is added to it.</param>
    public static RunReport Check(IEnumerable<Sample> run, Envelope envelope, DetectorSettings settings, SequenceMonitor? sequence = null)
    {
        var m = new RunMonitor(envelope, settings);
        double lastT = 0;
        foreach (var s in run) { m.Push(s); lastT = s.T; }

        var alarms = m.Alarms.ToList();
        if (sequence is not null) alarms.AddRange(sequence.AddRun(new RunSummary(m.MedianDeviation, lastT)));

        if (alarms.Count == 0)
            return new RunReport(Verdict.Ok, null, OkReason, alarms, m.MedianDeviation);
        var first = alarms[0];
        return new RunReport(first.Kind, first.T, first.Reason, alarms, m.MedianDeviation);
    }
}
