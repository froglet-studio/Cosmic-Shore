using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace CosmicShore.Utility.Tools.DensityPartitionBenchmark
{
    /// <summary>
    /// Edit-Mode temporal ecology simulator for the density-partition system.
    ///
    /// The geometric benchmark (DensityPartitionBenchmarkRunner) answers "is one
    /// query accurate?". This answers the question that actually matters: "over
    /// time, does the algorithm + flora growth + fauna consumption produce the
    /// two-frequency oscillation the design wants — or a static plateau?"
    ///
    /// It models the production loop faithfully enough to be load-bearing:
    ///   - Flora at fixed positions spawn prisms of their own domain while the
    ///     cell phase is below Frozen (FloraGrowingEnabled).
    ///   - Cell phase is driven by total prism count through CellPhaseRules /
    ///     CellPhaseThresholds — the real production transition table.
    ///   - Fauna spawn for the *dominant* domain once phase >= Quiet, seek the
    ///     density algorithm's anti-own-domain target (any-domain at Rabid),
    ///     and consume opposing-domain prisms within consumeRadius.
    ///
    /// The headline test: run the whole sim twice — once with the LITERAL shipped
    /// grid (fixed ±500m box) and once with the cell-sized grid — and compare. If
    /// the shipped grid can't see the outer shell, outer-shell flora produce
    /// prisms no fauna is ever directed at, that mass accumulates monotonically,
    /// and the cell pins at Rabid: a plateau, no oscillation possible. The
    /// cell-sized grid lets fauna reach all the mass, so the count can fall as
    /// well as rise — oscillation becomes possible.
    ///
    /// It is also an ecology *tuning* bench: flora/fauna rates are serialized, so
    /// the parameter regime that produces the desired oscillation can be searched
    /// here instead of by eyeballing Menu_Main.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class DensityPartitionTemporalSimRunner : MonoBehaviour
    {
        [Header("Sim duration")]
        [Tooltip("Total simulated seconds.")]
        [Min(10f)] public float simDurationSeconds = 300f;
        [Tooltip("Fixed timestep. Matches Cell's 0.5s phase tick.")]
        [Min(0.05f)] public float tickIntervalSeconds = 0.5f;

        [Header("Cell")]
        [Tooltip("Cell membrane radius (m). Blob cell is 1200.")]
        [Min(100f)] public float cellMembraneRadius = 1200f;
        [Tooltip("Deterministic RNG seed.")]
        public int seed = 42;

        [Header("Flora layout")]
        [Tooltip("Flora placed within the production grid's ±500m box (the shipped grid CAN see these).")]
        [Min(0)] public int innerFloraCount = 60;
        [Tooltip("Flora placed in the 500m..(membrane-100m) outer shell (the shipped ±500m grid CANNOT see these).")]
        [Min(0)] public int outerFloraCount = 60;
        [Tooltip("Seconds between prism spawns per flora.")]
        [Min(0.1f)] public float floraGrowthIntervalSeconds = 1.5f;
        [Tooltip("Gaussian scatter radius (m) of spawned prisms around their flora.")]
        [Min(1f)] public float floraScatterRadius = 80f;

        [Header("Phase thresholds")]
        [Tooltip("CellPhaseThresholds.Default scaled by this. Default is tuned for thousands " +
                 "of prisms; 0.15 gives Quiet~150 / Frozen~1500 / Rabid~2250 for a fast sim.")]
        [Range(0.02f, 1f)] public float thresholdScale = 0.15f;

        [Header("Fauna")]
        [Tooltip("Consume radius (m). MassShark/Brittlestar SOs use 40.")]
        [Min(1f)] public float faunaConsumeRadius = 40f;
        [Tooltip("Max opposing prisms one fauna eats per tick while on target.")]
        [Min(1)] public int faunaConsumePerTickInRange = 3;
        [Tooltip("Fauna travel speed (m/s).")]
        [Min(10f)] public float faunaSpeed = 250f;
        [Tooltip("Seconds between a fauna re-querying the density algorithm for a new goal.")]
        [Min(0.5f)] public float faunaGoalUpdateIntervalSeconds = 5f;
        [Tooltip("Per-fauna stable orbit offset radius (m) — spreads the swarm around the goal.")]
        [Min(0f)] public float faunaGoalOrbitRadius = 60f;
        [Tooltip("Max simultaneous fauna.")]
        [Min(1)] public int faunaPopulationCap = 40;
        [Tooltip("Seconds between fauna spawns (for the dominant domain) once phase >= Quiet.")]
        [Min(0.25f)] public float faunaSpawnIntervalSeconds = 2f;

        [Header("Output")]
        [Tooltip("Sample the time series for the report this often (simulated seconds).")]
        [Min(1f)] public float timeSeriesSampleEverySeconds = 10f;
        [TextArea(10, 48)] public string lastReport = "";

        // ==================================================================
        //  Public API
        // ==================================================================

        [ContextMenu("Run Temporal Sim (old grid vs new grid)")]
        public string RunComparison()
        {
            var sb = new StringBuilder(8192);
            string dateUtc = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture);

            sb.AppendLine("=== DensityPartitionTemporalSim v1.0 ===");
            sb.AppendLine($"Date: {dateUtc}   Seed: {seed}");
            sb.AppendLine($"Cell membrane radius: {cellMembraneRadius:F0}m   Sim: {simDurationSeconds:F0}s @ {tickIntervalSeconds:F2}s/tick");
            sb.AppendLine($"Flora: {innerFloraCount} inner (within ±500m) + {outerFloraCount} outer shell (>500m)");
            var th = ScaledThresholds();
            sb.AppendLine($"Phase thresholds (Default × {thresholdScale:F2}): Quiet {th.QuietEnter}/{th.QuietExit}  " +
                          $"Settled {th.SettledEnter}/{th.SettledExit}  Restless {th.RestlessEnter}/{th.RestlessExit}  " +
                          $"Frozen {th.FrozenEnter}/{th.FrozenExit}  Rabid {th.RabidEnter}/{th.RabidExit}");
            float floraRate = (innerFloraCount + outerFloraCount) / Mathf.Max(0.01f, floraGrowthIntervalSeconds);
            sb.AppendLine($"Flora production capacity: {floraRate:F0} prisms/s   " +
                          $"Fauna consumption capacity: ~{faunaPopulationCap * faunaConsumePerTickInRange / Mathf.Max(0.01f, tickIntervalSeconds):F0} prisms/s (cap × rate)");
            sb.AppendLine();
            sb.AppendLine("Headline test: the shipped grid is a fixed ±500m box. Outer-shell flora");
            sb.AppendLine("produce prisms it cannot see → no fauna is ever directed there → that mass");
            sb.AppendLine("accumulates monotonically → the cell pins at Rabid (plateau). The cell-sized");
            sb.AppendLine("grid lets fauna reach all the mass, so the count can fall as well as rise.");
            sb.AppendLine();

            // --- Run A: the LITERAL shipped grid (fixed ±500m box) ---
            var optOld = DensityPartitionBenchmarkAlgorithms.ProductionFixedGrid17();
            var resultOld = RunOne(optOld);
            AppendRun(sb, "OLD — ProductionFixedGrid17 (fixed ±500m box, the shipped grid)", resultOld, th);

            // --- Run B: the cell-sized smoothed grid (the c058663 fix) ---
            var optNew = DensityPartitionBenchmarkAlgorithms.GridSmoothedInterp17();
            var resultNew = RunOne(optNew);
            AppendRun(sb, "NEW — GridSmoothedInterp17 (grid sized to the cell + smoothing + interp)", resultNew, th);

            // --- Verdict ---
            sb.AppendLine("=== Verdict ===");
            sb.AppendLine($"  OLD grid: {resultOld.Verdict}");
            sb.AppendLine($"  NEW grid: {resultNew.Verdict}");
            sb.AppendLine();
            // The robust discriminator is whether outer-shell mass is bounded — it
            // doesn't depend on sim length the way IsPlateau does. If the grid can
            // see the whole cell, fauna get directed at outer-shell clusters and
            // that mass cycles; if not, it accumulates forever.
            bool oldBlind = !resultOld.OuterShellBounded;
            bool newSees = resultNew.OuterShellBounded;
            if (oldBlind && newSees)
            {
                sb.AppendLine("  CONFIRMED: with the shipped ±500m grid, outer-shell mass is never consumed —");
                sb.AppendLine("  it accumulates monotonically because no fauna is ever directed there. With the");
                sb.AppendLine("  cell-sized grid, fauna reach the whole cell and outer-shell mass cycles.");
                sb.AppendLine("  The grid-sizing fix (c058663) is a necessary condition for the ecosystem to");
                sb.AppendLine("  oscillate instead of plateau. Secondary (Rabid) cycle tuning is now unblocked.");
            }
            else if (oldBlind && !newSees)
            {
                sb.AppendLine("  The shipped grid is blind to the outer shell (as expected) — but the cell-sized");
                sb.AppendLine("  grid ALSO leaves outer-shell mass unbounded. The grid fix landed, so this points");
                sb.AppendLine("  at a rate-balance problem: consumption capacity can't keep up with flora");
                sb.AppendLine("  production. Tune floraGrowthInterval up / faunaPopulationCap up / faunaConsume up.");
            }
            else if (!oldBlind && newSees)
            {
                sb.AppendLine("  Both grids keep outer-shell mass bounded — re-check that outerFloraCount > 0 and");
                sb.AppendLine("  that the outer flora actually sit outside ±500m (per-run outer-shell peak > 0).");
                sb.AppendLine("  If the OLD run shows a healthy outer-shell peak that still gets consumed, the");
                sb.AppendLine("  sim's fauna may be wandering into the outer shell by chance — tighten faunaSpeed");
                sb.AppendLine("  or goal cadence so they stay on the (blind) grid's targets.");
            }
            else
            {
                sb.AppendLine("  Inconclusive at the current parameters. Inspect the per-run tables above.");
            }

            lastReport = sb.ToString();
            Debug.Log("[DensityPartitionTemporalSim]\n" + lastReport);
            return lastReport;
        }

        // ==================================================================
        //  Sim entities
        // ==================================================================

        struct SimPrism { public Vector3 Pos; public Domains Domain; public bool Alive; }
        struct SimFlora { public Vector3 Pos; public Domains Domain; public float GrowthClock; }
        struct SimFauna { public Vector3 Pos; public Domains Domain; public Vector3 Goal; public Vector3 OrbitOffset; public float GoalClock; }

        struct TickRecord
        {
            public float Time;
            public int Total, Jade, Ruby, Gold;
            public int OuterShellPrisms; // prisms whose |pos| component exceeds 500m
            public CellPhase Phase;
            public CellAggressionLevel Aggression;
            public int FaunaCount;
            public Domains Dominant;
        }

        class RunResult
        {
            public List<TickRecord> Series = new();
            public string Verdict;
            public bool IsPlateau;
            public int PeakOuterShellPrisms;
            public int CycleCount;
            public int RabidEntries;
            public float MinTotalSteady, MaxTotalSteady;
            // The headline OLD-vs-NEW signal: is outer-shell mass bounded (fauna
            // are directed there and consume it) or unbounded (the grid can't
            // see it, so it accumulates forever)?
            public bool OuterShellBounded;
            public int OuterShellMinSteady, OuterShellMaxSteady;
        }

        // ==================================================================
        //  Sim core
        // ==================================================================

        RunResult RunOne(SearchOptions densityOpt)
        {
            var rng = new System.Random(seed);
            var thresholds = ScaledThresholds();

            // --- Place flora deterministically ---
            var flora = new List<SimFlora>(innerFloraCount + outerFloraCount);
            Domains[] domainCycle = { Domains.Jade, Domains.Ruby, Domains.Gold };
            for (int i = 0; i < innerFloraCount; i++)
                flora.Add(new SimFlora
                {
                    Pos = RandomInBall(rng, 480f),
                    Domain = domainCycle[i % 3],
                    GrowthClock = (float)rng.NextDouble() * floraGrowthIntervalSeconds, // stagger
                });
            for (int i = 0; i < outerFloraCount; i++)
                flora.Add(new SimFlora
                {
                    Pos = RandomInShell(rng, 520f, Mathf.Max(560f, cellMembraneRadius - 100f)),
                    Domain = domainCycle[i % 3],
                    GrowthClock = (float)rng.NextDouble() * floraGrowthIntervalSeconds,
                });

            var prisms = new List<SimPrism>(4096);
            var fauna = new List<SimFauna>(faunaPopulationCap);
            var benchPrisms = new List<BenchmarkPrism>(4096); // reused per tick

            CellPhase phase = CellPhase.Sprout;
            float faunaSpawnClock = 0f;
            int tickCount = Mathf.CeilToInt(simDurationSeconds / tickIntervalSeconds);
            float dt = tickIntervalSeconds;
            float nextSampleAt = 0f;

            var result = new RunResult();

            for (int tick = 0; tick < tickCount; tick++)
            {
                float t = tick * dt;

                // --- 1. Recompute counts / phase / dominant domain ---
                int jade = 0, ruby = 0, gold = 0, outerShell = 0;
                for (int i = 0; i < prisms.Count; i++)
                {
                    var p = prisms[i];
                    if (!p.Alive) continue;
                    switch (p.Domain)
                    {
                        case Domains.Jade: jade++; break;
                        case Domains.Ruby: ruby++; break;
                        case Domains.Gold: gold++; break;
                    }
                    if (Mathf.Abs(p.Pos.x) > 500f || Mathf.Abs(p.Pos.y) > 500f || Mathf.Abs(p.Pos.z) > 500f)
                        outerShell++;
                }
                int total = jade + ruby + gold;
                phase = CellPhaseRules.Compute(total, phase, in thresholds);
                CellAggressionLevel aggression = phase switch
                {
                    CellPhase.Restless => CellAggressionLevel.Level1,
                    CellPhase.Frozen => CellAggressionLevel.Level1,
                    CellPhase.Rabid => CellAggressionLevel.Level2,
                    _ => CellAggressionLevel.Level0,
                };
                Domains dominant =
                    jade >= ruby && jade >= gold ? Domains.Jade :
                    ruby >= gold ? Domains.Ruby : Domains.Gold;

                // --- 2. Flora growth (gated: phase < Frozen, matching FloraGrowingEnabled) ---
                bool floraGrowing = phase < CellPhase.Frozen;
                if (floraGrowing)
                {
                    for (int i = 0; i < flora.Count; i++)
                    {
                        var f = flora[i];
                        f.GrowthClock += dt;
                        while (f.GrowthClock >= floraGrowthIntervalSeconds)
                        {
                            f.GrowthClock -= floraGrowthIntervalSeconds;
                            prisms.Add(new SimPrism
                            {
                                Pos = f.Pos + GaussianVec(rng) * floraScatterRadius,
                                Domain = f.Domain,
                                Alive = true,
                            });
                        }
                        flora[i] = f;
                    }
                }

                // --- 3. Fauna spawn (gated: phase >= Quiet, for the dominant domain) ---
                bool faunaSpawning = phase >= CellPhase.Quiet;
                faunaSpawnClock += dt;
                if (faunaSpawning && faunaSpawnClock >= faunaSpawnIntervalSeconds && fauna.Count < faunaPopulationCap)
                {
                    faunaSpawnClock = 0f;
                    fauna.Add(new SimFauna
                    {
                        Pos = RandomInBall(rng, cellMembraneRadius * 0.5f),
                        Domain = dominant,
                        Goal = Vector3.zero,
                        OrbitOffset = RandomOnSphere(rng) * faunaGoalOrbitRadius,
                        GoalClock = 999f, // force an immediate goal query
                    });
                }

                // --- 4. Fauna behaviour ---
                if (fauna.Count > 0)
                {
                    // Build the BenchmarkPrism snapshot once per tick — reused by every
                    // fauna's density query this tick (allocation-free, the list is reused).
                    benchPrisms.Clear();
                    for (int i = 0; i < prisms.Count; i++)
                    {
                        var p = prisms[i];
                        if (p.Alive)
                            benchPrisms.Add(new BenchmarkPrism { Position = p.Pos, Domain = p.Domain, Volume = 1f });
                    }

                    float r2 = faunaConsumeRadius * faunaConsumeRadius;
                    for (int fi = 0; fi < fauna.Count; fi++)
                    {
                        var fa = fauna[fi];

                        // Re-query the density algorithm on the goal cadence.
                        fa.GoalClock += dt;
                        if (fa.GoalClock >= faunaGoalUpdateIntervalSeconds)
                        {
                            fa.GoalClock = 0f;
                            // Level2 (Rabid): any-domain densest. Else: anti-own-domain.
                            Domains? exclude = aggression == CellAggressionLevel.Level2 ? (Domains?)null : fa.Domain;
                            var r = DensityPartitionBenchmarkAlgorithms.Search(
                                benchPrisms, exclude, cellMembraneRadius, densityOpt);
                            fa.Goal = r.Empty ? Vector3.zero : r.Location + fa.OrbitOffset;
                        }

                        // Move toward the goal.
                        Vector3 toGoal = fa.Goal - fa.Pos;
                        float dist = toGoal.magnitude;
                        float step = faunaSpeed * dt;
                        fa.Pos = dist <= step || dist < 0.001f ? fa.Goal : fa.Pos + toGoal * (step / dist);

                        // Consume opposing-domain prisms within range.
                        int eaten = 0;
                        for (int pi = 0; pi < prisms.Count && eaten < faunaConsumePerTickInRange; pi++)
                        {
                            var p = prisms[pi];
                            if (!p.Alive || p.Domain == fa.Domain) continue;
                            if ((p.Pos - fa.Pos).sqrMagnitude > r2) continue;
                            p.Alive = false;
                            prisms[pi] = p;
                            eaten++;
                        }

                        fauna[fi] = fa;
                    }
                }

                // --- 5. Compact dead prisms (swap-remove) ---
                for (int i = prisms.Count - 1; i >= 0; i--)
                {
                    if (!prisms[i].Alive)
                    {
                        prisms[i] = prisms[prisms.Count - 1];
                        prisms.RemoveAt(prisms.Count - 1);
                    }
                }

                // --- 6. Record (sampled) ---
                if (t >= nextSampleAt || tick == tickCount - 1)
                {
                    nextSampleAt += timeSeriesSampleEverySeconds;
                    result.Series.Add(new TickRecord
                    {
                        Time = t,
                        Total = total, Jade = jade, Ruby = ruby, Gold = gold,
                        OuterShellPrisms = outerShell,
                        Phase = phase, Aggression = aggression,
                        FaunaCount = fauna.Count,
                        Dominant = dominant,
                    });
                }
            }

            Analyze(result, thresholds);
            return result;
        }

        // ==================================================================
        //  Analysis
        // ==================================================================

        void Analyze(RunResult result, CellPhaseThresholds thresholds)
        {
            var series = result.Series;
            if (series.Count == 0) { result.Verdict = "no data"; result.IsPlateau = true; return; }

            // Steady-state window = last 60% of the run (skip warmup).
            int start = Mathf.Clamp(Mathf.RoundToInt(series.Count * 0.4f), 0, series.Count - 1);
            int minTotal = int.MaxValue, maxTotal = int.MinValue;
            int peakOuter = 0;
            for (int i = start; i < series.Count; i++)
            {
                minTotal = Mathf.Min(minTotal, series[i].Total);
                maxTotal = Mathf.Max(maxTotal, series[i].Total);
            }
            for (int i = 0; i < series.Count; i++)
                peakOuter = Mathf.Max(peakOuter, series[i].OuterShellPrisms);

            result.MinTotalSteady = minTotal;
            result.MaxTotalSteady = maxTotal;
            result.PeakOuterShellPrisms = peakOuter;

            // Outer-shell trend in the steady window. Bounded = the count comes
            // back down meaningfully (fauna are reaching it). Unbounded = it sits
            // at/near its max (the grid never directed a fauna there).
            int outerMin = int.MaxValue, outerMax = int.MinValue;
            for (int i = start; i < series.Count; i++)
            {
                outerMin = Mathf.Min(outerMin, series[i].OuterShellPrisms);
                outerMax = Mathf.Max(outerMax, series[i].OuterShellPrisms);
            }
            result.OuterShellMinSteady = outerMin;
            result.OuterShellMaxSteady = outerMax;
            result.OuterShellBounded =
                outerMax > 0 && (float)(outerMax - outerMin) / outerMax > 0.15f;

            // Cycle count: midpoint up-crossings in the steady window.
            float mid = (minTotal + maxTotal) * 0.5f;
            int cycles = 0;
            bool below = series[start].Total < mid;
            for (int i = start + 1; i < series.Count; i++)
            {
                bool nowBelow = series[i].Total < mid;
                if (below && !nowBelow) cycles++;
                below = nowBelow;
            }
            result.CycleCount = cycles;

            // Rabid entries: count Rabid-phase transitions across the whole run.
            int rabidEntries = 0;
            bool wasRabid = false;
            for (int i = 0; i < series.Count; i++)
            {
                bool isRabid = series[i].Phase == CellPhase.Rabid;
                if (isRabid && !wasRabid) rabidEntries++;
                wasRabid = isRabid;
            }
            result.RabidEntries = rabidEntries;

            // Plateau: steady-window swing is < 10% of the max.
            float swing = maxTotal > 0 ? (float)(maxTotal - minTotal) / maxTotal : 0f;
            result.IsPlateau = swing < 0.10f;

            var last = series[series.Count - 1];
            if (result.IsPlateau)
            {
                if (maxTotal < thresholds.QuietEnter)
                    result.Verdict = $"COLLAPSE — total pinned at {maxTotal} (< Quiet enter {thresholds.QuietEnter}), fauna ate everything";
                else if (last.Phase == CellPhase.Rabid)
                    result.Verdict = $"PLATEAU at RABID — total pinned ~{minTotal}-{maxTotal}, phase stuck at Rabid, peak outer-shell prisms {peakOuter}";
                else
                    result.Verdict = $"PLATEAU — total pinned ~{minTotal}-{maxTotal} (swing {swing * 100f:F0}%), phase {last.Phase}";
            }
            else
            {
                result.Verdict = $"OSCILLATING — total swings {minTotal}-{maxTotal} (~{swing * 100f:F0}%), " +
                                 $"{cycles} cycles in steady window, {rabidEntries} Rabid dips, peak outer-shell {peakOuter}";
            }
        }

        void AppendRun(StringBuilder sb, string title, RunResult r, CellPhaseThresholds thresholds)
        {
            sb.AppendLine($"--- {title} ---");
            sb.AppendLine(string.Format("{0,8} {1,8} {2,7} {3,7} {4,7} {5,9} {6,-9} {7,-7} {8,6} {9,-6}",
                "t(s)", "total", "jade", "ruby", "gold", "outerSh", "phase", "aggr", "fauna", "dom"));
            foreach (var rec in r.Series)
            {
                sb.AppendLine(string.Format("{0,8:F0} {1,8} {2,7} {3,7} {4,7} {5,9} {6,-9} {7,-7} {8,6} {9,-6}",
                    rec.Time, rec.Total, rec.Jade, rec.Ruby, rec.Gold, rec.OuterShellPrisms,
                    rec.Phase, rec.Aggression, rec.FaunaCount, rec.Dominant));
            }
            sb.AppendLine();
            sb.AppendLine($"  steady-state total: {r.MinTotalSteady:F0}-{r.MaxTotalSteady:F0}   " +
                          $"cycles: {r.CycleCount}   Rabid dips: {r.RabidEntries}");
            sb.AppendLine($"  outer-shell prisms (steady): {r.OuterShellMinSteady}-{r.OuterShellMaxSteady}, peak {r.PeakOuterShellPrisms}   " +
                          $"=> outer-shell mass {(r.OuterShellBounded ? "BOUNDED (fauna reach it)" : "UNBOUNDED (never consumed — grid is blind to it)")}");
            sb.AppendLine();
        }

        // ==================================================================
        //  Helpers
        // ==================================================================

        CellPhaseThresholds ScaledThresholds()
        {
            var d = CellPhaseThresholds.Default;
            float s = Mathf.Clamp(thresholdScale, 0.02f, 1f);
            int Sc(int v) => Mathf.Max(1, Mathf.RoundToInt(v * s));
            return new CellPhaseThresholds
            {
                QuietEnter = Sc(d.QuietEnter), QuietExit = Sc(d.QuietExit),
                SettledEnter = Sc(d.SettledEnter), SettledExit = Sc(d.SettledExit),
                RestlessEnter = Sc(d.RestlessEnter), RestlessExit = Sc(d.RestlessExit),
                FrozenEnter = Sc(d.FrozenEnter), FrozenExit = Sc(d.FrozenExit),
                RabidEnter = Sc(d.RabidEnter), RabidExit = Sc(d.RabidExit),
            };
        }

        static Vector3 RandomInBall(System.Random rng, float radius)
        {
            // Rejection-free: direction on sphere × cube-root-distributed radius.
            Vector3 dir = RandomOnSphere(rng);
            float r = radius * Mathf.Pow((float)rng.NextDouble(), 1f / 3f);
            return dir * r;
        }

        static Vector3 RandomInShell(System.Random rng, float inner, float outer)
        {
            Vector3 dir = RandomOnSphere(rng);
            // Uniform-in-volume within the shell.
            float u = (float)rng.NextDouble();
            float r = Mathf.Pow(inner * inner * inner + u * (outer * outer * outer - inner * inner * inner), 1f / 3f);
            return dir * r;
        }

        static Vector3 RandomOnSphere(System.Random rng)
        {
            // Marsaglia.
            float x1, x2, s;
            do
            {
                x1 = (float)(rng.NextDouble() * 2 - 1);
                x2 = (float)(rng.NextDouble() * 2 - 1);
                s = x1 * x1 + x2 * x2;
            } while (s >= 1f || s < 1e-6f);
            float f = 2f * Mathf.Sqrt(1f - s);
            return new Vector3(x1 * f, x2 * f, 1f - 2f * s);
        }

        static Vector3 GaussianVec(System.Random rng)
        {
            double u1 = 1.0 - rng.NextDouble();
            double u2 = 1.0 - rng.NextDouble();
            double mag = Math.Sqrt(-2.0 * Math.Log(u1));
            float x = (float)(mag * Math.Cos(2.0 * Math.PI * u2));
            float y = (float)(mag * Math.Sin(2.0 * Math.PI * u2));
            double u3 = 1.0 - rng.NextDouble();
            double u4 = 1.0 - rng.NextDouble();
            float z = (float)(Math.Sqrt(-2.0 * Math.Log(u3)) * Math.Cos(2.0 * Math.PI * u4));
            return new Vector3(x, y, z);
        }
    }
}
