using System.Collections.Generic;
using CosmicShore.Utility;
using Unity.Profiling;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Proximity collider-LOD over the prism population - the collider half of the
    /// ecosystem performance contract (Docs/ECOSYSTEM_MASTERPLAN.md §4). Prism
    /// BoxColliders only matter near the things that physically TOUCH prisms:
    /// vessels (hull, skimmer, crystal-shield triggers) and projectiles in flight.
    /// Everything else stopped needing them when the senses moved onto
    /// <see cref="PrismSpatialIndex"/> (fauna scans, AOE damage, growth occupancy -
    /// see Docs/SPATIAL_INDEX.md), so colliders far from every focus are culled and
    /// restored as foci move. This is what lets cells hold thousands of prisms
    /// (large flyable flora lattices) while the active-collider count stays bounded
    /// by the LOD radius instead of the population.
    ///
    /// Focus points self-register: <see cref="VesselStatus"/> (every vessel, human,
    /// AI, and menu autopilot alike) and <see cref="Projectile"/> (per flight, so
    /// shots fired at distant structures wake the colliders they are about to hit)
    /// call <see cref="RegisterFocus"/>/<see cref="UnregisterFocus"/> from their
    /// enable/disable lifecycle.
    ///
    /// Classification runs in ONE Burst pass per tick
    /// (<see cref="PrismSpatialIndex.RunLodClassification"/>) that emits only
    /// near/far TRANSITIONS via the per-slot LodNear flag bit - the managed cost
    /// here is O(changed colliders), bounded further by a per-frame toggle budget,
    /// never O(population). (The previous managed sliced scan paid ~5.5 ms per
    /// slice frame at 25k prisms.)
    ///
    /// Safety properties:
    ///   - With NO focus registered (tool scenes, teardown) the manager restores
    ///     anything it culled and otherwise leaves collider state to the Prism
    ///     lifecycle - it never blanket-disables.
    ///   - Mound-layer blocks (Boid.NewBlock) never register with the index, so
    ///     their colliders - the only way mound mate-finding sees them - are never
    ///     touched.
    ///   - Cull/restore goes through <see cref="Prism.SetColliderCulledByLod"/>,
    ///     which snapshots and restores the pre-cull collider state so destruction,
    ///     the spawn window, and shield collider-swaps are never fought.
    ///
    /// Lives on the same auto-created GameObject as <see cref="PrismSpatialIndex"/>;
    /// place one in a scene to override the serialized knobs.
    /// </summary>
    [DisallowMultipleComponent]
    public class PrismColliderLodManager : Singleton<PrismColliderLodManager>
    {
        [Header("LOD")]
        [Tooltip("Master switch. OFF restores every culled collider and goes idle - the in-editor kill switch if any collider consumer was missed.")]
        [SerializeField] bool lodEnabled = true;

        [Tooltip("Prism colliders stay enabled within this distance of any focus (vessel / projectile). " +
                 "Must comfortably exceed focus speed × tick so fast vessels never outrun their collider bubble.")]
        [Min(50f)] [SerializeField] float lodRadiusMeters = 200f;

        [Tooltip("Hysteresis: a NEAR prism only becomes far again beyond lodRadiusMeters × this " +
                 "(prisms in the annulus keep their state). >1 stops the bubble boundary of a MOVING " +
                 "focus from flapping prisms near/far - and re-toggling their colliders - every tick. " +
                 "1 = no hysteresis (the pre-hysteresis behavior).")]
        [Range(1f, 2f)] [SerializeField] float lodExitRadiusMultiplier = 1.15f;

        [Tooltip("Seconds between LOD classification passes. At 0.25s a 100 u/s vessel moves 25m per pass - well inside the radius margin.")]
        [Min(0.05f)] [SerializeField] float tickIntervalSeconds = 0.25f;

        [Tooltip("Collider CULLS applied per frame from the deferred cull queue. Restores are " +
                 "never budgeted (turning a collider back on near a focus is the safety-critical " +
                 "direction, and restores on already-enabled prisms are cheap early-outs); culls " +
                 "are the bulk direction on a reconcile (population minus near set) and never " +
                 "urgent, so they drain at this rate without ever blocking classification.")]
        [Min(64)] [SerializeField] int maxColliderTogglesPerFrame = 512;

        // Focus registry: vessels + in-flight projectiles. Main-thread only, tiny.
        static readonly List<Transform> s_foci = new(16);

        /// <summary>Active colliders after the last pass (telemetry - EcosystemPerfProbe).</summary>
        public static int LastNearCount { get; private set; }

        /// <summary>Live prisms seen in the last pass (telemetry - EcosystemPerfProbe).</summary>
        public static int LastLiveCount { get; private set; }

        public static void RegisterFocus(Transform focus)
        {
            if (focus && !s_foci.Contains(focus)) s_foci.Add(focus);
        }

        public static void UnregisterFocus(Transform focus)
        {
            s_foci.Remove(focus);
        }

        float _nextTickAt;
        bool _culledAnything;
        // One whole-population reconciliation is needed whenever LOD (re)gains an
        // opinion: first pass, foci returning after an idle restore-all, or
        // re-enable. Steady-state passes emit only near/far transitions (the
        // LodNear flag bit in the spatial index is the memory).
        bool _reconcileNextSweep = true;
        int _nearCount;

        readonly List<Vector3> _fociSnapshot = new(16);
        readonly List<Prism> _becameNear = new(1024);
        readonly List<Prism> _becameFar = new(4096);
        // Deferred culls: drained at maxColliderTogglesPerFrame. The set is the
        // cancellation surface - a prism that re-enters the bubble before its cull
        // applies is removed from the set by the restore pass, and the queue entry
        // becomes a no-op at drain. Classification is never blocked by this queue.
        readonly List<Prism> _cullQueue = new(4096);
        readonly HashSet<Prism> _cullPending = new();
        int _cullCursor;
        readonly List<Prism> _liveScratch = new(4096);

        // Attribution split (Docs/PERFORMANCE_OPTIMIZATION.md): Sweep is the 0.25s
        // tick (Burst classify + restore/enqueue transitions); Drain is the
        // every-frame budgeted cull application.
        static readonly ProfilerMarker s_sweepMarker = new("LOD.Sweep");
        static readonly ProfilerMarker s_drainMarker = new("LOD.Drain");

        /// <summary>
        /// Called when a prism's collider comes online outside the pass cadence
        /// (spawn-window end, trail restore). The transition-based pass only
        /// touches prisms whose near/far state CHANGES, so without this a prism
        /// born far from every focus would keep its collider until it crossed a
        /// bubble boundary. O(foci) - no population walk.
        /// </summary>
        public static void NotifyPrismActivated(Prism prism)
        {
            var inst = Instance;
            if (inst == null || !inst.lodEnabled || prism == null) return;
            if (s_foci.Count == 0) return;

            float r2 = inst.lodRadiusMeters * inst.lodRadiusMeters;
            Vector3 p = prism.transform.position;
            for (int i = 0; i < s_foci.Count; i++)
            {
                var focus = s_foci[i];
                if (focus && (focus.position - p).sqrMagnitude <= r2)
                    return; // near a focus - collider stays as the lifecycle set it
            }

            prism.SetColliderCulledByLod(true);
            inst._culledAnything = true;
        }

        public static PrismColliderLodManager EnsureInstance()
        {
            if (Instance != null) return Instance;
            // Ride the spatial index's GameObject - one bootstrap path, present in
            // exactly the scenes that have prisms.
            var host = PrismSpatialIndex.EnsureInstance();
            if (host == null) return null;
            return host.gameObject.AddComponent<PrismColliderLodManager>();
        }

        void OnEnable()
        {
            // De-phase from the other 0.25s whole-population pass
            // (Cell.VolumeSum, armed by the first volume reader): a half-interval
            // offset keeps the two tick spikes off the same frame.
            _nextTickAt = Time.time + 0.5f * Mathf.Max(0.05f, tickIntervalSeconds);
        }

        void Update()
        {
            if (!lodEnabled)
            {
                // Kill switch mid-drain: drop pending culls; the next tick runs the
                // restore-all path and re-arms a reconcile.
                if (_cullPending.Count > 0) ClearCullQueue();
            }
            else
            {
                using (s_drainMarker.Auto())
                {
                    DrainCullQueue();
                }
            }

            if (Time.time < _nextTickAt) return;
            // Phase-preserving re-arm: advancing from the DUE time (not from now)
            // keeps the OnEnable half-interval offset from Cell.VolumeSum forever.
            // Re-arming from Time.time re-syncs the two ticks onto the same frame
            // after any hitch longer than the offset (observed in the 07-14
            // capture #3) - the exact stacking the offset exists to prevent.
            float interval = Mathf.Max(0.05f, tickIntervalSeconds);
            do { _nextTickAt += interval; } while (_nextTickAt <= Time.time);
            using (s_sweepMarker.Auto())
            {
                RunSweep();
            }
        }

        void ClearCullQueue()
        {
            _cullQueue.Clear();
            _cullPending.Clear();
            _cullCursor = 0;
        }

        void RunSweep()
        {
            var index = PrismSpatialIndex.Instance;
            if (index == null || !index.IsAvailable) return;

            // Prune destroyed foci (pool teardown without OnDisable, scene unload).
            for (int i = s_foci.Count - 1; i >= 0; i--)
                if (!s_foci[i]) s_foci.RemoveAt(i);

            // Disabled or nothing to focus on: restore whatever we culled, then idle.
            // Never blanket-cull a focus-less scene - the lifecycle owns collider
            // state when LOD has no opinion.
            if (!lodEnabled || s_foci.Count == 0)
            {
                ClearCullQueue();
                if (_culledAnything)
                {
                    int n = index.CopyLivePrisms(_liveScratch);
                    for (int i = 0; i < n; i++)
                        _liveScratch[i].SetColliderCulledByLod(false);
                    _culledAnything = false;
                }
                _reconcileNextSweep = true; // re-reconcile when foci return
                _nearCount = 0;
                LastNearCount = LastLiveCount = 0;
                return;
            }

            _fociSnapshot.Clear();
            for (int f = 0; f < s_foci.Count; f++)
                _fociSnapshot.Add(s_foci[f].position);

            bool reconcile = _reconcileNextSweep;
            _reconcileNextSweep = false;

            // One Burst pass over the whole population, transitions out.
            // Enter/exit radii form the hysteresis band (see the multiplier knob).
            index.RunLodClassification(_fociSnapshot, lodRadiusMeters,
                lodRadiusMeters * Mathf.Max(1f, lodExitRadiusMultiplier), reconcile, _becameNear, _becameFar);

            // Restores apply immediately and unbudgeted: turning a collider back ON
            // near a focus is the safety-critical direction (a missing collider is a
            // vessel flying through solid mass), and restores on already-enabled
            // prisms are cheap early-outs. A restore also CANCELS any still-queued
            // cull for the same prism - a prism can leave and re-enter the bubble
            // across passes faster than the cull queue drains.
            for (int i = 0; i < _becameNear.Count; i++)
            {
                var prism = _becameNear[i];
                if (!prism) continue;
                _cullPending.Remove(prism);
                prism.SetColliderCulledByLod(false);
            }

            // Culls join the budgeted queue - disabling colliders is the bulk
            // direction on reconciles (population minus near set) and never urgent.
            for (int i = 0; i < _becameFar.Count; i++)
            {
                var prism = _becameFar[i];
                if (prism && _cullPending.Add(prism))
                    _cullQueue.Add(prism);
            }

            // Running near-count from transition deltas (exact on reconcile;
            // approximate between them - telemetry only).
            if (reconcile) _nearCount = _becameNear.Count;
            else _nearCount += _becameNear.Count - _becameFar.Count;

            LastNearCount = _nearCount;
            LastLiveCount = index.LiveCount;
        }

        void DrainCullQueue()
        {
            if (_cullCursor >= _cullQueue.Count)
            {
                if (_cullQueue.Count > 0) ClearCullQueue();
                return;
            }

            float r2 = lodRadiusMeters * lodRadiusMeters;
            int budget = maxColliderTogglesPerFrame;
            while (budget > 0 && _cullCursor < _cullQueue.Count)
            {
                var prism = _cullQueue[_cullCursor++];
                // Set removal FIRST - it works on destroyed refs too, so dead
                // entries can never accumulate in the pending set.
                if (!_cullPending.Remove(prism) || !prism) continue;

                // Charge the budget for every entry that reaches the re-validation
                // work below, not only for applied culls - a churny queue full of
                // now-near entries otherwise drains in ONE frame with an unbounded
                // number of transform reads (a multi-ms self-time spike at scale).
                // Cancelled/dead entries above stay uncharged: hash-remove only.
                budget--;

                // Re-validate against LIVE foci at apply time: the queue can lag
                // classification by many frames on a reconcile, and a pooled slot
                // can be reborn as a different prism near a focus in that window.
                // O(foci) - the same test NotifyPrismActivated uses. Skipping a
                // cull leaves the collider ON (the safe direction); the flag bit
                // reconverges on a later pass.
                bool near = false;
                Vector3 p = prism.transform.position;
                for (int f = 0; f < s_foci.Count; f++)
                {
                    var focus = s_foci[f];
                    if (focus && (focus.position - p).sqrMagnitude <= r2)
                    {
                        near = true;
                        break;
                    }
                }
                if (near) continue;

                prism.SetColliderCulledByLod(true);
                _culledAnything = true;
            }

            if (_cullCursor >= _cullQueue.Count) ClearCullQueue();
        }

        void OnDisable()
        {
            // Component toggled off (or teardown): hand collider ownership back.
            ClearCullQueue();
            _becameNear.Clear();
            _becameFar.Clear();
            _reconcileNextSweep = true;
            _nearCount = 0;
            var index = PrismSpatialIndex.Instance;
            if (index == null || !index.IsAvailable || !_culledAnything) return;
            int n = index.CopyLivePrisms(_liveScratch);
            for (int i = 0; i < n; i++)
                _liveScratch[i].SetColliderCulledByLod(false);
            _culledAnything = false;
        }
    }
}
