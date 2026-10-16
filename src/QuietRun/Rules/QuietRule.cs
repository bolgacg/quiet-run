using System.Globalization;

namespace QuietRun;

/// <summary>
/// QUIET: the instrument says Running and its heartbeat arrives, but the method clock stands still.
/// It checks progress, not presence. It reads every sample, before the smoothing window is ready.
/// </summary>
public sealed class QuietRule : IRule
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>A heartbeat older than this many seconds means the instrument is silent, which is a presence failure, not QUIET.</summary>
    public const double HeartbeatTimeoutS = 5;

    private readonly double _stillS;

    private double _lastMethodS = double.NaN, _lastProgressT = double.NaN;
    private long _lastHeartbeat = long.MinValue;
    private double _firstHeartbeatT = double.NaN, _lastHeartbeatT = double.NaN;
    private long _heartbeats;

    /// <param name="stillS">Seconds the method clock may stand still while the instrument says Running.</param>
    public QuietRule(double stillS) => _stillS = stillS;

    public Verdict Kind => Verdict.Quiet;

    public string? Check(in RuleContext ctx)
    {
        var x = ctx.Sample;
        if (x.Heartbeat != _lastHeartbeat)
        {
            if (_heartbeats++ == 0) _firstHeartbeatT = x.T;
            _lastHeartbeat = x.Heartbeat;
            _lastHeartbeatT = x.T;
        }
        if (x.Status != InstrumentStatus.Running || double.IsNaN(_lastMethodS) || x.MethodS > _lastMethodS + 1e-6)
        {
            _lastMethodS = x.MethodS;
            _lastProgressT = x.T;
            return null;
        }
        double still = x.T - _lastProgressT;
        double hbAge = x.T - _lastHeartbeatT;
        if (hbAge > HeartbeatTimeoutS || still < _stillS) return null;

        // The heartbeat's own rate, from the timestamps of its changes so far.
        double every = _heartbeats >= 2 ? (_lastHeartbeatT - _firstHeartbeatT) / (_heartbeats - 1) : double.NaN;
        string heartbeat = hbAge < 1.5 * every
            ? "its heartbeat arrives " + Every(every)
            : string.Format(Inv, "its last heartbeat came {0:0} s ago", hbAge);
        return string.Format(Inv,
            "The instrument reports Running and {0}, but the method clock has stood at {1} for {2:0} s. A presence check passes while the run is not moving.",
            heartbeat, VerdictText.Clock(_lastMethodS), still);
    }

    private static string Every(double seconds) =>
        Math.Abs(seconds - 1) < 0.05 ? "every second" : string.Format(Inv, "every {0:0.#} s", seconds);
}
