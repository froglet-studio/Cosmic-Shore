using System.Diagnostics;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;
using UnityEngine.SceneManagement;

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
    /// Auto-bootstrap: <see cref="EnsureExists"/> is called from a
    /// <see cref="RuntimeInitializeOnLoadMethodAttribute"/> hook so every
    /// loaded scene gets the system for free — no manual scene placement
    /// required. The lifetime is per-scene; <see cref="ResetForSceneLoad"/>
    /// re-spawns it after each load so cell registries from the previous
    /// scene don't leak forward.
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

        [Header("Diagnostics")]
        [Tooltip("If true, log each recompute's millisecond cost.")]
        [SerializeField] bool verboseProfiling;

        // ── Cached solutions (read by anyone via the public API) ────────────
        PartitionSolution _antiJade;
        PartitionSolution _antiRuby;
        PartitionSolution _antiGold;

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

        // ── Auto-bootstrap on every scene load ──────────────────────────────

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void HookSceneLoad()
        {
            // Wire once per game session. SubsystemRegistration runs before
            // any scene loads, so we get to subscribe before Bootstrap.
            SceneManager.sceneLoaded -= OnSceneLoadedStatic;
            SceneManager.sceneLoaded += OnSceneLoadedStatic;
        }

        static void OnSceneLoadedStatic(Scene scene, LoadSceneMode mode)
        {
            // Drop the stale reference from the previous scene; Active getter
            // will re-find or EnsureExists will create a fresh instance.
            _active = null;
            EnsureExists();
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

            int scanned = 0;
            int withPrisms = 0;
            var cells = Cell.ActiveCells;
            foreach (var cell in cells)
            {
                if (cell == null) continue;
                if (cell.countGrids == null || cell.countGrids.Count == 0) continue;
                scanned++;

                bool any = false;
                any |= EvaluateCellAntiDomain(cell, Domains.Jade, ref bestJade);
                any |= EvaluateCellAntiDomain(cell, Domains.Ruby, ref bestRuby);
                any |= EvaluateCellAntiDomain(cell, Domains.Gold, ref bestGold);
                if (any) withPrisms++;
            }

            _antiJade = bestJade;
            _antiRuby = bestRuby;
            _antiGold = bestGold;

            sw.Stop();
            LastRecomputeMillis = (float)sw.Elapsed.TotalMilliseconds;
            LastRecomputeCellsScanned = scanned;
            LastRecomputeCellsWithPrisms = withPrisms;

            if (verboseProfiling)
            {
                CSDebug.Log($"[DensityPartitionSystem] v{_version} scanned {scanned} cells " +
                            $"({withPrisms} with prisms) in {LastRecomputeMillis:F2}ms — " +
                            $"antiJ={bestJade.Density:F0} antiR={bestRuby.Density:F0} " +
                            $"antiG={bestGold.Density:F0}");
            }
        }

        /// <summary>
        /// Returns true when this cell contributed any prism to <paramref name="friendlyDomain"/>'s
        /// anti-domain bucket — used to count "cells with prisms" for diagnostics.
        /// </summary>
        static bool EvaluateCellAntiDomain(Cell cell, Domains friendlyDomain, ref PartitionSolution best)
        {
            // Cell.countGrids[friendly] holds every block NOT in `friendly`
            // (see Cell.AddBlock — friendly is the one team it skips). So the
            // densest region of that grid is the anti-friendly answer.
            if (!cell.countGrids.TryGetValue(friendlyDomain, out var grid) || grid == null)
                return false;

            var pos = grid.FindDensestRegion();
            int density = grid.GetDensityAtPosition(pos);
            if (density <= 0) return false;
            if (density <= best.Density) return true;

            best.Position = pos;
            best.Density = density;
            best.CellId = cell.ID;
            best.Stride = grid.Stride;
            return true;
        }
    }
}
