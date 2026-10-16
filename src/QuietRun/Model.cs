namespace QuietRun;

/// <summary>What the instrument says it is doing.</summary>
public enum InstrumentStatus { Idle, Running }

/// <summary>
/// One reading from the instrument, taken once per second.
/// Pressure and flow are indexes, not physical units: 100 means the healthy average for this method
/// (pressure) or the set flow (flow).
/// </summary>
/// <param name="T">Seconds since the run started, by the logger's clock.</param>
/// <param name="Pressure">Pump pressure index. 100 is the healthy average over the run.</param>
/// <param name="Flow">Flow index. 100 is the set flow.</param>
/// <param name="Status">What the instrument reports it is doing.</param>
/// <param name="Heartbeat">A counter the instrument increments while it is alive.</param>
/// <param name="MethodS">How far through the method the instrument says it is, in seconds.</param>
public readonly record struct Sample(
    double T,
    double Pressure,
    double Flow,
    InstrumentStatus Status,
    long Heartbeat,
    double MethodS);

/// <summary>The verdict for a run. Ok means no rule fired.</summary>
public enum Verdict { Ok, Leak, Clog, Air, Drift, Quiet }

/// <summary>One rule firing: what it decided, when, and why in plain English.</summary>
public sealed record Alarm(Verdict Kind, double T, string Reason);

/// <summary>The result of checking one run.</summary>
/// <param name="Verdict">The first rule that fired, or Ok.</param>
/// <param name="FlaggedAtS">Seconds into the run when the first rule fired. Null when Ok.</param>
/// <param name="Reason">Plain-English reason for the verdict.</param>
/// <param name="Alarms">Every rule that fired during the run, in order.</param>
/// <param name="MedianDeviation">The run's median pressure deviation from its envelope, as a fraction.</param>
public sealed record RunReport(
    Verdict Verdict,
    double? FlaggedAtS,
    string Reason,
    IReadOnlyList<Alarm> Alarms,
    double MedianDeviation);

public static class VerdictText
{
    public static string Upper(this Verdict v) => v.ToString().ToUpperInvariant();

    /// <summary>Formats a method position in seconds as m:ss.</summary>
    public static string Clock(double seconds)
    {
        var s = (int)Math.Round(Math.Max(0, seconds));
        return $"{s / 60}:{s % 60:00}";
    }
}
