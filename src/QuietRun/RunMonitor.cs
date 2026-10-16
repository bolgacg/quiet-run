using System.Globalization;

namespace QuietRun;

/// <summary>
/// Watches one run, one sample at a time, and fires each in-run rule at most once:
/// LEAK (pressure sits below the envelope), CLOG (above it), AIR (several short swings while the average holds)
/// and QUIET (the instrument says Running and its heartbeat arrives, but the method clock stands still).
/// Every rule only looks at samples it has already seen, so it can run live on an instrument.
/// </summary>
public sealed class RunMonitor
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private readonly Envelope _env;
    private readonly DetectorSettings _s;
    private readonly RollingMedian _med = new(Envelope.SmoothWindow);
    private readonly Queue<double> _methodQ = new();
    private readonly List<Alarm> _alarms = [];
    private readonly HashSet<Verdict> _fired = [];
    private readonly List<double> _deviations = [];

    // LEAK and CLOG
    private double _belowSince = double.NaN, _aboveSince = double.NaN;
    private double _worstBelow, _worstAbove;

    // AIR
    private bool _inSwing;
    private double _swingStart;
    private int _swingSign;
    private readonly List<(double End, int Sign, double Depth)> _swings = [];
    private double _swingDepth;

    // QUIET
    private double _lastMethodS = double.NaN, _lastProgressT = double.NaN;
    private long _lastHeartbeat = long.MinValue;
    private double _lastHeartbeatT = double.NaN;

    public RunMonitor(Envelope envelope, DetectorSettings settings)
    {
        _env = envelope;
        _s = settings;
    }

    public IReadOnlyList<Alarm> Alarms => _alarms;

    /// <summary>The run's median deviation from the envelope so far, as a fraction. Feeds the DRIFT rule.</summary>
    public double MedianDeviation => Stats.Median(_deviations);

    /// <summary>Feeds one sample. Returns the alarm if a rule fired on this sample.</summary>
    public Alarm? Push(Sample x)
    {
        Alarm? fired = null;
        fired ??= CheckQuiet(x);

        double sm = _med.Push(x.Pressure);
        _methodQ.Enqueue(x.MethodS);
        if (_methodQ.Count > Envelope.SmoothWindow) _methodQ.Dequeue();
        if (!_med.Full || x.Status != InstrumentStatus.Running) return fired;

        double centre = _methodQ.ElementAt(Envelope.SmoothWindow / 2);
        double expected = _env.ExpectedAt(centre);
        double dev = (sm - expected) / expected;
        _deviations.Add(dev);

        var level = CheckLevel(x, dev, centre);
        fired ??= level;
        var air = CheckAir(x, (x.Pressure - sm) / expected);
        fired ??= air;
        return fired;
    }

    private Alarm? Fire(Verdict kind, double t, string reason)
    {
        if (!_s.IsOn(kind) || !_fired.Add(kind)) return null;
        var a = new Alarm(kind, t, reason);
        _alarms.Add(a);
        return a;
    }

    private Alarm? CheckQuiet(Sample x)
    {
        if (x.Heartbeat != _lastHeartbeat) { _lastHeartbeat = x.Heartbeat; _lastHeartbeatT = x.T; }
        if (x.Status != InstrumentStatus.Running || double.IsNaN(_lastMethodS) || x.MethodS > _lastMethodS + 1e-6)
        {
            _lastMethodS = x.MethodS;
            _lastProgressT = x.T;
            return null;
        }
        double still = x.T - _lastProgressT;
        double hbAge = x.T - _lastHeartbeatT;
        bool alive = hbAge <= 5;
        if (!alive || still < _s.QuietS) return null;
        string heartbeat = hbAge < 1.5
            ? "its heartbeat arrives every second"
            : string.Format(Inv, "its last heartbeat came {0:0} s ago", hbAge);
        return Fire(Verdict.Quiet, x.T, string.Format(Inv,
            "The instrument reports Running and {0}, but the method clock has stood at {1} for {2:0} s. A presence check passes while the run is not moving.",
            heartbeat, VerdictText.Clock(_lastMethodS), still));
    }

    private Alarm? CheckLevel(Sample x, double dev, double centre)
    {
        double thr = _s.LevelZ * _env.LevelSigma;
        Alarm? fired = null;

        if (dev < -thr)
        {
            if (double.IsNaN(_belowSince)) { _belowSince = x.T; _worstBelow = dev; }
            _worstBelow = Math.Min(_worstBelow, dev);
            if (x.T - _belowSince >= _s.LevelHoldS)
                fired ??= Fire(Verdict.Leak, x.T, string.Format(Inv,
                    "Pressure has sat {0:0.0}% below what this method normally shows at {1} for {2:0} s, while the pump's flow reads {3:0.0} of its set 100. Liquid is leaving the flow path somewhere.",
                    -dev * 100, VerdictText.Clock(centre), x.T - _belowSince, x.Flow));
        }
        else _belowSince = double.NaN;

        if (dev > thr)
        {
            if (double.IsNaN(_aboveSince)) { _aboveSince = x.T; _worstAbove = dev; }
            _worstAbove = Math.Max(_worstAbove, dev);
            if (x.T - _aboveSince >= _s.LevelHoldS)
                fired ??= Fire(Verdict.Clog, x.T, string.Format(Inv,
                    "Pressure has sat {0:0.0}% above what this method normally shows at {1} for {2:0} s, while the pump's flow reads {3:0.0} of its set 100. Something in the flow path is narrowing.",
                    dev * 100, VerdictText.Clock(centre), x.T - _aboveSince, x.Flow));
        }
        else _aboveSince = double.NaN;

        return fired;
    }

    private Alarm? CheckAir(Sample x, double swing)
    {
        // A swing starts above the threshold and only ends once it falls back under half of it, so a slow ramp
        // hovering near the threshold counts as one long swing, not many short ones.
        double thr = _s.SpikeZ * _env.SpikeSigma;
        if (!_inSwing && Math.Abs(swing) > thr)
        {
            _inSwing = true; _swingStart = x.T; _swingSign = Math.Sign(swing); _swingDepth = 0;
        }
        if (_inSwing && Math.Abs(swing) >= thr / 2)
        {
            _swingDepth = Math.Max(_swingDepth, Math.Abs(swing));
        }
        else if (_inSwing)
        {
            _inSwing = false;
            double length = x.T - _swingStart;
            if (length <= _s.SpikeMaxS) _swings.Add((x.T, _swingSign, _swingDepth));
        }

        _swings.RemoveAll(w => w.End < x.T - _s.SpikeWindowS);
        if (_swings.Count < _s.SpikeCount) return null;

        int dips = _swings.Count(w => w.Sign < 0);
        string what = dips * 2 >= _swings.Count ? "dips" : "spikes";
        double span = _swings[^1].End - _swings[0].End;
        double deepest = _swings.Max(w => w.Depth) * 100;
        return Fire(Verdict.Air, x.T, string.Format(Inv,
            "{0} short pressure {1} in {2:0} s, each shorter than {3:0} s and the deepest {4:0.0}%, while the average pressure held. That is the pattern of gas bubbles passing through the pump.",
            _swings.Count, what, span, _s.SpikeMaxS, deepest));
    }
}

