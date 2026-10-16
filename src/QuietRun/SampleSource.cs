namespace QuietRun;

/// <summary>
/// Where one run's samples come from. The checker reads any source the same way, so a CSV file, the simulator
/// and, later, a live instrument connection are interchangeable.
/// </summary>
public interface ISampleSource
{
    /// <summary>A short name for the run, used in printed verdicts.</summary>
    string Name { get; }

    /// <summary>The run's samples, in time order.</summary>
    IEnumerable<Sample> Samples();
}

/// <summary>A run saved as CSV (see <see cref="TraceCsv"/>). The file is read and checked in full before any sample is returned.</summary>
public sealed class CsvSource(string path) : ISampleSource
{
    public string Name => Path.GetFileName(path);

    public IEnumerable<Sample> Samples() => TraceCsv.Read(path);
}

/// <summary>A synthetic run from <see cref="Simulator"/>. The same arguments give the same run.</summary>
public sealed class SimulatedSource(int seed, double runOffset, FaultSpec? fault = null, double driftOffset = 0) : ISampleSource
{
    public string Name => fault is null ? $"simulated run {seed}" : $"simulated run {seed} ({fault.Kind.Upper()})";

    public IEnumerable<Sample> Samples() => Simulator.Run(seed, runOffset, fault, driftOffset);
}
