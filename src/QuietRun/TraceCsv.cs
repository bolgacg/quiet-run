using System.Globalization;
using System.Text;
using System.Text.Json;

namespace QuietRun;

/// <summary>
/// Reads and writes run traces as CSV with the header
/// t_s,pressure,flow,status,heartbeat,method_s
/// and envelopes as JSON.
/// </summary>
public static class TraceCsv
{
    public const string Header = "t_s,pressure,flow,status,heartbeat,method_s";
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static void Write(string path, IEnumerable<Sample> run)
    {
        var sb = new StringBuilder();
        sb.Append(Header).Append('\n');
        foreach (var s in run)
            sb.Append(string.Format(Inv, "{0:0},{1:0.000},{2:0.000},{3},{4},{5:0}\n",
                s.T, s.Pressure, s.Flow, s.Status == InstrumentStatus.Running ? "Running" : "Idle", s.Heartbeat, s.MethodS));
        File.WriteAllText(path, sb.ToString());
    }

    public static List<Sample> Read(string path) => Parse(File.ReadLines(path), path);

    public static List<Sample> Parse(IEnumerable<string> lines, string source = "input")
    {
        var result = new List<Sample>();
        int lineNo = 0;
        string[]? cols = null;
        foreach (var raw in lines)
        {
            lineNo++;
            var line = raw.Trim();
            if (line.Length == 0) continue;
            if (cols is null)
            {
                cols = line.Split(',').Select(c => c.Trim().ToLowerInvariant()).ToArray();
                foreach (var need in Header.Split(','))
                    if (!cols.Contains(need))
                        throw new TraceFormatException($"{source}: missing column '{need}'. Expected header: {Header}");
                continue;
            }
            var f = line.Split(',');
            if (f.Length != cols.Length)
                throw new TraceFormatException($"{source} line {lineNo}: expected {cols.Length} fields, found {f.Length}.");
            string Get(string name) => f[Array.IndexOf(cols, name)].Trim();
            try
            {
                result.Add(new Sample(
                    double.Parse(Get("t_s"), Inv),
                    double.Parse(Get("pressure"), Inv),
                    double.Parse(Get("flow"), Inv),
                    Get("status").Equals("Running", StringComparison.OrdinalIgnoreCase) ? InstrumentStatus.Running : InstrumentStatus.Idle,
                    long.Parse(Get("heartbeat"), Inv),
                    double.Parse(Get("method_s"), Inv)));
            }
            catch (System.FormatException e)
            {
                throw new TraceFormatException($"{source} line {lineNo}: {e.Message}");
            }
        }
        return result;
    }

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public static void WriteEnvelope(string path, Envelope env) =>
        File.WriteAllText(path, JsonSerializer.Serialize(env, Json));

    public static Envelope ReadEnvelope(string path) =>
        JsonSerializer.Deserialize<Envelope>(File.ReadAllText(path))
        ?? throw new TraceFormatException($"{path}: not an envelope.");
}

public sealed class TraceFormatException(string message) : Exception(message);
