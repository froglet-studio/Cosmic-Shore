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
    /// two-frequency oscillation the design wants - or a static plateau?"
    ///
    /// It models the production loop faithfully enough to be load-bearing:
    ///   - Flora at fixed positions spawn prisms of their own domain while the
    ///     cell phase is below Frenzy (FloraGrowingEnabled) - steady until frenzy.
    ///   - Cell phase is driven by total prism count through CellPhaseRules /
    ///     CellPhaseThresholds - the real production transition table.
    ///   - Fauna spawn for the *dominant* domain once the cell holds mass, seek the
    ///     density algorithm's anti-own-domain target (any-domain at Frenzy), and
    ///     continuously sweep an orbit sphere around it (re-rolling a sub-goal on
    ///     arrival - the sim's stand-in for boid separation spreading the swarm),
    ///     consuming opposing-domain prisms within consumeRadius along the way.
    ///
    /// The headline test: run the whole sim three times and compare -
    ///   OLD: the LITERAL shipped grid (fixed ±500m box). Outer-shell mass is
    ///        invisible to the algorithm, so no fauna is ever directed there.
    ///   MID: the c058663 fix (cell-sized grid at 17³ + smoothing + sub-voxel
    ///        interp). Sees the whole cell but its 141m voxels can't resolve
    ///        sub-cluster structure: fauna swarm one cluster, deplete its core,
    ///        then idle (the densest voxel never shifts because the cluster's
    ///        out-of-reach shell keeps the bin loaded).
    ///   NEW: ProductionV2 - the Phase-2 BlockDensityGrid pipeline (75m adaptive
    ///        voxels + smoothing + interp + voxel mean-shift). The answer tracks
    ///        remaining mass as fauna consume, so consumption sustains.
    /// The geometric benchmark proved MID is far more ACCURATE than OLD; this
    /// sim asks whether that accuracy translates to ecology oscillation, and
    /// whether the Phase-2 pipeline does better.
    ///
    /// KNOWN FAUNA-MODEL APPROXIMATIONS (vs production LightFauna):
    ///   - Swarm spread is modeled as an orbit-sphere sweep, not boid separation.
    ///   - Population is a flat cap, not production's base + 4-per-100-prisms
    ///     scaling (LightFaunaManagerDataSO.extraFaunaPerHundredPrisms).
    ///   - Sim fauna travel ~7× faster than production (250 vs 35 m/s).
    ///   These make the sim's absolute numbers unreliable; only the RELATIVE
    ///   comparison between the three runs (same fauna model) is load-bearing.
    ///   In-game observation in Menu_Main is the final ecology validation.
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
        [Tooltip("Flora placed within the production grid's ±500m box (the shipped grid CAN see these). " +
                 "Distributed across innerClusterCount cluster centers - uniform-random placement creates " +
                 "a flat density field that the algorithm has nothing to lock onto.")]
        [Min(0)] public int innerFloraCount = 60;
        [Tooltip("Flora placed in the 500m..(membrane-100m) outer shell (the shipped ±500m grid CANNOT see these). " +
                 "Distributed across outerClusterCount cluster centers.")]
        [Min(0)] public int outerFloraCount = 60;
        [Tooltip("How many inner cluster centers innerFloraCount flora are distributed across. " +
                 "Production SpawnProfile data places flora in discrete biome clumps; modeling that gives " +
                 "the density field real peaks instead of uniform noise.")]
        [Min(1)] public int innerClusterCount = 2;
        [Tooltip("How many outer-shell cluster centers outerFloraCount flora are distributed across.")]
        [Min(1)] public int outerClusterCount = 3;
        [Tooltip("Gaussian scatter radius (m) of flora positions around their cluster center.")]
        [Min(1f)] public float floraClusterScatterRadius = 120f;
        [Tooltip("Seconds between prism spawns per flora.")]
        [Min(0.1f)] public float floraGrowthIntervalSeconds = 1.5f;
        [Tooltip("Gaussian scatter radius (m) of spawned prisms around their flora.")]
        [Min(1f)] public float floraScatterRadius = 80f;

        [Header("Phase thresholds")]
        [Tooltip("CellPhaseThresholds.Default scaled by this. Default is tuned for thousands " +
                 "of prisms; 0.15 gives Restless~1200 / Frenzy~2250 for a fast sim.")]
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
        [Tooltip("Radius (m) of the orbit sphere each fauna sweeps around the density target. " +
                 "The fauna continuously re-rolls a sub-goal inside this sphere, so the swarm " +
                 "collectively covers a volume of roughly this + consumeRadius - the sim's " +
                 "stand-in for boid separation spreading the swarm. Default 120m so total reach " +
                 "(120 + 40 = 160m) matches the algorithm's 150m smoothing kernel - i.e. the " +
                 "swarm's effective consumption volume equals the volume the algorithm samples " +
                 "for density. Set lower (~60m) to model a tighter swarm; the cluster shell will " +
                 "become unreachable and consumption stalls.")]
        [Min(0f)] public float faunaGoalOrbitRadius = 120f;
        [Tooltip("Max simultaneous fauna.")]
        [Min(1)] public int faunaPopulationCap = 40;
        [Tooltip("Seconds between fauna spawns (for the dominant domain) once the cell holds mass.")]
        [Min(0.25f)] public float faunaSpawnIntervalSeconds = 2f;

        [Header("Output")]
        [Tooltip("Sample the time series for the report this often (simulated seconds).")]
        [Min(1f)] public float timeSeriesSampleEverySeconds = 10f;
        [TextArea(10, 48)] public string lastReport = "";

        // ==================================================================
        //  Public API
        // ==================================================================

        [ContextMenu("Run Temporal Sim (OLD vs MID vs NEW)")]
        public string RunComparison()
        {
            var sb = new StringBuilder(8192);
            string dateUtc = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture);

            sb.AppendLine("=== DensityPartitionTemporalSim v1.0 ===");
            sb.AppendLine($"Date: {dateUtc}   Seed: {seed}");
            sb.AppendLine($"Cell membrane radius: {cellMembraneRadius:F0}m   Sim: {simDurationSeconds:F0}s @ {tickIntervalSeconds:F2}s/tick");
            sb.AppendLine($"Flora: {innerFloraCount} inner (within ±500m) across {Math.Max(1, innerClusterCount)} clusters " +
                          $"+ {outerFloraCount} outer shell (>500m) across {Math.Max(1, outerClusterCount)} clusters, " +
                          $"σ={floraClusterScatterRadius:F0}m");
            var th = ScaledThresholds();
            sb.AppendLine($"Phase thresholds (Default × {thresholdScale:F2}): " +
                          $"Restless {th.RestlessEnter}/{th.RestlessExit}  Frenzy {th.FrenzyEnter}/{th.FrenzyExit}");
            float floraRate = (innerFloraCount + outerFloraCount) / Mathf.Max(0.01f, floraGrowthIntervalSeconds);
            sb.AppendLine($"Flora production capacity: {floraRate:F0} prisms/s   " +
                          $"Fauna consumption capacity: ~{faunaPopulationCap * faunaConsumePerTickInRange / Mathf.Max(0.01f, tickIntervalSeconds):F0} prisms/s (cap × rate)");
            sb.AppendLine();
            sb.AppendLine("Three-way comparison: OLD is the literal shipped fixed-±500m grid; MID is the");
            sb.AppendLine("c058663 fix (cell-sized 17³ + smoothing + sub-voxel interp); NEW is ProductionV2 -");
            sb.AppendLine("the Phase-2 BlockDensityGrid pipeline (75m adaptive voxels + smoothing + interp +");
            sb.AppendLine("voxel mean-shift). Geometric accuracy is necessary but not sufficient for the");
            sb.AppendLine("ecology to oscillate - the algorithm also has to direct fauna onto locations where");
            sb.AppendLine("consumption is sustainable as the mass distribution shifts. Coarse-voxel smoothed");
            sb.AppendLine("argmax is sticky (fauna deplete one cluster's core and idle); fine voxels + voxel");
            sb.AppendLine("mean-shift let the target track remaining mass, sustaining consumption.");
            sb.AppendLine();

            // --- Run A: the LITERAL shipped grid (fixed ±500m box) ---
            var optOld = DensityPartitionBenchmarkAlgorithms.ProductionFixedGrid17();
            var resultOld = RunOne(optOld);
            AppendRun(sb, "OLD - ProductionFixedGrid17 (fixed ±500m box, the shipped grid)", resultOld, th);

            // --- Run B: the cell-sized smoothed-interp grid (the c058663 fix as shipped) ---
            var optMid = DensityPartitionBenchmarkAlgorithms.GridSmoothedInterp17();
            var resultMid = RunOne(optMid);
            AppendRun(sb, "MID - GridSmoothedInterp17 (cell-sized + smoothing + sub-voxel interp, c058663)", resultMid, th);

            // --- Run C: ProductionV2 - the EXACT Phase-2 production pipeline ---
            // What BlockDensityGrid ships after the audit's §7.3 fix: adaptive
            // resolution targeting 75m voxels (33 points/axis at Blob-cell scale),
            // box smoothing at 150m, sub-voxel interp, then 5 iterations of
            // mean-shift over the RAW VOXEL HISTOGRAM (the production grid stores
            // only counts - no prism positions to refine against).
            //
            // Why this beats MID: with 141m voxels (MID), an entire flora cluster
            // fits in one voxel, so the argmax can't shift as fauna deplete the
            // cluster's reachable core - the out-of-reach shell keeps the bin
            // loaded and fauna idle. 75m voxels resolve sub-cluster structure, and
            // the voxel mean-shift walks the answer onto whatever mass actually
            // remains, so the target tracks consumption.
            var optNew = DensityPartitionBenchmarkAlgorithms.ProductionV2();
            var resultNew = RunOne(optNew);
            AppendRun(sb, "NEW - ProductionV2 (75m voxels + smoothing + interp + voxel mean-shift - Phase 2 ship)", resultNew, th);

            // --- Verdict ---
            sb.AppendLine("=== Verdict ===");
            sb.AppendLine($"  OLD: {resultOld.Verdict}");
            sb.AppendLine($"  MID: {resultMid.Verdict}");
            sb.AppendLine($"  NEW: {resultNew.Verdict}");
            sb.AppendLine();

            // What the three-way comparison can prove:
            //  OLD plateau + MID plateau + NEW oscillates → the grid-sizing fix
            //    is necessary BUT the algorithm also needs mean-shift centroid
            //    refinement. The bare smoothed argmax sticks on one cluster and
            //    fauna deplete its core in seconds, then sit idle until the next
            //    goal update (5s); meanshift pulls the answer outward to where
            //    the remaining prisms are, sustaining consumption.
            //  OLD plateau + MID oscillates → c058663 alone is sufficient (the
            //    earlier sim that showed MID locking was a flora-distribution
            //    artifact, now fixed by clustering).
            //  All three plateau → it's parameter-side, not algorithm-side.
            //    Likely: goal-update interval too long, fauna speed too low,
            //    or threshold scale needs nudging.
            bool oldOsc = !resultOld.IsPlateau;
            bool midOsc = !resultMid.IsPlateau;
            bool newOsc = !resultNew.IsPlateau;
            bool midBlind = !resultMid.OuterShellBounded;
            bool newBlind = !resultNew.OuterShellBounded;
            if (!midOsc && newOsc && !newBlind)
            {
                sb.AppendLine("  CONFIRMED: the c058663 grid-sizing fix is necessary BUT not sufficient - the");
                sb.AppendLine("  Phase 2 pipeline (ProductionV2: 75m adaptive voxels + voxel mean-shift) is what");
                sb.AppendLine("  sustains consumption. With 141m voxels (MID) one cluster fits per voxel, so the");
                sb.AppendLine("  densest voxel can't shift as fauna eat the core - the out-of-reach shell keeps");
                sb.AppendLine("  the bin loaded. 75m voxels resolve sub-cluster structure and the voxel");
                sb.AppendLine("  mean-shift walks the answer onto remaining mass as the core empties.");
                sb.AppendLine("  This is exactly what the Phase-2 BlockDensityGrid ships - see the audit §7.3.");
            }
            else if (midOsc && !midBlind)
            {
                sb.AppendLine("  CONFIRMED: c058663 alone produces oscillation with flora distributed in");
                sb.AppendLine("  clusters (matching production SpawnProfile geometry). The earlier 'MID");
                sb.AppendLine("  plateaus' result was a sim flora-distribution artifact.");
            }
            else if (!oldOsc && !midOsc && !newOsc)
            {
                sb.AppendLine("  ALL THREE PLATEAU - algorithm choice is not the load-bearing dimension here.");
                sb.AppendLine("  Tune parameters: shorten faunaGoalUpdateIntervalSeconds (fauna retarget more");
                sb.AppendLine("  often as they deplete a cluster), raise faunaPopulationCap, or lower");
                sb.AppendLine("  thresholdScale so the Frenzy entry/exit band is tighter relative to flora");
                sb.AppendLine("  production rate.");
            }
            else
            {
                sb.AppendLine("  Mixed result - inspect the per-run tables above. The robust discriminator is");
                sb.AppendLine("  whether outer-shell mass is BOUNDED (count rises and falls) vs UNBOUNDED");
                sb.AppendLine("  (count never decreases). A run that plateaus in Frenzy will look UNBOUNDED");
                sb.AppendLine("  because Frenzy freezes flora at the moment of entry; that's a phase-lock,");
                sb.AppendLine("  not a grid-blindness problem.");
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
        struct SimFauna
        {
            public Vector3 Pos;
            public Domains Domain;
            public Vector3 DensityTarget; // density-algorithm answer, refreshed on the goal cadence
            public Vector3 SubGoal;       // a point inside the orbit sphere around DensityTarget; re-rolled on arrival
            public float GoalClock;
        }

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

            // --- Place flora deterministically, around CLUSTER CENTERS ---
            // Uniform-random flora over a 1200m sphere produces a near-flat density
            // field - the algorithm has no clear peaks to lock onto and the swarm
            // wanders in low-contrast regions where consumption can't keep up with
            // growth. Production SpawnProfile data places flora in discrete biome
            // clumps; we model that with a small number of cluster centers.
            int innerClusters = Math.Max(1, innerClusterCount);
            int outerClusters = Math.Max(1, outerClusterCount);
            var innerCenters = new Vector3[innerClusters];
            for (int i = 0; i < innerClusters; i++)
                innerCenters[i] = RandomInBall(rng, 400f);
            var outerCenters = new Vector3[outerClusters];
            float outerInner = 520f;
            float outerOuter = Mathf.Max(outerInner + 40f, cellMembraneRadius - 200f);
            for (int i = 0; i < outerClusters; i++)
                outerCenters[i] = RandomInShell(rng, outerInner, outerOuter);

            var flora = new List<SimFlora>(innerFloraCount + outerFloraCount);
            Domains[] domainCycle = { Domains.Jade, Domains.Ruby, Domains.Gold };
            for (int i = 0; i < innerFloraCount; i++)
                flora.Add(new SimFlora
                {
                    Pos = innerCenters[i % innerClusters] + GaussianVec(rng) * floraClusterScatterRadius,
                    Domain = domainCycle[i % 3],
                    GrowthClock = (float)rng.NextDouble() * floraGrowthIntervalSeconds, // stagger
                });
            for (int i = 0; i < outerFloraCount; i++)
                flora.Add(new SimFlora
                {
                    Pos = outerCenters[i % outerClusters] + GaussianVec(rng) * floraClusterScatterRadius,
                    Domain = domainCycle[i % 3],
                    GrowthClock = (float)rng.NextDouble() * floraGrowthIntervalSeconds,
                });

            var prisms = new List<SimPrism>(4096);
            var fauna = new List<SimFauna>(faunaPopulationCap);
            var benchPrisms = new List<BenchmarkPrism>(4096); // reused per tick

            CellPhase phase = CellPhase.Calm;
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
                // The sim tracks nominal prisms (no per-prism scale), so its volume is
                // count × NominalPrismVolume - with derived thresholds this reproduces
                // the same ladder the old count-driven Compute walked.
                phase = CellPhaseRules.Compute(
                    total * CellPhaseThresholds.NominalPrismVolume, total, phase, in thresholds);
                CellAggressionLevel aggression = phase switch
                {
                    CellPhase.Restless => CellAggressionLevel.Level1,
                    CellPhase.Frenzy => CellAggressionLevel.Level2,
                    _ => CellAggressionLevel.Level0,
                };
                Domains dominant =
                    jade >= ruby && jade >= gold ? Domains.Jade :
                    ruby >= gold ? Domains.Ruby : Domains.Gold;

                // --- 2. Flora growth (gated: phase < Frenzy, matching FloraGrowingEnabled) ---
                bool floraGrowing = phase < CellPhase.Frenzy;
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

                // --- 3. Fauna spawn (gated: cell holds mass, for the dominant domain) ---
                bool faunaSpawning = total > 0;
                faunaSpawnClock += dt;
                if (faunaSpawning && faunaSpawnClock >= faunaSpawnIntervalSeconds && fauna.Count < faunaPopulationCap)
                {
                    faunaSpawnClock = 0f;
                    fauna.Add(new SimFauna
                    {
                        Pos = RandomInBall(rng, cellMembraneRadius * 0.5f),
                        Domain = dominant,
                        DensityTarget = Vector3.zero,
                        SubGoal = Vector3.zero,
                        GoalClock = 999f, // force an immediate density query
                    });
                }

                // --- 4. Fauna behaviour ---
                if (fauna.Count > 0)
                {
                    // Build the BenchmarkPrism snapshot once per tick - reused by every
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

                        // Re-query the density target on the goal cadence.
                        fa.GoalClock += dt;
                        if (fa.GoalClock >= faunaGoalUpdateIntervalSeconds)
                        {
                            fa.GoalClock = 0f;
                            // Level2 (Frenzy): any-domain densest. Else: anti-own-domain.
                            Domains? exclude = aggression == CellAggressionLevel.Level2 ? (Domains?)null : fa.Domain;
                            var r = DensityPartitionBenchmarkAlgorithms.Search(
                                benchPrisms, exclude, cellMembraneRadius, densityOpt);
                            fa.DensityTarget = r.Empty ? Vector3.zero : r.Location;
                            fa.SubGoal = fa.DensityTarget + RandomInBall(rng, faunaGoalOrbitRadius);
                        }

                        // Move toward the sub-goal. On arrival, re-roll a new sub-goal
                        // inside the orbit sphere around the density target - so the
                        // fauna continuously SWEEPS the goal region instead of parking
                        // at one offset point. This is the sim's stand-in for boid
                        // separation spreading the swarm across a volume; without it a
                        // fauna parks goalOrbitRadius away from the mass and its
                        // consumeRadius never reaches the prisms - consumption stalls
                        // at ~0 and the cell shows a false plateau.
                        Vector3 toGoal = fa.SubGoal - fa.Pos;
                        float dist = toGoal.magnitude;
                        float step = faunaSpeed * dt;
                        if (dist <= step || dist < 0.001f)
                        {
                            fa.Pos = fa.SubGoal;
                            fa.SubGoal = fa.DensityTarget + RandomInBall(rng, faunaGoalOrbitRadius);
                        }
                        else
                        {
                            fa.Pos += toGoal * (step / dist);
                        }

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

            // Frenzy entries: count Frenzy-phase transitions across the whole run.
            int rabidEntries = 0;
            bool wasRabid = false;
            for (int i = 0; i < series.Count; i++)
            {
                bool isRabid = series[i].Phase == CellPhase.Frenzy;
                if (isRabid && !wasRabid) rabidEntries++;
                wasRabid = isRabid;
            }
            result.RabidEntries = rabidEntries;

            // Plateau threshold: 5%. The Frenzy entry/exit band is naturally ~5%
            // wide (1500/1425 = 5% of 1500), so a healthy primary cycle that
            // overshoots the band edges should easily exceed 5% swing. Anything
            // tighter than that is either a true plateau or a faint oscillation
            // hugging the band - both indistinguishable from "no real cycle".
            float swing = maxTotal > 0 ? (float)(maxTotal - minTotal) / maxTotal : 0f;
            result.IsPlateau = swing < 0.05f;

            var last = series[series.Count - 1];
            if (result.IsPlateau)
            {
                if (maxTotal < thresholds.RestlessEnter)
                    result.Verdict = $"COLLAPSE - total pinned at {maxTotal} (< Restless enter {thresholds.RestlessEnter}), fauna ate everything";
                else if (last.Phase == CellPhase.Frenzy)
                    result.Verdict = $"PLATEAU at FRENZY - total pinned ~{minTotal}-{maxTotal}, phase stuck at Frenzy, peak outer-shell prisms {peakOuter}";
                else
                    result.Verdict = $"PLATEAU - total pinned ~{minTotal}-{maxTotal} (swing {swing * 100f:F0}%), phase {last.Phase}";
            }
            else
            {
                result.Verdict = $"OSCILLATING - total swings {minTotal}-{maxTotal} (~{swing * 100f:F0}%), " +
                                 $"{cycles} cycles in steady window, {rabidEntries} Frenzy dips, peak outer-shell {peakOuter}";
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
                          $"cycles: {r.CycleCount}   Frenzy dips: {r.RabidEntries}");
            sb.AppendLine($"  outer-shell prisms (steady): {r.OuterShellMinSteady}-{r.OuterShellMaxSteady}, peak {r.PeakOuterShellPrisms}   " +
                          $"=> outer-shell mass {(r.OuterShellBounded ? "BOUNDED (fauna reach it)" : "UNBOUNDED (never consumed - grid is blind to it)")}");
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
                RestlessEnter = Sc(d.RestlessEnter), RestlessExit = Sc(d.RestlessExit),
                FrenzyEnter = Sc(d.FrenzyEnter), FrenzyExit = Sc(d.FrenzyExit),
            }.WithDerivedVolumeScale();
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