/// <summary>
/// Watches whole runs in sequence for a slow trend that no single run shows. It is a two-sided cumulative sum
/// (CUSUM, Page 1954) of each run's median deviation, measured in run-to-run spreads.
/// </summary>
public sealed class DriftMonitor
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private readonly Envelope _env;
    private readonly DetectorSettings _s;
    private double _up, _down;
    private readonly List<double> _upRuns = [], _downRuns = [];

    public DriftMonitor(Envelope envelope, DetectorSettings settings)
    {
        _env = envelope;
        _s = settings;
    }

    public int RunsSeen { get; private set; }

    /// <summary>Adds one finished run. Returns a DRIFT alarm when the cumulative sum crosses its limit.</summary>
    public Alarm? AddRun(double medianDeviation, double runEndT)
    {
        RunsSeen++;
        if (double.IsNaN(medianDeviation) || !_s.IsOn(Verdict.Drift)) return null;
        double z = medianDeviation / _env.RunSigma;
        _up = Math.Max(0, _up + z - _s.DriftK);
        _down = Math.Max(0, _down - z - _s.DriftK);
        if (_up > 0) _upRuns.Add(medianDeviation); else _upRuns.Clear();
        if (_down > 0) _downRuns.Add(medianDeviation); else _downRuns.Clear();

        List<double>? streak = _up > _s.DriftH ? _upRuns : _down > _s.DriftH ? _downRuns : null;
        if (streak is null) return null;

        string dir = streak == _upRuns ? "risen" : "fallen";
        double perRun = Stats.Slope(streak) * 100;
        string reason = streak.Count >= 2
            ? string.Format(Inv,
                "Across the last {0} runs the run-average pressure has {1} to {2:+0.0;-0.0}% of normal, about {3:+0.00;-0.00}% per run. This rule compares runs with each other, so it can fire while every run on its own looks normal.",
                streak.Count, dir, medianDeviation * 100, perRun)
            : string.Format(Inv,
                "This run's average pressure sits at {0:+0.0;-0.0}% of normal, far enough from the runs before it that the cross-run rule fires on one run.",
                medianDeviation * 100);
        var alarm = new Alarm(Verdict.Drift, runEndT, reason);
        _up = _down = 0;
        _upRuns.Clear();
        _downRuns.Clear();
        return alarm;
    }
}

/// <summary>Checks a whole run in one call.</summary>
public static class RunChecker
{
    public const string OkReason =
        "Pressure stayed inside this method's envelope, there were no bursts of short swings, and the method clock kept moving.";

    /// <param name="drift">Optional: the monitor for the sequence this run belongs to. The run is added to it.</param>
    public static RunReport Check(IEnumerable<Sample> run, Envelope envelope, DetectorSettings settings, DriftMonitor? drift = null)
    {
        var m = new RunMonitor(envelope, settings);
        double lastT = 0;
        foreach (var s in run) { m.Push(s); lastT = s.T; }

        var alarms = m.Alarms.ToList();
        if (drift?.AddRun(m.MedianDeviation, lastT) is { } d) alarms.Add(d);

        if (alarms.Count == 0)
            return new RunReport(Verdict.Ok, null, OkReason, alarms, m.MedianDeviation);
        var first = alarms[0];
        return new RunReport(first.Kind, first.T, first.Reason, alarms, m.MedianDeviation);
    }
}
