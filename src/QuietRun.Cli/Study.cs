using System.Text.Json;
using QuietRun;

namespace QuietRun.Cli;

/// <summary>
/// The synthetic study behind the page. Everything is generated from one seed, so running it again writes the
/// same file. Every number the page shows comes from the file this writes.
/// </summary>
public static class Study
{
    public const int TrainingRuns = 40;
    public const int HealthySequences = 25;
    public const int SequenceRuns = 20;
    public const int Reps = 25;
    public const int DriftStartRun = 5;
    public const double FaultStartMin = 240, FaultStartSpan = 660;

    private static readonly Verdict[] InRunKinds = [Verdict.Leak, Verdict.Clog, Verdict.Air, Verdict.Quiet];

    private static readonly Dictionary<Verdict, string[]> SeverityLabels = new()
    {
        [Verdict.Leak] = ["2% lost", "4% lost", "7% lost", "12% lost"],
        [Verdict.Clog] = ["3% rise", "6% rise", "10% rise", "18% rise"],
        [Verdict.Air] = ["2% dips", "4% dips", "8% dips", "15% dips"],
        [Verdict.Quiet] = ["20 s", "45 s", "2 min", "for good"],
        [Verdict.Drift] = ["0.25% a run", "0.5% a run", "1% a run", "2% a run"],
    };

    private sealed record Outcome(double? Delay, Verdict? Kind, bool Early);

    private static double R(double x, int d) => Math.Round(x, d, MidpointRounding.AwayFromZero);
    private static double? R(double? x, int d) => x is null ? null : R(x.Value, d);
    private static string K(Verdict v) => v.ToString().ToLowerInvariant();

