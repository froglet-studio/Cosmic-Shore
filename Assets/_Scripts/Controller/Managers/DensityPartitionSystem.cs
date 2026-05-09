using System.Diagnostics;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// One pre-computed densest-region answer for a single anti-domain bucket
    /// (i.e. the densest region of all prisms NOT belonging to the friendly
    /// domain that's asking). Held as a struct so consumers can pass it around
    /// by value and so a future network-sync layer can serialize it directly.
    /// </summary>
    public struct PartitionSolution : System.IEquatable<PartitionSolution>
    {
        public Vector3 Position;
        public float Density;
        public int CellId;

        // Stride at the time of compute, so consumers can re-derive the bucket
        // bounds for AOE / nav purposes without round-tripping to the cell.
        public float Stride;

        // Friendly domain this solution is "anti" to (the team that's asking).
        public int AntiOfDomain;

        // Monotonic counter so consumers can tell whether a poll returned a
        // new answer or the same one they saw last frame.
        public uint Version;

        public bool HasResult => Density > 0f;

        public Domains AntiOfDomainEnum => (Domains)AntiOfDomain;

        public bool Equals(PartitionSolution other) =>
            Position.Equals(other.Position)
            && Density == other.Density
            && CellId == other.CellId
            && Stride == other.Stride
            && AntiOfDomain == other.AntiOfDomain
            && Version == other.Version;

        public override bool Equals(object obj) => obj is PartitionSolution o && Equals(o);

        public override int GetHashCode() =>
            Position.GetHashCode() ^ Density.GetHashCode() ^ CellId ^ AntiOfDomain ^ (int)Version;
    }

    /// <summary>
    /// Periodic aggregator over per-Cell density grids. Computes three
    /// "anti-domain" solutions every <see cref="recomputeIntervalSeconds"/>
    /// and caches them so any reader (AI, fauna, vessel abilities) can poll
    /// with no per-reader recompute cost:
    ///
    /// <list type="bullet">
    ///   <item>Anti-Jade = densest region of {Ruby ∪ Gold} prisms.</item>
    ///   <item>Anti-Ruby = densest region of {Jade ∪ Gold} prisms.</item>
    ///   <item>Anti-Gold = densest region of {Jade ∪ Ruby} prisms.</item>
    /// </list>
    ///
    /// The buckets aren't recomputed here — <see cref="Cell"/> already keeps a
    /// per-team <see cref="BlockCountDensityGrid"/> where the per-domain bucket
    /// stores every block NOT belonging to that domain (the existing
    /// "anti-domain" semantic). This system only picks the strongest centroid
    /// across all active cells, stamps a version, and caches.
    ///
    /// <para>
    /// Bootstrap: <see cref="EnsureExists"/> spawns the system on demand.
    /// <see cref="Cell.OnEnable"/> calls it so any scene with cells gets
    /// the system for free; the editor toolbox's "Density" tab also calls
    /// it so a Menu_Main session without cells still has the diagnostics
    /// available the moment you open the tab. Scenes that have neither
    /// cells nor toolbox access (e.g. headless tests) need to call
    /// <see cref="EnsureExists"/> explicitly.
    /// </para>
    ///
    /// <para>
    /// Hybrid event entry: <see cref="RequestImmediateRecompute"/> is the
    /// cooldowned event path. Many simultaneous callers within
    /// <see cref="eventCooldownSeconds"/> coalesce to a single recompute,
    /// preventing thrash on volatile prism volumes.
    /// </para>
    ///
    /// <para>
    /// Network sync was scoped out of the initial system: each client computes
    /// locally over its own (Netcode-replicated) Cells. A future
    /// <c>DensityPartitionNetworkSync</c> sibling can be added if game scenes
    /// require server-authoritative answers.
    /// </para>
    /// </summary>
    public class DensityPartitionSystem : MonoBehaviour
    {
        [Header("Recompute cadence")]
        [Tooltip("Seconds between server-side recomputes. Default 0.5s (2 Hz) " +
                 "comfortably covers fauna seeking and AI target choice without " +
                 "burning CPU on every frame.")]
        [SerializeField] float recomputeIntervalSeconds = 0.5f;

        [Tooltip("Minimum seconds between RequestImmediateRecompute() responses. " +
                 "Many event-driven callers within the cooldown coalesce to a " +
                 "single recompute, preventing thrash on volatile prism volumes.")]
        [SerializeField] float eventCooldownSeconds = 0.25f;

        [Header("Search")]
        [Tooltip("Half-extent (in grid cells) of the kernel-smoothed peak search. " +
                 "0 = pick the single densest cell (raw max, brittle to single-prism " +
                 "noise); 1 = pick the densest 3x3x3 region (default — smooths over " +
                 "isolated tight clusters so the strongest *region* wins, not the " +
                 "strongest single cell); 2 = densest 5x5x5; etc.")]
        [SerializeField, Range(0, 4)] int searchKernelRadius = 1;

        [Header("Diagnostics")]
        [Tooltip("If true, log each recompute's millisecond cost.")]
        [SerializeField] bool verboseProfiling;

        // ── Cached solutions (read by anyone via the public API) ────────────
        PartitionSolution _antiJade;
        PartitionSolution _antiRuby;
        PartitionSolution _antiGold;
        // All-domain (countGrids[Blue]) peak — diagnostic only, surfaces in
        // the toolbox so the user can see whether the search itself agrees
        // with the visible heatmap (it should). Disagreement between this
        // and the per-team antis is a bucket-staleness symptom in Cell.cs.
        PartitionSolution _allDomain;

        // ── Tick state ──────────────────────────────────────────────────────
        float _nextRecomputeAt;
        float _earliestEventRecomputeAt;
        uint _version;

        // ── Diagnostics ─────────────────────────────────────────────────────
        public uint Version => _version;
        public int LastRecomputeCellsScanned { get; private set; }
        public int LastRecomputeCellsWithPrisms { get; private set; }
        public float LastRecomputeMillis { get; private set; }
        public float RecomputeIntervalSeconds => recomputeIntervalSeconds;
        public float EventCooldownSeconds => eventCooldownSeconds;
        public float NextRecomputeIn => Mathf.Max(0f, _nextRecomputeAt - Time.time);

        // ── Singleton-ish accessor + auto-bootstrap ─────────────────────────
        // The system is per-scene; the static field is cleared and re-seeded
        // on each scene load.
        static DensityPartitionSystem _active;
        public static DensityPartitionSystem Active
        {
            get
            {
                if (_active != null) return _active;
                _active = FindFirstObjectByType<DensityPartitionSystem>(FindObjectsInactive.Exclude);
                return _active;
            }
        }

        /// <summary>
        /// Returns the per-scene system, creating it on a hidden GameObject if
        /// none exists. Safe to call from any scene at any time.
        /// </summary>
        public static DensityPartitionSystem EnsureExists()
        {
            var existing = Active;
            if (existing != null) return existing;

            var go = new GameObject($"[Auto] {nameof(DensityPartitionSystem)}");
            go.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            var system = go.AddComponent<DensityPartitionSystem>();
            return system;
        }

        void Awake()
        {
            if (_active != null && _active != this)
            {
                CSDebug.LogWarning($"[DensityPartitionSystem] Multiple instances detected; " +
                                   $"keeping the original on '{_active.gameObject.name}' and " +
                                   $"ignoring '{gameObject.name}'.");
                enabled = false;
                return;
            }
            _active = this;
        }

        void OnDestroy()
        {
            if (_active == this) _active = null;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Read API — call from anywhere
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns the densest centroid of prisms NOT in <paramref name="friendlyDomain"/>.
        /// I.e. for a Jade-team consumer, returns the densest cluster of
        /// Ruby+Gold prisms. <see cref="PartitionSolution.HasResult"/> is false
        /// when no enemy prisms are tracked anywhere.
        /// </summary>
        public PartitionSolution GetAntiDomainSolution(Domains friendlyDomain) =>
            friendlyDomain switch
            {
                Domains.Jade => _antiJade,
                Domains.Ruby => _antiRuby,
                Domains.Gold => _antiGold,
                _ => default,
            };

        public bool TryGetAntiDomainSolution(Domains friendlyDomain, out PartitionSolution solution)
        {
            solution = GetAntiDomainSolution(friendlyDomain);
            return solution.HasResult;
        }

        /// <summary>
        /// Diagnostic-only: densest region of the cell's all-domain bucket
        /// (countGrids[Domains.Blue]). Useful as a sanity check against the
        /// heatmap — if the all-domain marker tracks the bright cubes but
        /// the per-team antis don't, that's a bucket-staleness issue in
        /// Cell's Add/Remove path, not in this search.
        /// </summary>
        public PartitionSolution GetAllDomainSolution() => _allDomain;

        /// <summary>
        /// Convenience for AI / fauna / vessel abilities that just want the
        /// current anti-domain answer without null-checking <see cref="Active"/>.
        /// Returns a default <see cref="PartitionSolution"/> (HasResult == false)
        /// if no system is in the scene yet.
        /// </summary>
        public static PartitionSolution GetSolutionForDomain(Domains friendlyDomain)
        {
            var system = Active;
            return system == null ? default : system.GetAntiDomainSolution(friendlyDomain);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Tick
        // ─────────────────────────────────────────────────────────────────────

        void Update()
        {
            if (Time.time < _nextRecomputeAt) return;

            _nextRecomputeAt = Time.time + recomputeIntervalSeconds;
            Recompute();
        }

        /// <summary>
        /// Cooldowned event entry — for callers that want fresher answers
        /// after a known mass change (e.g. a big explosion or a big spawn).
        /// Returns true if a recompute actually ran. The cooldown coalesces
        /// many simultaneous callers down to a single recompute.
        /// </summary>
        public bool RequestImmediateRecompute()
        {
            if (Time.time < _earliestEventRecomputeAt) return false;

            _earliestEventRecomputeAt = Time.time + eventCooldownSeconds;
            // Slide the periodic deadline too so we don't double-recompute
            // milliseconds after an event.
            _nextRecomputeAt = Time.time + recomputeIntervalSeconds;
            Recompute();
            return true;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Compute
        // ─────────────────────────────────────────────────────────────────────

        void Recompute()
        {
            // Always measure so the toolbox shows live cost; verboseProfiling
            // only gates the log line.
            var sw = Stopwatch.StartNew();

            _version++;

            var bestJade = new PartitionSolution { AntiOfDomain = (int)Domains.Jade, Version = _version };
            var bestRuby = new PartitionSolution { AntiOfDomain = (int)Domains.Ruby, Version = _version };
            var bestGold = new PartitionSolution { AntiOfDomain = (int)Domains.Gold, Version = _version };
            var bestAll  = new PartitionSolution { AntiOfDomain = (int)Domains.Blue, Version = _version };

            int radius = Mathf.Max(0, searchKernelRadius);
            int scanned = 0;
            int withPrisms = 0;
            var cells = Cell.ActiveCells;
            foreach (var cell in cells)
            {
                if (cell == null) continue;
                if (cell.countGrids == null || cell.countGrids.Count == 0) continue;
                scanned++;

                bool any = false;
                any |= EvaluateCellGrid(cell, Domains.Jade, radius, ref bestJade);
                any |= EvaluateCellGrid(cell, Domains.Ruby, radius, ref bestRuby);
                any |= EvaluateCellGrid(cell, Domains.Gold, radius, ref bestGold);
                EvaluateCellGrid(cell, Domains.Blue, radius, ref bestAll); // diagnostic
                if (any) withPrisms++;
            }

            _antiJade = bestJade;
            _antiRuby = bestRuby;
            _antiGold = bestGold;
            _allDomain = bestAll;

            sw.Stop();
            LastRecomputeMillis = (float)sw.Elapsed.TotalMilliseconds;
            LastRecomputeCellsScanned = scanned;
            LastRecomputeCellsWithPrisms = withPrisms;

            if (verboseProfiling)
            {
                CSDebug.Log($"[DensityPartitionSystem] v{_version} scanned {scanned} cells " +
                            $"({withPrisms} with prisms) in {LastRecomputeMillis:F2}ms — " +
                            $"antiJ={bestJade.Density:F0} antiR={bestRuby.Density:F0} " +
                            $"antiG={bestGold.Density:F0} all={bestAll.Density:F0}");
            }
        }

        /// <summary>
        /// Kernel-smoothed peak search: scans the cell's grid for the position
        /// where the sum over a (2r+1)³ neighborhood is largest. Smooths out
        /// single-prism noise so a tight 2-prism cluster doesn't outvote a
        /// wider 10-prism spread, which was making the original FindDensestRegion
        /// (single-cell max) jitter to "arbitrary" peaks in long-running ecosystems.
        /// Returns true when this cell contributed any mass — used to count
        /// "cells with prisms" for diagnostics.
        /// </summary>
        static bool EvaluateCellGrid(Cell cell, Domains bucketDomain, int radius,
                                     ref PartitionSolution best)
        {
            // Cell.countGrids[bucketDomain] semantics:
            //   Jade/Ruby/Gold = every block NOT in that team (anti-team bucket)
            //   Blue           = every block regardless of team (all-domain wildcard)
            if (!cell.countGrids.TryGetValue(bucketDomain, out var grid) || grid == null)
                return false;
            if (grid.values == null) return false;

            int n = grid.values.GetLength(0);
            if (n <= 0) return false;

            int bestSum = 0;
            int bx = 0, by = 0, bz = 0;

            for (int x = 0; x < n; x++)
            {
                int xMin = x - radius; if (xMin < 0) xMin = 0;
                int xMax = x + radius; if (xMax >= n) xMax = n - 1;

                for (int y = 0; y < n; y++)
                {
                    int yMin = y - radius; if (yMin < 0) yMin = 0;
                    int yMax = y + radius; if (yMax >= n) yMax = n - 1;

                    for (int z = 0; z < n; z++)
                    {
                        int zMin = z - radius; if (zMin < 0) zMin = 0;
                        int zMax = z + radius; if (zMax >= n) zMax = n - 1;

                        int sum = 0;
                        for (int xi = xMin; xi <= xMax; xi++)
                            for (int yi = yMin; yi <= yMax; yi++)
                                for (int zi = zMin; zi <= zMax; zi++)
                                    sum += grid.values[xi, yi, zi];

                        if (sum > bestSum)
                        {
                            bestSum = sum;
                            bx = x; by = y; bz = z;
                        }
                    }
                }
            }

            if (bestSum <= 0) return false;
            if (bestSum <= best.Density) return true;

            best.Position = grid.MapGridIndicesToCoordinates(new Vector3Int(bx, by, bz));
            best.Density = bestSum;
            best.CellId = cell.ID;
            best.Stride = grid.Stride;
            return true;
        }
    }
}
