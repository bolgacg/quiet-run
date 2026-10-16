using System.Globalization;
using QuietRun;
using QuietRun.Cli;

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

try
{
    return Cli.Run(args);
}
catch (Exception e) when (e is TraceFormatException or ArgumentException or FileNotFoundException or DirectoryNotFoundException)
{
    Console.Error.WriteLine($"quiet-run: {e.Message}");
    return 2;
}

namespace QuietRun.Cli
{
    public static class Cli
    {
        public const int DefaultSeed = 20261016;

        private const string Usage = """
            quiet-run: a run-health monitor for liquid chromatography runs.

            Commands:
              simulate --out <dir> [--seed N]
                  Learns an envelope from 40 synthetic healthy runs and writes it with one
                  example run per fault (healthy, leak, clog, air, quiet) as CSV.
              check <run.csv>... --envelope <envelope.json> [--setting relaxed|chosen|sensitive]
                  Prints a verdict per run: OK, LEAK, CLOG, AIR, DRIFT or QUIET, with the reason.
                  Several files are read as consecutive runs, so DRIFT can be judged across them.
                  Exit code 0 when every run is OK, 3 when any run is flagged.
              study [--seed N] [--out docs/data.js]
                  Runs the full synthetic study and writes the data file the page reads.
              tests <results.trx> --out <docs/tests.js>
                  Turns a dotnet test TRX file into the test list the page shows.

            CSV header: t_s,pressure,flow,status,heartbeat,method_s (one row per reading).
            Rows need not be evenly spaced; every rule measures time from t_s, which must increase.
            """;

        public static int Run(string[] args)
        {
            if (args.Length == 0 || args[0] is "help" or "--help" or "-h")
            {
                Console.WriteLine(Usage);
                return args.Length == 0 ? 1 : 0;
            }
            var rest = args.Skip(1).ToList();
            return args[0] switch
            {
                "simulate" => Simulate(rest),
                "check" => Check(rest),
                "study" => Study.Run(Opt(rest, "--seed") is { } s ? int.Parse(s, CultureInfo.InvariantCulture) : DefaultSeed,
                                     Opt(rest, "--out") ?? Path.Combine("docs", "data.js")),
                "tests" => TestList.Run(rest.FirstOrDefault(a => !a.StartsWith("--")) ?? throw new ArgumentException("Give a .trx file."),
                                        Opt(rest, "--out") ?? Path.Combine("docs", "tests.js")),
                _ => Fail($"Unknown command '{args[0]}'.\n\n{Usage}"),
            };
        }

        private static int Fail(string msg)
        {
            Console.Error.WriteLine(msg);
            return 1;
        }

        public static string? Opt(List<string> args, string name)
        {
            int i = args.IndexOf(name);
            if (i < 0) return null;
            if (i + 1 >= args.Count) throw new ArgumentException($"{name} needs a value.");
            var v = args[i + 1];
            args.RemoveRange(i, 2);
            return v;
        }

        private static int Simulate(List<string> args)
        {
            string outDir = Opt(args, "--out") ?? "samples";
            int seed = Opt(args, "--seed") is { } s ? int.Parse(s, CultureInfo.InvariantCulture) : DefaultSeed;
            Directory.CreateDirectory(outDir);
            var training = Simulator.HealthyRuns(40, seed);
            TraceCsv.WriteEnvelope(Path.Combine(outDir, "envelope.json"), Envelope.Learn(training));
            var rng = new Random(seed + 1);
            var examples = new (string Name, FaultSpec? Fault)[]
            {
                ("healthy", null),
                ("leak", new FaultSpec(Verdict.Leak, 0.07, 420)),
                ("clog", new FaultSpec(Verdict.Clog, 0.10, 420)),
                ("air", new FaultSpec(Verdict.Air, 0.08, 420)),
                ("quiet", new FaultSpec(Verdict.Quiet, double.PositiveInfinity, 420)),
            };
            foreach (var (name, fault) in examples)
            {
                var path = Path.Combine(outDir, name + ".csv");
                TraceCsv.Write(path, Simulator.Run(rng.Next(), 0, fault));
                Console.WriteLine($"wrote {path}");
            }
            Console.WriteLine($"wrote {Path.Combine(outDir, "envelope.json")} (learned from 40 healthy runs, seed {seed})");
            return 0;
        }

        private static int Check(List<string> args)
        {
            string envPath = Opt(args, "--envelope") ?? throw new ArgumentException("check needs --envelope <envelope.json>.");
            var setting = DetectorSettings.ByName(Opt(args, "--setting") ?? "chosen");
            var files = args.Where(a => !a.StartsWith("--")).ToList();
            if (files.Count == 0) throw new ArgumentException("check needs at least one run CSV.");
            var env = TraceCsv.ReadEnvelope(envPath);
            var sequence = new SequenceMonitor(env, setting);
            int flagged = 0;
            foreach (var source in files.Select(f => new CsvSource(f)))
            {
                var report = RunChecker.Check(source, env, setting, sequence);
                Console.Write(Format(source.Name, report));
                if (report.Verdict != Verdict.Ok) flagged++;
            }
            return flagged > 0 ? 3 : 0;
        }

        /// <summary>The text check prints for one run. The study uses the same function for the page.</summary>
        public static string Format(string name, RunReport r)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append(r.Verdict == Verdict.Ok
                ? $"{name}  OK\n"
                : $"{name}  {r.Verdict.Upper()} at {VerdictText.Clock(r.FlaggedAtS!.Value)} ({r.FlaggedAtS:0} s into the run)\n");
            sb.Append($"  {r.Reason}\n");
            var others = r.Alarms.Skip(1).ToList();
            if (r.Verdict != Verdict.Ok)
                sb.Append(others.Count == 0
                    ? "  No other rule fired.\n"
                    : $"  Also fired: {string.Join(", ", others.Select(a => $"{a.Kind.Upper()} at {VerdictText.Clock(a.T)}"))}.\n");
            return sb.ToString();
        }
    }
}
