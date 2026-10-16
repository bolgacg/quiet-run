# quiet-run

A run-health monitor for liquid chromatography (LC) runs, written in C# on .NET 8.

It reads a run one sample at a time (pump pressure, flow, instrument status, heartbeat and method clock, each
with a timestamp) and gives every run a verdict with a plain-English reason:

| Verdict | What the pump shows |
|---|---|
| OK | Nothing unusual. |
| LEAK | Pressure sits below what this method normally shows at that point of the run. |
| CLOG | Pressure sits above it. |
| AIR | Several short dips or spikes while the average holds. |
| DRIFT | Each run's average creeps away from normal, run after run, while every run on its own looks fine. |
| QUIET | The instrument says Running and its heartbeat arrives, but the method clock has stopped moving. |

The page at **https://bolgacg.github.io/quiet-run/** shows how quickly each fault is flagged against a fixed high
and low pressure limit, on 500 synthetic faulty runs and 500 healthy ones, including where quiet-run fails.

**All data in this repository is synthetic.** The only public facts used are the run length (a 21-minute
gradient, as in a 60 samples-per-day method; Bache et al., Molecular & Cellular Proteomics 17(11), 2018) and that
LC instruments record a pressure profile per run. The pressure shape, noise and faults are invented, and pressure
is an index where 100 is the healthy average for the method. It is not Evosep's gradient or the pressure of loading
an Evotip.

The tests and the simulator have the same author, so passing tests show that the code matches its own model of a
run, not that it works on a real instrument.

## Run it

```
dotnet test
dotnet run --project src/QuietRun.Cli -- simulate --out samples
dotnet run --project src/QuietRun.Cli -- check samples/leak.csv --envelope samples/envelope.json
dotnet run --project src/QuietRun.Cli -- study --out docs/data.js
```

`check` prints a verdict per file and exits with 3 if any run was flagged, so it can sit in a script. Several
files are read as consecutive runs, which is what DRIFT needs. `study` regenerates every number on the page from
one seed; running it twice writes the same file, and CI fails if the published `docs/data.js` differs from it.

## Trace format

One CSV row per reading. The study writes one a second, but rows need not be evenly spaced: every rule measures
time from `t_s`, which must increase from row to row.

```
t_s,pressure,flow,status,heartbeat,method_s
0,102.306,99.970,Running,1,0
```

`method_s` is how far through its method the instrument says it is. It is what the QUIET rule watches: a
heartbeat proves the instrument is alive, and only the method clock proves the run is moving.

## How the rules work

- An **envelope** is learned from healthy runs of one method: the expected pressure at each 10-second slice of the
  method, and how far healthy runs stray from it. Pressure is compared with the envelope at the same point of the
  method, so a run that pauses is still compared with the right point.
- **LEAK and CLOG** fire when a 15-second median of the pressure sits more than a set number of spreads below or
  above the envelope for a set time.
- **AIR** fires when several short swings (each a few seconds long) cross a threshold within 90 seconds. A swing
  ends only when it falls back under half the threshold, so a fast ramp counts as one long swing, not many short
  ones.
- **QUIET** fires when the status is Running and the heartbeat is arriving, but the method clock has not moved for
  a set time. Planned pauses in healthy runs are shorter than that time. It assumes a method clock that moves
  steadily; on a system with a pre-formed gradient, such as the Evosep One, it may not apply as written.
- **DRIFT** is a two-sided cumulative sum (Page, 1954) of each run's median deviation, measured in run-to-run
  spreads.

The three settings (relaxed, chosen, sensitive) are in `src/QuietRun/DetectorSettings.cs`. They were fixed before
the study first ran.

## Layout

- `src/QuietRun/`: the library.
  - `Rules/`: one class per rule. `QuietRule`, `LeakRule`, `ClogRule` and `AirRule` implement `IRule` and see
    every sample of one run; `DriftRule` implements `ISequenceRule` and sees each finished run. `RuleSet` builds
    them from an envelope and a setting.
  - `RunMonitor` asks whatever `IRule`s it is given on each sample and lets each kind fire once a run.
    `SequenceMonitor` does the same with `ISequenceRule`s across runs. `RunChecker` checks a whole run.
  - `ISampleSource` is where a run comes from: `CsvSource` or `SimulatedSource`.
  - `SampleWindow` is the 15-second smoothing window, measured by timestamps, on a `RingBuffer`.
  - `Envelope`, `FixedLimits`, `Simulator` and `TraceCsv` are the envelope, the fixed-limit baseline, the
    synthetic runs and the file formats.
- `src/QuietRun.Cli/`: the command-line tool, including the study that writes the page data.
- `tests/QuietRun.Tests/`: xUnit tests, one file per verdict. Each rule has a test showing the same run is not
  flagged when that rule is switched off. `TimingTests` feed the monitor jittered, lost and unevenly spaced
  readings; `CompositionTests` and `WindowTests` cover the rule interface, the sources and the window.
- `docs/`: the page. `data.js` and `tests.js` are generated; do not edit them by hand.
- `samples/`: one synthetic run per verdict and the envelope they are checked against.

## Licence

MIT. See `LICENSE`.