    public static int Run(int seed, string outPath)
    {
        var started = DateTime.UtcNow;
        var master = new Random(seed);
        int trainingSeed = master.Next(), healthySeed = master.Next();
        var training = Simulator.HealthyRuns(TrainingRuns, trainingSeed);
        var env = Envelope.Learn(training);
        var limits = FixedLimits.Learn(training);
        var settings = DetectorSettings.All;

        object AlarmsJson(IEnumerable<Alarm> alarms) =>
            alarms.Select(a => new { k = K(a.Kind), t = R(a.T, 0), r = a.Reason }).ToList();

        object TraceJson(IReadOnlyList<Sample> run) => new
        {
            p = run.Select(s => R(s.Pressure, 1)).ToList(),
            m = run.Select(s => (int)Math.Round(s.MethodS)).ToList(),
        };

        // ---------- healthy runs: false alarms ----------
        var hrng = new Random(healthySeed);
        var healthyFlagged = settings.ToDictionary(s => s.Name, _ => 0);
        var healthyByKind = settings.ToDictionary(s => s.Name, _ => new SortedDictionary<string, int>());
        int fixedHealthyFlagged = 0;
        object? healthyExample = null, healthyFlaggedExample = null;
        for (int q = 0; q < HealthySequences; q++)
        {
            var drifts = settings.Select(s => new SequenceMonitor(env, s)).ToArray();
            for (int i = 0; i < SequenceRuns; i++)
            {
                int rs = hrng.Next();
                double off = Simulator.DrawRunOffset(hrng);
                var run = Simulator.Run(rs, off);
                var reports = settings.Select((s, k) => RunChecker.Check(run, env, s, drifts[k])).ToArray();
                for (int k = 0; k < settings.Count; k++)
                {
                    if (reports[k].Verdict == Verdict.Ok) continue;
                    healthyFlagged[settings[k].Name]++;
                    var dict = healthyByKind[settings[k].Name];
                    dict[K(reports[k].Verdict)] = dict.GetValueOrDefault(K(reports[k].Verdict)) + 1;
                }
                var fixedAlarm = FixedLimitMonitor.Check(run, limits);
                if (fixedAlarm is not null) fixedHealthyFlagged++;

                object Example(string why) => new
                {
                    kind = "healthy", sev = 0, label = why, start = (double?)null, offsetPct = R(off * 100, 2),
                    sequence = q, runInSequence = i,
                    trace = TraceJson(run),
                    alarms = settings.ToDictionary(s => s.Name, s => AlarmsJson(reports[settings.ToList().IndexOf(s)].Alarms)),
                    @fixed = fixedAlarm is null ? null : new { t = R(fixedAlarm.T, 0), r = fixedAlarm.Reason },
                };
                int chosenIdx = settings.ToList().IndexOf(DetectorSettings.Chosen);
                if (healthyExample is null && reports.All(r => r.Verdict == Verdict.Ok) && fixedAlarm is null)
                    healthyExample = Example("a healthy run");
                if (healthyFlaggedExample is null && reports[chosenIdx].Verdict != Verdict.Ok)
                    healthyFlaggedExample = Example("a healthy run that got a false alarm");
            }
        }

        // ---------- in-run faults ----------
        var cells = new List<object>();
        var examples = new Dictionary<string, object?>();
        var confusion = settings.ToDictionary(s => s.Name, _ => new Dictionary<string, Dictionary<string, int>>());
        void Count(string setting, Verdict injected, Verdict? got)
        {
            var row = confusion[setting].TryGetValue(K(injected), out var r) ? r : confusion[setting][K(injected)] = new();
            string key = got is null ? "ok" : K(got.Value);
            row[key] = row.GetValueOrDefault(key) + 1;
        }
        string? cliExample = null;

        foreach (var kind in InRunKinds)
        {
            for (int sev = 1; sev <= 4; sev++)
            {
                double size = Simulator.Severities[kind][sev - 1];
                var crng = new Random(master.Next());
                var plans = new List<(int Seed, double Off, double Start)>();
                var qr = settings.ToDictionary(s => s.Name, _ => new List<Outcome>());
                var fixedOut = new List<Outcome>();
                for (int rep = 0; rep < Reps; rep++)
                {
                    int rs = crng.Next();
                    double off = Simulator.DrawRunOffset(crng);
                    double start = Math.Round(FaultStartMin + crng.NextDouble() * FaultStartSpan);
                    plans.Add((rs, off, start));
                    var run = Simulator.Run(rs, off, new FaultSpec(kind, size, start));
                    foreach (var s in settings)
                    {
                        var rep0 = RunChecker.Check(run, env, s);
                        var post = rep0.Alarms.FirstOrDefault(a => a.T >= start);
                        qr[s.Name].Add(new Outcome(post?.T - start, post?.Kind, rep0.Alarms.Any(a => a.T < start)));
                        Count(s.Name, kind, post?.Kind);
                    }
                    var fa = FixedLimitMonitor.Check(run, limits);
                    fixedOut.Add(fa is null ? new Outcome(null, null, false)
                        : fa.T >= start ? new Outcome(fa.T - start, fa.Kind, false) : new Outcome(null, null, true));
                }

                int pick = Representative(qr[DetectorSettings.Chosen.Name]);
                var (ps, po, pst) = plans[pick];
                var exRun = Simulator.Run(ps, po, new FaultSpec(kind, size, pst));
                var exReports = settings.ToDictionary(s => s.Name, s => RunChecker.Check(exRun, env, s));
                var exFixed = FixedLimitMonitor.Check(exRun, limits);
                examples[$"{K(kind)}-{sev}"] = new
                {
                    kind = K(kind), sev, label = SeverityLabels[kind][sev - 1], start = (double?)pst, offsetPct = R(po * 100, 2),
                    rep = pick,
                    trace = TraceJson(exRun),
                    alarms = settings.ToDictionary(s => s.Name, s => AlarmsJson(exReports[s.Name].Alarms)),
                    @fixed = exFixed is null ? null : new { t = R(exFixed.T, 0), r = exFixed.Reason },
                };
                if (kind == Verdict.Leak && sev == 3)
                    cliExample = Cli.Format("leak-7pct.csv", exReports[DetectorSettings.Chosen.Name]);

                cells.Add(new
                {
                    kind = K(kind), sev, label = SeverityLabels[kind][sev - 1], unit = "s",
                    start = plans.Select(p => p.Start).ToList(),
                    floor = plans.Select(p => (double)Simulator.RunSeconds - p.Start).ToList(),
                    qr = settings.ToDictionary(s => s.Name, s => new
                    {
                        delay = qr[s.Name].Select(o => R(o.Delay, 0)).ToList(),
                        kind = qr[s.Name].Select(o => o.Kind is null ? null : K(o.Kind.Value)).ToList(),
                        early = qr[s.Name].Count(o => o.Early),
                    }),
                    @fixed = new
                    {
                        delay = fixedOut.Select(o => R(o.Delay, 0)).ToList(),
                        early = fixedOut.Count(o => o.Early),
                    },
                });
            }
        }

        // ---------- drift across runs ----------
        for (int sev = 1; sev <= 4; sev++)
        {
            double size = Simulator.Severities[Verdict.Drift][sev - 1];
            var crng = new Random(master.Next());
            var qr = settings.ToDictionary(s => s.Name, _ => new List<Outcome>());
            var fixedOut = new List<Outcome>();
            var plans = new List<List<(int Seed, double Off, double Creep)>>();
            for (int rep = 0; rep < Reps; rep++)
            {
                var drifts = settings.Select(s => new SequenceMonitor(env, s)).ToArray();
                var first = new Outcome?[settings.Count];
                var early = new bool[settings.Count];
                Outcome? fixedFirst = null;
                bool fixedEarly = false;
                var plan = new List<(int, double, double)>();
                for (int i = 0; i < SequenceRuns; i++)
                {
                    int rs = crng.Next();
                    double off = Simulator.DrawRunOffset(crng);
                    double creep = i >= DriftStartRun ? size * (i - DriftStartRun + 1) : 0;
                    plan.Add((rs, off, creep));
                    var run = Simulator.Run(rs, off, null, creep);
                    for (int k = 0; k < settings.Count; k++)
                    {
                        var rep0 = RunChecker.Check(run, env, settings[k], drifts[k]);
                        if (rep0.Alarms.Count == 0) continue;
                        if (i < DriftStartRun) { early[k] = true; continue; }
                        first[k] ??= new Outcome((i - DriftStartRun) + (rep0.Alarms[0].T + 1) / Simulator.RunSeconds, rep0.Alarms[0].Kind, false);
                    }
                    var fa = FixedLimitMonitor.Check(run, limits);
                    if (fa is not null)
                    {
                        if (i < DriftStartRun) fixedEarly = true;
                        else fixedFirst ??= new Outcome((i - DriftStartRun) + (fa.T + 1) / Simulator.RunSeconds, fa.Kind, false);
                    }
                }
                plans.Add(plan);
                for (int k = 0; k < settings.Count; k++)
                {
                    var o = first[k] ?? new Outcome(null, null, false);
                    qr[settings[k].Name].Add(o with { Early = early[k] });
                    Count(settings[k].Name, Verdict.Drift, o.Kind);
                }
                fixedOut.Add((fixedFirst ?? new Outcome(null, null, false)) with { Early = fixedEarly });
            }

            int pick = Representative(qr[DetectorSettings.Chosen.Name]);
            examples[$"drift-{sev}"] = DriftExample(plans[pick], sev, size, pick);

            cells.Add(new
            {
                kind = "drift", sev, label = SeverityLabels[Verdict.Drift][sev - 1], unit = "runs",
                start = Enumerable.Repeat((double)DriftStartRun, Reps).ToList(),
                floor = Enumerable.Repeat((double)(SequenceRuns - DriftStartRun), Reps).ToList(),
                qr = settings.ToDictionary(s => s.Name, s => new
                {
                    delay = qr[s.Name].Select(o => R(o.Delay, 3)).ToList(),
                    kind = qr[s.Name].Select(o => o.Kind is null ? null : K(o.Kind.Value)).ToList(),
                    early = qr[s.Name].Count(o => o.Early),
                }),
                @fixed = new
                {
                    delay = fixedOut.Select(o => R(o.Delay, 3)).ToList(),
                    early = fixedOut.Count(o => o.Early),
                },
            });
        }

        object DriftExample(List<(int Seed, double Off, double Creep)> plan, int sev, double size, int pick)
        {
            var drifts = settings.Select(s => new SequenceMonitor(env, s)).ToArray();
            var medians = new List<double>();
            var fixedRuns = new List<bool>();
            var firstAlarm = settings.ToDictionary(s => s.Name, _ => (object?)null);
            var firstRun = settings.ToDictionary(s => s.Name, _ => -1);
            for (int i = 0; i < plan.Count; i++)
            {
                var run = Simulator.Run(plan[i].Seed, plan[i].Off, null, plan[i].Creep);
                for (int k = 0; k < settings.Count; k++)
                {
                    var rep0 = RunChecker.Check(run, env, settings[k], drifts[k]);
                    if (settings[k] == DetectorSettings.Chosen) medians.Add(rep0.MedianDeviation);
                    if (i >= DriftStartRun && firstRun[settings[k].Name] < 0 && rep0.Alarms.Count > 0)
                    {
                        firstRun[settings[k].Name] = i;
                        firstAlarm[settings[k].Name] = new { run = i, alarms = AlarmsJson(rep0.Alarms) };
                    }
                }
                fixedRuns.Add(FixedLimitMonitor.Check(run, limits) is not null);
            }
            int show = firstRun[DetectorSettings.Chosen.Name] >= 0 ? firstRun[DetectorSettings.Chosen.Name] : plan.Count - 1;
            var shown = Simulator.Run(plan[show].Seed, plan[show].Off, null, plan[show].Creep);
            var shownFixed = FixedLimitMonitor.Check(shown, limits);
            return new
            {
                kind = "drift", sev, label = SeverityLabels[Verdict.Drift][sev - 1], rep = pick,
                start = (double?)null, offsetPct = R(plan[show].Off * 100, 2), creepPct = R(plan[show].Creep * 100, 2),
                sequence = new
                {
                    medianPct = medians.Select(m => R(m * 100, 2)).ToList(),
                    creepPct = plan.Select(p => R(p.Creep * 100, 2)).ToList(),
                    fixedTripped = fixedRuns,
                    first = firstAlarm,
                },
                shownRun = show,
                trace = TraceJson(shown),
                alarms = settings.ToDictionary(s => s.Name, s =>
                {
                    var d = new SequenceMonitor(env, s);
                    RunReport? rep0 = null;
                    for (int i = 0; i <= show; i++)
                        rep0 = RunChecker.Check(i == show ? shown : Simulator.Run(plan[i].Seed, plan[i].Off, null, plan[i].Creep), env, s, d);
                    return AlarmsJson(rep0!.Alarms);
                }),
                @fixed = shownFixed is null ? null : new { t = R(shownFixed.T, 0), r = shownFixed.Reason },
            };
        }

        var data = new
        {
            meta = new
            {
                seed,
                command = $"quiet-run study --seed {seed}",
                runSeconds = Simulator.RunSeconds,
                trainingRuns = TrainingRuns,
                healthyRuns = HealthySequences * SequenceRuns,
                healthySequences = HealthySequences,
                sequenceRuns = SequenceRuns,
                driftStartRun = DriftStartRun,
                reps = Reps,
                faultStartMin = FaultStartMin,
                faultStartMax = FaultStartMin + FaultStartSpan,
                leakRampS = Simulator.LeakRampS,
                clogRampS = Simulator.ClogRampS,
                airBurstS = Simulator.AirBurstS,
                airDips = Simulator.AirDips,
                smoothWindowS = Envelope.SmoothSeconds,
                fixedDebounceS = FixedLimits.DebounceS,
                fixedMarginIndex = FixedLimits.DefaultMargin,
                pauseMaxS = Simulator.PauseMaxS,
                severitySizes = Simulator.Severities.ToDictionary(kv => K(kv.Key), kv => kv.Value.Select(x => double.IsInfinity(x) ? (double?)null : x).ToList()),
                severityLabels = SeverityLabels.ToDictionary(kv => K(kv.Key), kv => kv.Value),
            },
            envelope = new
            {
                binSeconds = env.BinSeconds,
                expected = env.Expected.Select(x => R(x, 2)).ToList(),
                levelSigmaPct = R(env.LevelSigma * 100, 3),
                spikeSigmaPct = R(env.SpikeSigma * 100, 3),
                runSigmaPct = R(env.RunSigma * 100, 3),
            },
            limits = new { low = R(limits.Low, 2), high = R(limits.High, 2) },
            settings = settings.Select(s => new
            {
                name = s.Name,
                levelPct = R(s.LevelZ * env.LevelSigma * 100, 2),
                levelZ = s.LevelZ,
                holdS = s.LevelHoldS,
                spikePct = R(s.SpikeZ * env.SpikeSigma * 100, 2),
                spikeCount = s.SpikeCount,
                spikeWindowS = s.SpikeWindowS,
                spikeMaxS = s.SpikeMaxS,
                quietS = s.QuietS,
                driftK = s.DriftK,
                driftH = s.DriftH,
            }).ToList(),
            healthy = new
            {
                runs = HealthySequences * SequenceRuns,
                qr = settings.ToDictionary(s => s.Name, s => new { flagged = healthyFlagged[s.Name], byKind = healthyByKind[s.Name] }),
                @fixed = new { flagged = fixedHealthyFlagged },
            },
            cells,
            confusion,
            examples = examples.Append(new("healthy", healthyExample)).Append(new("healthy-flagged", healthyFlaggedExample))
                .ToDictionary(kv => kv.Key, kv => kv.Value),
            cliExample,
        };

        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = false });
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath))!);
        File.WriteAllText(outPath,
            $"// Generated by `quiet-run study --seed {seed}`. Every number on the page comes from this file. Do not edit by hand.\n" +
            "window.QR = " + json + ";\n");

        // A short console summary, so a run of the study can be read without the page.
        var chosen = DetectorSettings.Chosen.Name;
        Console.WriteLine($"envelope: level spread {env.LevelSigma:P2}, spike spread {env.SpikeSigma:P2}, run spread {env.RunSigma:P2}; fixed limits {limits.Low:0.0} to {limits.High:0.0}");
        Console.WriteLine($"healthy runs flagged ({HealthySequences * SequenceRuns}): " +
                          string.Join(", ", settings.Select(s => $"{s.Name} {healthyFlagged[s.Name]}")) + $", fixed limits {fixedHealthyFlagged}");
        Console.WriteLine($"wrote {outPath} in {(DateTime.UtcNow - started).TotalSeconds:0.0} s");
        return 0;
    }

    /// <summary>The replicate the page shows for a cell: the median catch if most were caught, else the first miss.</summary>
    private static int Representative(List<Outcome> outcomes)
    {
        var caught = outcomes.Select((o, i) => (o, i)).Where(x => x.o.Delay is not null).ToList();
        if (caught.Count * 2 >= outcomes.Count)
        {
            double med = Stats.Median(caught.Select(x => x.o.Delay!.Value).ToList());
            return caught.OrderBy(x => Math.Abs(x.o.Delay!.Value - med)).ThenBy(x => x.i).First().i;
        }
        return outcomes.FindIndex(o => o.Delay is null);
    }
}
